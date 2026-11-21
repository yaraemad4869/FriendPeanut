using FriendPeanut.Application.Abstractions;
using Npgsql;

namespace FriendPeanut.Infrastructure.Inventory;

public sealed class NpgsqlStockAuditWriter : IStockAuditWriter
{
    private readonly NpgsqlDataSource _dataSource;

    public NpgsqlStockAuditWriter(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    public async Task WriteAsync(
        string action,
        Guid documentId,
        string details,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(action);
        ArgumentException.ThrowIfNullOrWhiteSpace(details);

        await using var connection =
            await _dataSource.OpenConnectionAsync(cancellationToken);

        await using var command = connection.CreateCommand();

        command.CommandText = """
            INSERT INTO audit_events
            (
                actor_id,
                action,
                entity_type,
                entity_id,
                after_redacted,
                reason,
                correlation_id
            )
            VALUES
            (
                NULL,
                @action,
                'STOCK_DOCUMENT',
                @entity_id,
                CAST(@details AS jsonb),
                NULL,
                @correlation_id
            );
            """;

        command.Parameters.AddWithValue("action", action);
        command.Parameters.AddWithValue("entity_id", documentId);
        command.Parameters.AddWithValue("details", details);
        command.Parameters.AddWithValue(
            "correlation_id",
            Guid.NewGuid().ToString("N"));

        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}