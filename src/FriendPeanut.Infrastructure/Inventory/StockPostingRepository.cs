using FriendPeanut.Application.Abstractions;
using FriendPeanut.Domain.Inventory;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace FriendPeanut.Infrastructure.Inventory;

public sealed class StockPostingRepository : IStockPostingRepository
{
    private readonly Persistence.FriendPeanutDbContext _db;

    public StockPostingRepository(Persistence.FriendPeanutDbContext db)
    {
        _db = db;
    }

    private NpgsqlConnection Connection =>
        (NpgsqlConnection)_db.Database.GetDbConnection();

    private NpgsqlCommand Command(string sql)
    {
        var command = Connection.CreateCommand();
        command.CommandText = sql;
        command.CommandTimeout = 30;
        command.Transaction =
            _db.Database.CurrentTransaction?.GetDbTransaction() as NpgsqlTransaction;
        return command;
    }

    public async Task<bool> DocumentExistsAsync(
        Guid documentId,
        CancellationToken cancellationToken)
    {
        await using var command = Command("""
            SELECT EXISTS (
                SELECT 1
                FROM friend_peanut.documents
                WHERE id=@id
                  AND document_type IN
                    ('STOCK','PRODUCTION','PURCHASE','SALE','HARVEST')
            )
            """);

        command.Parameters.AddWithValue("id", documentId);
        return (bool)(await command.ExecuteScalarAsync(cancellationToken) ?? false);
    }

    public async Task<bool> IsDocumentPostedAsync(
        Guid documentId,
        CancellationToken cancellationToken)
    {
        await using var command = Command("""
            SELECT EXISTS (
                SELECT 1
                FROM friend_peanut.documents
                WHERE id=@id AND status='POSTED'
            )
            """);

        command.Parameters.AddWithValue("id", documentId);
        return (bool)(await command.ExecuteScalarAsync(cancellationToken) ?? false);
    }

    public async Task<long?> GetPostedDocumentVersionAsync(
        Guid documentId,
        CancellationToken cancellationToken)
    {
        await using var command = Command("""
            SELECT version
            FROM friend_peanut.documents
            WHERE id=@id AND status='POSTED'
            """);

        command.Parameters.AddWithValue("id", documentId);

        var result = await command.ExecuteScalarAsync(cancellationToken);

        return result is null or DBNull
            ? null
            : Convert.ToInt64(result, System.Globalization.CultureInfo.InvariantCulture);
    }
    public async Task<IReadOnlyList<StockMovement>> GetMovementsAsync(
        Guid documentId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT l.id, l.item_id, sd.warehouse_id, l.lot_id,
                   l.base_unit_id, l.quantity_base, l.direction,
                   sd.movement_type, sd.counter_warehouse_id
            FROM friend_peanut.stock_documents sd
            JOIN friend_peanut.stock_document_lines l
                ON l.stock_document_id=sd.id
            JOIN friend_peanut.documents d ON d.id=sd.id
            WHERE sd.id=@id AND d.status='APPROVED'
            ORDER BY l.line_no, l.id
            """;

        await using var command = Command(sql);
        command.Parameters.AddWithValue("id", documentId);

        var result = new List<StockMovement>();

        await using var reader =
            await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            var lineId = reader.GetGuid(0);
            var itemId = reader.GetGuid(1);
            var warehouseId = reader.GetGuid(2);
            var lotId = reader.GetGuid(3);
            var unitId = reader.GetGuid(4);
            var quantity = reader.GetDecimal(5);
            var direction = reader.GetInt16(6);
            var movementType = reader.GetString(7);
            var counterWarehouse =
                reader.IsDBNull(8) ? (Guid?)null : reader.GetGuid(8);

            if (movementType == "TRANSFER")
            {
                if (counterWarehouse is null)
                    throw new InvalidOperationException(
                        "Transfer requires a counter warehouse.");

                result.Add(new StockMovement(
                    lineId, itemId, warehouseId, lotId, unitId,
                    quantity, StockMovementDirection.Outbound));

                result.Add(new StockMovement(
                    lineId, itemId, counterWarehouse.Value, lotId, unitId,
                    quantity, StockMovementDirection.Inbound));
            }
            else
            {
                result.Add(new StockMovement(
                    lineId, itemId, warehouseId, lotId, unitId,
                    quantity,
                    direction == 1
                        ? StockMovementDirection.Inbound
                        : StockMovementDirection.Outbound));
            }
        }

        return result;
    }

    public async Task<IReadOnlyDictionary<StockLockKey, decimal>>
        GetAvailableQuantitiesAsync(
            IReadOnlyCollection<StockLockKey> lockKeys,
            CancellationToken cancellationToken)
    {
        var values = new Dictionary<StockLockKey, decimal>();

        foreach (var key in lockKeys
            .Distinct()
            .OrderBy(x => x.ItemId)
            .ThenBy(x => x.WarehouseId)
            .ThenBy(x => x.LotId)
            .ThenBy(x => x.UnitId))
        {
            await using var command = Command("""
                SELECT quantity_base, base_unit_id
                FROM friend_peanut.stock_balances
                WHERE item_id=@item
                  AND warehouse_id=@warehouse
                  AND lot_id=@lot
                """);

            command.Parameters.AddWithValue("item", key.ItemId);
            command.Parameters.AddWithValue("warehouse", key.WarehouseId);
            command.Parameters.AddWithValue("lot", key.LotId);

            await using var reader =
                await command.ExecuteReaderAsync(cancellationToken);

            if (!await reader.ReadAsync(cancellationToken))
                throw new InvalidOperationException(
                    "Stock balance row was not found after locking.");

            if (reader.GetGuid(1) != key.UnitId)
                throw new InvalidOperationException(
                    "Stock balance base unit does not match the document line.");

            values[key] = reader.GetDecimal(0);
        }

        return values;
    }

    public async Task AcquireStockLocksAsync(
        IReadOnlyCollection<StockLockKey> lockKeys,
        CancellationToken cancellationToken)
    {
        foreach (var key in lockKeys
            .Distinct()
            .OrderBy(x => x.ItemId)
            .ThenBy(x => x.WarehouseId)
            .ThenBy(x => x.LotId)
            .ThenBy(x => x.UnitId))
        {
            await using (var ensure = Command("""
                INSERT INTO friend_peanut.stock_balances
                    (item_id, warehouse_id, lot_id, base_unit_id, quantity_base)
                VALUES (@item, @warehouse, @lot, @unit, 0)
                ON CONFLICT (item_id, warehouse_id, lot_id) DO NOTHING
                """))
            {
                ensure.Parameters.AddWithValue("item", key.ItemId);
                ensure.Parameters.AddWithValue("warehouse", key.WarehouseId);
                ensure.Parameters.AddWithValue("lot", key.LotId);
                ensure.Parameters.AddWithValue("unit", key.UnitId);
                await ensure.ExecuteNonQueryAsync(cancellationToken);
            }

            await using var lockCommand = Command("""
                SELECT base_unit_id
                FROM friend_peanut.stock_balances
                WHERE item_id=@item
                  AND warehouse_id=@warehouse
                  AND lot_id=@lot
                FOR UPDATE
                """);

            lockCommand.Parameters.AddWithValue("item", key.ItemId);
            lockCommand.Parameters.AddWithValue("warehouse", key.WarehouseId);
            lockCommand.Parameters.AddWithValue("lot", key.LotId);

            var unit = await lockCommand.ExecuteScalarAsync(cancellationToken);

            if (unit is not Guid actualUnit || actualUnit != key.UnitId)
                throw new InvalidOperationException(
                    "Unable to lock stock balance or base unit mismatch occurred.");
        }
    }

    public async Task<IReadOnlyList<StockMovement>> ApplyLedgerEntriesAsync(
        Guid documentId,
        Guid actorId,
        IReadOnlyCollection<StockMovement> movements,
        CancellationToken cancellationToken)
    {
        await using var batch = Command("""
            INSERT INTO friend_peanut.posting_batches
                (document_id, posted_by, engine_version)
            SELECT d.id, @actor, 'sprint2-v2'
            FROM friend_peanut.documents d
            WHERE d.id=@id AND d.status='APPROVED'
            ON CONFLICT (document_id) DO NOTHING
            RETURNING id
            """);

        batch.Parameters.AddWithValue("id", documentId);
        batch.Parameters.AddWithValue("actor", actorId);

        var batchValue = await batch.ExecuteScalarAsync(cancellationToken);
        Guid batchId;

        if (batchValue is Guid created)
        {
            batchId = created;
        }
        else
        {
            await using var existing = Command("""
                SELECT id
                FROM friend_peanut.posting_batches
                WHERE document_id=@id
                """);

            existing.Parameters.AddWithValue("id", documentId);
            var existingValue =
                await existing.ExecuteScalarAsync(cancellationToken);

            if (existingValue is not Guid found)
                throw new InvalidOperationException(
                    "Posting batch could not be created.");

            batchId = found;
        }

        var insertedMovements = new List<StockMovement>();

        foreach (var movement in movements)
        {
            await using var insert = Command("""
                INSERT INTO friend_peanut.stock_ledger_entries
                    (posting_batch_id, stock_line_id, item_id, warehouse_id,
                     lot_id, base_unit_id, quantity_delta)
                VALUES
                    (@batch, @line, @item, @warehouse, @lot, @unit, @delta)
                ON CONFLICT
                    (posting_batch_id, stock_line_id, warehouse_id)
                DO NOTHING
                RETURNING id
                """);

            insert.Parameters.AddWithValue("batch", batchId);
            insert.Parameters.AddWithValue("line", movement.StockLineId);
            insert.Parameters.AddWithValue("item", movement.ItemId);
            insert.Parameters.AddWithValue("warehouse", movement.WarehouseId);
            insert.Parameters.AddWithValue("lot", movement.LotId);
            insert.Parameters.AddWithValue("unit", movement.UnitId);
            insert.Parameters.AddWithValue("delta", movement.SignedQuantity);

            var inserted = await insert.ExecuteScalarAsync(cancellationToken);

            if (inserted is Guid)
                insertedMovements.Add(movement);
        }

        return insertedMovements;
    }

    public async Task UpdateBalancesAsync(
        IReadOnlyCollection<StockMovement> movements,
        bool negativeOverrideAuthorized,
        string? negativeOverrideReason,
        CancellationToken cancellationToken)
    {
        foreach (var group in movements.GroupBy(x => new StockLockKey(
            x.ItemId, x.WarehouseId, x.LotId, x.UnitId)))
        {
            var delta = group.Sum(x => x.SignedQuantity);

            await using var update = Command("""
                UPDATE friend_peanut.stock_balances
                SET quantity_base=quantity_base+@delta,
                    negative_override_authorized =
                        CASE WHEN quantity_base+@delta < 0
                             THEN @override ELSE false END,
                    negative_override_reason =
                        CASE WHEN quantity_base+@delta < 0
                             THEN @reason ELSE NULL END,
                    version=version+1
                WHERE item_id=@item
                  AND warehouse_id=@warehouse
                  AND lot_id=@lot
                  AND base_unit_id=@unit
                """);

            update.Parameters.AddWithValue("delta", delta);
            update.Parameters.AddWithValue("override", negativeOverrideAuthorized);
            update.Parameters.AddWithValue(
                "reason",
                (object?)negativeOverrideReason ?? DBNull.Value);
            update.Parameters.AddWithValue("item", group.Key.ItemId);
            update.Parameters.AddWithValue("warehouse", group.Key.WarehouseId);
            update.Parameters.AddWithValue("lot", group.Key.LotId);
            update.Parameters.AddWithValue("unit", group.Key.UnitId);

            var affected = await update.ExecuteNonQueryAsync(cancellationToken);

            if (affected != 1)
                throw new InvalidOperationException(
                    "Stock balance row is missing or unit mismatch occurred.");
        }
    }

    public async Task MarkDocumentPostedAsync(
        Guid documentId,
        Guid actorId,
        long expectedVersion,
        CancellationToken cancellationToken)
    {
        await using var command = Command("""
            UPDATE friend_peanut.documents
            SET status='POSTED',
                posted_by=@actor,
                posted_at=now(),
                version=version+1
            WHERE id=@id
              AND status='APPROVED'
              AND version=@version
            """);

        command.Parameters.AddWithValue("id", documentId);
        command.Parameters.AddWithValue("actor", actorId);
        command.Parameters.AddWithValue("version", expectedVersion);

        var affected = await command.ExecuteNonQueryAsync(cancellationToken);

        if (affected != 1)
            throw new InvalidOperationException("CONFLICT_VERSION");
    }
}