using FriendPeanut.Application.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace FriendPeanut.Infrastructure.Inventory;

public sealed class NpgsqlStockAuditWriter : IStockAuditWriter
{
    private readonly Persistence.FriendPeanutDbContext _db;

    public NpgsqlStockAuditWriter(Persistence.FriendPeanutDbContext db)
    {
        _db = db;
    }

    public async Task WriteAsync(
        string action,
        Guid documentId,
        Guid actorId,
        string details,
        string? reason,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(action);
        ArgumentException.ThrowIfNullOrWhiteSpace(details);

        var connection =
            (NpgsqlConnection)_db.Database.GetDbConnection();

        await using var command = connection.CreateCommand();

        command.CommandText = """
            INSERT INTO friend_peanut.audit_events
                (actor_id, action, entity_type, entity_id,
                 after_redacted, reason, correlation_id)
            VALUES
                (@actor, @action, 'STOCK_DOCUMENT', @entity,
                 CAST(@details AS jsonb), @reason, @correlation)
            """;

        command.Transaction =
            _db.Database.CurrentTransaction?.GetDbTransaction()
                as NpgsqlTransaction;

        command.Parameters.AddWithValue("actor", actorId);
        command.Parameters.AddWithValue("action", action);
        command.Parameters.AddWithValue("entity", documentId);
        command.Parameters.AddWithValue("details", details);
        command.Parameters.AddWithValue(
            "reason",
            (object?)reason ?? DBNull.Value);
        command.Parameters.AddWithValue(
            "correlation",
            Guid.NewGuid().ToString("N"));

        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}