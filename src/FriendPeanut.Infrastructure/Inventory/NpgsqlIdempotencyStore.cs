using FriendPeanut.Application.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace FriendPeanut.Infrastructure.Inventory;

public sealed class NpgsqlIdempotencyStore : IIdempotencyStore
{
    private readonly Persistence.FriendPeanutDbContext _db;

    public NpgsqlIdempotencyStore(Persistence.FriendPeanutDbContext db)
    {
        _db = db;
    }

    private NpgsqlConnection Connection =>
        (NpgsqlConnection)_db.Database.GetDbConnection();

    private NpgsqlCommand CreateCommand(string sql)
    {
        var command = Connection.CreateCommand();
        command.CommandText = sql;
        command.CommandTimeout = 30;
        command.Transaction =
            _db.Database.CurrentTransaction?.GetDbTransaction() as NpgsqlTransaction;
        return command;
    }

    public async Task<(IdempotencyReservation? completed, bool conflict)>
        ReserveOrReplayAsync(
            Guid userId,
            string routeKey,
            string key,
            string requestHash,
            TimeSpan retention,
            CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(routeKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentException.ThrowIfNullOrWhiteSpace(requestHash);

        if (key.Length > 100)
            throw new InvalidOperationException("Idempotency key exceeds 100 characters.");

        if (requestHash.Length != 64)
            throw new InvalidOperationException("Invalid idempotency request hash.");

        await using (var delete = CreateCommand("""
            DELETE FROM friend_peanut.idempotency_records
            WHERE user_id=@user
              AND route_key=@route
              AND idempotency_key=@key
              AND expires_at <= now()
            """))
        {
            delete.Parameters.AddWithValue("user", userId);
            delete.Parameters.AddWithValue("route", routeKey);
            delete.Parameters.AddWithValue("key", key);
            await delete.ExecuteNonQueryAsync(ct);
        }

        await using (var insert = CreateCommand("""
            INSERT INTO friend_peanut.idempotency_records
                (user_id, route_key, idempotency_key, request_hash, state, expires_at)
            VALUES
                (@user, @route, @key, @hash, 'IN_PROGRESS', @expires)
            ON CONFLICT (user_id, route_key, idempotency_key) DO NOTHING
            RETURNING id
            """))
        {
            insert.Parameters.AddWithValue("user", userId);
            insert.Parameters.AddWithValue("route", routeKey);
            insert.Parameters.AddWithValue("key", key);
            insert.Parameters.AddWithValue("hash", requestHash);
            insert.Parameters.AddWithValue(
                "expires",
                DateTimeOffset.UtcNow.Add(retention));

            var created = await insert.ExecuteScalarAsync(ct);
            if (created is Guid id)
            {
                return (
                    new IdempotencyReservation(
                        id,
                        userId,
                        routeKey,
                        key,
                        requestHash,
                        "IN_PROGRESS",
                        null,
                        null),
                    false);
            }
        }

        await using var select = CreateCommand("""
            SELECT id, user_id, route_key, idempotency_key,
                   request_hash, state, response_status,
                   response_body::text
            FROM friend_peanut.idempotency_records
            WHERE user_id=@user
              AND route_key=@route
              AND idempotency_key=@key
            FOR UPDATE
            """);

        select.Parameters.AddWithValue("user", userId);
        select.Parameters.AddWithValue("route", routeKey);
        select.Parameters.AddWithValue("key", key);

        await using var reader = await select.ExecuteReaderAsync(ct);

        if (!await reader.ReadAsync(ct))
            return (null, true);

        var existing = new IdempotencyReservation(
            reader.GetGuid(0),
            reader.GetGuid(1),
            reader.GetString(2),
            reader.GetString(3),
            reader.GetString(4),
            reader.GetString(5),
            reader.IsDBNull(6) ? null : reader.GetInt32(6),
            reader.IsDBNull(7) ? null : reader.GetString(7));

        if (!string.Equals(
            existing.RequestHash,
            requestHash,
            StringComparison.Ordinal))
        {
            return (null, true);
        }

        if (existing.State != "COMPLETED")
            return (null, true);

        return (existing, false);
    }

    public async Task SaveCompletedAsync(
        Guid reservationId,
        int status,
        string body,
        CancellationToken ct)
    {
        await using var command = CreateCommand("""
            UPDATE friend_peanut.idempotency_records
            SET state='COMPLETED',
                response_status=@status,
                response_body=CAST(@body AS jsonb),
                version=version+1
            WHERE id=@id
              AND state='IN_PROGRESS'
            """);

        command.Parameters.AddWithValue("id", reservationId);
        command.Parameters.AddWithValue("status", status);
        command.Parameters.AddWithValue("body", body);

        var affected = await command.ExecuteNonQueryAsync(ct);
        if (affected != 1)
            throw new InvalidOperationException("Unable to complete idempotency reservation.");
    }
}