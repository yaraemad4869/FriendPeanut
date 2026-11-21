using System.Data;
using FriendPeanut.Application.Abstractions;
using FriendPeanut.Domain.Inventory;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using NpgsqlTypes;

namespace FriendPeanut.Infrastructure.Inventory;

public sealed class StockPostingRepository : IStockPostingRepository
{
    private readonly Persistence.FriendPeanutDbContext _db;

    public StockPostingRepository(Persistence.FriendPeanutDbContext db) => _db = db;

    private NpgsqlConnection Connection => (NpgsqlConnection)_db.Database.GetDbConnection();

    private NpgsqlCommand Command(string sql, CancellationToken cancellationToken)
    {
        var command = Connection.CreateCommand();
        command.CommandText = sql;
        command.CommandTimeout = 30;
        command.Transaction = _db.Database.CurrentTransaction?.GetDbTransaction() as NpgsqlTransaction;
        return command;
    }

    public async Task<bool> DocumentExistsAsync(Guid documentId, CancellationToken cancellationToken)
    {
        await using var command = Command(
            "SELECT EXISTS (SELECT 1 FROM friend_peanut.documents WHERE id=@id AND document_type IN ('STOCK','PRODUCTION','PURCHASE','SALE','HARVEST'))",
            cancellationToken);
        command.Parameters.AddWithValue("id", documentId);
        return (bool)(await command.ExecuteScalarAsync(cancellationToken) ?? false);
    }

    public async Task<bool> IsDocumentPostedAsync(Guid documentId, CancellationToken cancellationToken)
    {
        await using var command = Command(
            "SELECT EXISTS (SELECT 1 FROM friend_peanut.documents WHERE id=@id AND status='POSTED')",
            cancellationToken);
        command.Parameters.AddWithValue("id", documentId);
        return (bool)(await command.ExecuteScalarAsync(cancellationToken) ?? false);
    }

    public async Task<IReadOnlyList<StockMovement>> GetMovementsAsync(Guid documentId, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT l.id, l.item_id, sd.warehouse_id, l.lot_id, l.base_unit_id,
                   l.quantity_base, l.direction, sd.movement_type, sd.counter_warehouse_id
            FROM friend_peanut.stock_documents sd
            JOIN friend_peanut.stock_document_lines l ON l.stock_document_id=sd.id
            JOIN friend_peanut.documents d ON d.id=sd.id
            WHERE sd.id=@id AND d.status='APPROVED'
            ORDER BY l.line_no, l.id
            """;

        await using var command = Command(sql, cancellationToken);
        command.Parameters.AddWithValue("id", documentId);
        var result = new List<StockMovement>();

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var lineId = reader.GetGuid(0);
            var itemId = reader.GetGuid(1);
            var warehouseId = reader.GetGuid(2);
            var lotId = reader.GetGuid(3);
            var baseUnitId = reader.GetGuid(4);
            var quantity = reader.GetDecimal(5);
            var direction = reader.GetInt16(6);
            var movementType = reader.GetString(7);
            var counterWarehouse = reader.IsDBNull(8) ? (Guid?)null : reader.GetGuid(8);

            if (movementType == "TRANSFER")
            {
                if (counterWarehouse is null)
                    throw new InvalidOperationException("Transfer requires a counter warehouse.");

                result.Add(new StockMovement(lineId, itemId, warehouseId, lotId, baseUnitId,
                    quantity, StockMovementDirection.Outbound));
                result.Add(new StockMovement(lineId, itemId, counterWarehouse.Value, lotId, baseUnitId,
                    quantity, StockMovementDirection.Inbound));
            }
            else
            {
                result.Add(new StockMovement(lineId, itemId, warehouseId, lotId, baseUnitId,
                    quantity, direction == 1 ? StockMovementDirection.Inbound : StockMovementDirection.Outbound));
            }
        }

        return result;
    }

    public async Task<IReadOnlyDictionary<Guid, decimal>> GetAvailableQuantitiesAsync(
        IReadOnlyCollection<Guid> itemIds,
        IReadOnlyCollection<Guid> warehouseIds,
        CancellationToken cancellationToken)
    {
        if (itemIds.Count == 0 || warehouseIds.Count == 0)
            return new Dictionary<Guid, decimal>();

        await using var command = Command("""
            SELECT item_id, SUM(quantity_base)
            FROM friend_peanut.stock_balances
            WHERE item_id = ANY(@items) AND warehouse_id = ANY(@warehouses)
            GROUP BY item_id
            """, cancellationToken);
        command.Parameters.AddWithValue("items", itemIds.ToArray());
        command.Parameters.AddWithValue("warehouses", warehouseIds.ToArray());

        var values = new Dictionary<Guid, decimal>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            values[reader.GetGuid(0)] = reader.GetDecimal(1);
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
            .ThenBy(x => x.LotId))
        {
            await using var ensure = Command("""
                INSERT INTO friend_peanut.stock_balances
                    (item_id, warehouse_id, lot_id, base_unit_id, quantity_base)
                SELECT @item, @warehouse, @lot, l.base_unit_id, 0
                FROM friend_peanut.stock_document_lines l
                WHERE l.item_id=@item AND l.lot_id=@lot
                ORDER BY l.id
                LIMIT 1
                ON CONFLICT (item_id, warehouse_id, lot_id) DO NOTHING
                """, cancellationToken);
            ensure.Parameters.AddWithValue("item", key.ItemId);
            ensure.Parameters.AddWithValue("warehouse", key.WarehouseId);
            ensure.Parameters.AddWithValue("lot", key.LotId);
            await ensure.ExecuteNonQueryAsync(cancellationToken);

            await using var lockCommand = Command("""
                SELECT id FROM friend_peanut.stock_balances
                WHERE item_id=@item AND warehouse_id=@warehouse AND lot_id=@lot
                FOR UPDATE
                """, cancellationToken);
            lockCommand.Parameters.AddWithValue("item", key.ItemId);
            lockCommand.Parameters.AddWithValue("warehouse", key.WarehouseId);
            lockCommand.Parameters.AddWithValue("lot", key.LotId);
            var locked = await lockCommand.ExecuteScalarAsync(cancellationToken);
            if (locked is null)
                throw new InvalidOperationException("Unable to create or lock stock balance row.");
        }
    }

    public async Task ApplyLedgerEntriesAsync(
        Guid documentId,
        IReadOnlyCollection<StockMovement> movements,
        CancellationToken cancellationToken)
    {
        await using var batch = Command("""
            INSERT INTO friend_peanut.posting_batches(document_id, posted_by, engine_version)
            SELECT d.id, d.approved_by, 'sprint2-v1'
            FROM friend_peanut.documents d
            WHERE d.id=@id AND d.status='APPROVED'
            ON CONFLICT(document_id) DO NOTHING
            RETURNING id
            """, cancellationToken);
        batch.Parameters.AddWithValue("id", documentId);
        var batchValue = await batch.ExecuteScalarAsync(cancellationToken);

        Guid batchId;
        if (batchValue is Guid created)
        {
            batchId = created;
        }
        else
        {
            await using var existing = Command(
                "SELECT id FROM friend_peanut.posting_batches WHERE document_id=@id",
                cancellationToken);
            existing.Parameters.AddWithValue("id", documentId);
            var existingValue = await existing.ExecuteScalarAsync(cancellationToken);
            if (existingValue is not Guid found)
                throw new InvalidOperationException("Posting batch could not be created.");
            batchId = found;
        }

        foreach (var movement in movements)
        {
            await using var insert = Command("""
                INSERT INTO friend_peanut.stock_ledger_entries
                    (posting_batch_id, stock_line_id, item_id, warehouse_id, lot_id,
                     base_unit_id, quantity_delta)
                VALUES (@batch, @line, @item, @warehouse, @lot, @unit, @delta)
                ON CONFLICT (posting_batch_id, stock_line_id, warehouse_id) DO NOTHING
                """, cancellationToken);
            insert.Parameters.AddWithValue("batch", batchId);
            insert.Parameters.AddWithValue("line", movement.StockLineId);
            insert.Parameters.AddWithValue("item", movement.ItemId);
            insert.Parameters.AddWithValue("warehouse", movement.WarehouseId);
            insert.Parameters.AddWithValue("lot", movement.LotId);
            insert.Parameters.AddWithValue("unit", movement.UnitId);
            insert.Parameters.AddWithValue("delta", movement.SignedQuantity);
            await insert.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    public async Task UpdateBalancesAsync(
        IReadOnlyCollection<StockMovement> movements,
        CancellationToken cancellationToken)
    {
        foreach (var group in movements.GroupBy(x => new
        {
            x.ItemId, x.WarehouseId, x.LotId, x.UnitId
        }))
        {
            var delta = group.Sum(x => x.SignedQuantity);
            await using var update = Command("""
                UPDATE friend_peanut.stock_balances
                SET quantity_base=quantity_base+@delta,
                    version=version+1
                WHERE item_id=@item AND warehouse_id=@warehouse
                  AND lot_id=@lot AND base_unit_id=@unit
                """, cancellationToken);
            update.Parameters.AddWithValue("delta", delta);
            update.Parameters.AddWithValue("item", group.Key.ItemId);
            update.Parameters.AddWithValue("warehouse", group.Key.WarehouseId);
            update.Parameters.AddWithValue("lot", group.Key.LotId);
            update.Parameters.AddWithValue("unit", group.Key.UnitId);
            var affected = await update.ExecuteNonQueryAsync(cancellationToken);
            if (affected != 1)
                throw new InvalidOperationException("Stock balance row is missing or unit mismatch occurred.");
        }
    }

    public async Task MarkDocumentPostedAsync(
        Guid documentId,
        long expectedVersion,
        CancellationToken cancellationToken)
    {
        await using var command = Command("""
            UPDATE friend_peanut.documents
            SET status='POSTED', posted_by=approved_by, posted_at=now(), version=version+1
            WHERE id=@id AND status='APPROVED' AND version=@version
            """, cancellationToken);
        command.Parameters.AddWithValue("id", documentId);
        command.Parameters.AddWithValue("version", expectedVersion);
        var affected = await command.ExecuteNonQueryAsync(cancellationToken);
        if (affected != 1)
            throw new InvalidOperationException("Document is not approved or its version has changed.");
    }
}