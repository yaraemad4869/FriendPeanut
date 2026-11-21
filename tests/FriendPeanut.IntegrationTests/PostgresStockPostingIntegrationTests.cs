using Microsoft.EntityFrameworkCore;
using Npgsql;
using FriendPeanut.Infrastructure.Inventory;
using FriendPeanut.Infrastructure.Persistence;
using Xunit;

namespace FriendPeanut.IntegrationTests;

public sealed class PostgresStockPostingIntegrationTests
{
    private static string ConnectionString =>
        Environment.GetEnvironmentVariable("FP_TEST_DATABASE")
        ?? throw new InvalidOperationException(
            "FP_TEST_DATABASE must point to the isolated Sprint 2 test database.");

    [Fact]
    public async Task Concurrent_same_key_requests_replay_the_completed_database_result()
    {
        var userId = Guid.NewGuid();
        await EnsureIdempotencyTestUserAsync(userId);
        var route = "/api/v1/stock-documents/{id}/post";
        var key = $"integration-{Guid.NewGuid():N}";
        var hash = Convert.ToHexString(
            System.Security.Cryptography.SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes(Guid.NewGuid().ToString("N"))))
            .ToLowerInvariant();

        try
        {
            var options = new DbContextOptionsBuilder<FriendPeanutDbContext>()
                .UseNpgsql(ConnectionString)
                .Options;

            await using (var firstContext = new FriendPeanutDbContext(options))
            await using (var secondContext = new FriendPeanutDbContext(options))
            {
                await using var firstTransaction =
                    await firstContext.Database.BeginTransactionAsync();

                var firstStore = new NpgsqlIdempotencyStore(firstContext);

                var first = await firstStore.ReserveOrReplayAsync(
                    userId,
                    route,
                    key,
                    hash,
                    TimeSpan.FromHours(24),
                    CancellationToken.None);

                Assert.False(first.conflict);
                Assert.NotNull(first.completed);
                Assert.Equal("IN_PROGRESS", first.completed!.State);

                await using var secondTransaction =
                    await secondContext.Database.BeginTransactionAsync();

                var secondStore = new NpgsqlIdempotencyStore(secondContext);

                var secondTask = secondStore.ReserveOrReplayAsync(
                    userId,
                    route,
                    key,
                    hash,
                    TimeSpan.FromHours(24),
                    CancellationToken.None);

                await firstStore.SaveCompletedAsync(
                    first.completed.Id,
                    200,
                    "{\"status\":\"POSTED\",\"version\":2}",
                    CancellationToken.None);

                await firstTransaction.CommitAsync();

                var second = await secondTask.WaitAsync(TimeSpan.FromSeconds(15));

                Assert.False(second.conflict);
                Assert.NotNull(second.completed);
                Assert.Equal("COMPLETED", second.completed!.State);
                Assert.Equal(200, second.completed.ResponseStatus);
                Assert.Contains("POSTED", second.completed.ResponseBody);

                await secondTransaction.CommitAsync();
            }

            await using (var conflictContext = new FriendPeanutDbContext(
                new DbContextOptionsBuilder<FriendPeanutDbContext>()
                    .UseNpgsql(ConnectionString)
                    .Options))
            {
                await using var transaction =
                    await conflictContext.Database.BeginTransactionAsync();

                var store = new NpgsqlIdempotencyStore(conflictContext);

                var conflict = await store.ReserveOrReplayAsync(
                    userId,
                    route,
                    key,
                    new string('a', 64),
                    TimeSpan.FromHours(24),
                    CancellationToken.None);

                Assert.True(conflict.conflict);

                await transaction.RollbackAsync();
            }
        }
        finally
        {
            await using var connection = new NpgsqlConnection(ConnectionString);
            await connection.OpenAsync();

            await using var cleanup = new NpgsqlCommand("""
                DELETE FROM friend_peanut.idempotency_records
                WHERE user_id=@user
                  AND route_key=@route
                  AND idempotency_key=@key
                """, connection);

            cleanup.Parameters.AddWithValue("user", userId);
            cleanup.Parameters.AddWithValue("route", route);
            cleanup.Parameters.AddWithValue("key", key);

            await cleanup.ExecuteNonQueryAsync();
        }
    }

    [Fact]
    public async Task Stock_balances_reconcile_with_the_append_only_ledger()
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand("""
            SELECT COUNT(*)
            FROM
            (
                SELECT
                    b.item_id,
                    b.warehouse_id,
                    b.lot_id,
                    b.quantity_base,
                    COALESCE(SUM(e.quantity_delta), 0) AS ledger_quantity
                FROM friend_peanut.stock_balances b
                LEFT JOIN friend_peanut.stock_ledger_entries e
                    ON e.item_id=b.item_id
                   AND e.warehouse_id=b.warehouse_id
                   AND e.lot_id=b.lot_id
                GROUP BY
                    b.item_id,
                    b.warehouse_id,
                    b.lot_id,
                    b.quantity_base
                HAVING b.quantity_base <> COALESCE(SUM(e.quantity_delta), 0)
            ) discrepancies;
            """, connection);

        var mismatches = Convert.ToInt64(await command.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture);

        Assert.Equal(0L, mismatches);
    }

    private static async Task EnsureIdempotencyTestUserAsync(Guid userId)
    {
        var connectionString = Environment.GetEnvironmentVariable("FP_TEST_DATABASE");
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("FP_TEST_DATABASE is not configured.");

        await using var connection = new Npgsql.NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        const string foreignKeySql = @"
            SELECT ref_ns.nspname, ref_tbl.relname, ref_col.attname
            FROM pg_constraint c
            JOIN pg_class src_tbl ON src_tbl.oid = c.conrelid
            JOIN pg_class ref_tbl ON ref_tbl.oid = c.confrelid
            JOIN pg_namespace ref_ns ON ref_ns.oid = ref_tbl.relnamespace
            JOIN LATERAL unnest(c.confkey) WITH ORDINALITY AS ref_key(attnum, ord) ON TRUE
            JOIN LATERAL unnest(c.conkey) WITH ORDINALITY AS src_key(attnum, ord)
                ON src_key.ord = ref_key.ord
            JOIN pg_attribute ref_col
                ON ref_col.attrelid = ref_tbl.oid
               AND ref_col.attnum = ref_key.attnum
            WHERE c.conname = 'fk_idempotency_records_user_id'
              AND src_tbl.relname = 'idempotency_records'
            LIMIT 1;";

        string schema;
        string table;
        string referencedColumn;

        await using (var fkCommand = new Npgsql.NpgsqlCommand(foreignKeySql, connection))
        await using (var fkReader = await fkCommand.ExecuteReaderAsync())
        {
            if (!await fkReader.ReadAsync())
                throw new InvalidOperationException("Could not resolve fk_idempotency_records_user_id.");

            schema = fkReader.GetString(0);
            table = fkReader.GetString(1);
            referencedColumn = fkReader.GetString(2);
        }

        static string Q(string value) => "\"" + value.Replace("\"", "\"\"") + "\"";

        var existsSql = $"SELECT EXISTS (SELECT 1 FROM {Q(schema)}.{Q(table)} WHERE {Q(referencedColumn)} = @userId);";
        await using (var existsCommand = new Npgsql.NpgsqlCommand(existsSql, connection))
        {
            existsCommand.Parameters.AddWithValue("userId", userId);
            if (Convert.ToBoolean(await existsCommand.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture))
                return;
        }

        const string columnsSql = @"
            SELECT column_name, data_type, udt_name, is_nullable,
                   column_default, is_identity, is_generated
            FROM information_schema.columns
            WHERE table_schema = @schema AND table_name = @table
            ORDER BY ordinal_position;";

        var columns = new System.Collections.Generic.List<(string Name, string DataType, string UdtName, bool Required, bool HasDefault, bool Identity, bool Generated)>();

        await using (var columnsCommand = new Npgsql.NpgsqlCommand(columnsSql, connection))
        {
            columnsCommand.Parameters.AddWithValue("schema", schema);
            columnsCommand.Parameters.AddWithValue("table", table);

            await using var reader = await columnsCommand.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var name = reader.GetString(0);
                var dataType = reader.GetString(1);
                var udtName = reader.GetString(2);
                var nullable = reader.GetString(3) == "YES";
                var hasDefault = !reader.IsDBNull(4);
                var identity = reader.GetString(5) == "YES";
                var generated = reader.GetString(6) != "NEVER";

                columns.Add((name, dataType, udtName, !nullable, hasDefault, identity, generated));
            }
        }

        var insertColumns = new System.Collections.Generic.List<string>();
        var insertValues = new System.Collections.Generic.List<string>();
        var parameters = new System.Collections.Generic.List<(string Name, object Value)>();
        var parameterIndex = 0;

        foreach (var column in columns)
        {
            var isForeignKey = string.Equals(column.Name, referencedColumn, StringComparison.OrdinalIgnoreCase);

            if (!isForeignKey && (!column.Required || column.HasDefault || column.Identity || column.Generated))
                continue;

            string expression;

            if (isForeignKey)
            {
                var parameterName = "p" + parameterIndex++;
                parameters.Add((parameterName, userId));
                expression = "@" + parameterName;
            }
            else if (column.DataType == "USER-DEFINED")
            {
                const string enumSql = @"
                    SELECT e.enumlabel
                    FROM pg_type t
                    JOIN pg_enum e ON e.enumtypid = t.oid
                    WHERE t.typname = @typeName
                    ORDER BY e.enumsortorder
                    LIMIT 1;";

                await using var enumCommand = new Npgsql.NpgsqlCommand(enumSql, connection);
                enumCommand.Parameters.AddWithValue("typeName", column.UdtName);
                var enumValue = await enumCommand.ExecuteScalarAsync();

                if (enumValue == null)
                    throw new InvalidOperationException("Unsupported required user-defined column: " + column.Name);

                var enumParameter = "p" + parameterIndex++;
                parameters.Add((enumParameter, Convert.ToString(enumValue, System.Globalization.CultureInfo.InvariantCulture)!));
                expression = "@" + enumParameter;
            }
            else if (column.DataType == "character varying" ||
                     column.DataType == "character" ||
                     column.DataType == "text" ||
                     column.UdtName == "citext")
            {
                var lowerName = column.Name.ToLowerInvariant();
                string value;

                if (lowerName.Contains("email"))
                    value = "fp-idempotency-" + userId.ToString("N") + "@example.test";
                else if (lowerName.Contains("username"))
                    value = "fp-idempotency-" + userId.ToString("N");
                else if (lowerName.Contains("phone"))
                    value = "01000000000";
                else if (lowerName.Contains("password"))
                    value = "IntegrationTestPasswordHash";
                else if (lowerName.Contains("name"))
                    value = "Integration Test User";
                else
                    value = "fp-test-" + userId.ToString("N");

                if (lowerName.StartsWith("normalized", StringComparison.Ordinal))
                    value = value.ToUpperInvariant();

                var stringParameter = "p" + parameterIndex++;
                parameters.Add((stringParameter, value));
                expression = "@" + stringParameter;
            }
            else if (column.DataType == "uuid")
                expression = "gen_random_uuid()";
            else if (column.DataType == "boolean")
                expression = "FALSE";
            else if (column.DataType == "smallint" || column.DataType == "integer" || column.DataType == "bigint")
            {
                expression = string.Equals(column.Name, "slot_no", StringComparison.OrdinalIgnoreCase)
                    ? $"(SELECT COALESCE(MAX({Q(column.Name)}), 0) + 1 FROM {Q(schema)}.{Q(table)})"
                    : "0";
            }
            else if (column.DataType == "numeric" || column.DataType == "decimal" ||
                     column.DataType == "real" || column.DataType == "double precision")
                expression = "0";
            else if (column.DataType.StartsWith("timestamp", StringComparison.Ordinal))
                expression = "CURRENT_TIMESTAMP";
            else if (column.DataType == "date")
                expression = "CURRENT_DATE";
            else if (column.DataType == "json")
                expression = "'{}'::json";
            else if (column.DataType == "jsonb")
                expression = "'{}'::jsonb";
            else if (column.DataType == "bytea")
                expression = "decode('', 'hex')";
            else
                throw new InvalidOperationException(
                    "Cannot safely seed required user column '" + column.Name +
                    "' of PostgreSQL type '" + column.DataType + "'.");

            insertColumns.Add(Q(column.Name));
            insertValues.Add(expression);
        }

        if (!insertColumns.Contains(Q(referencedColumn)))
            throw new InvalidOperationException("The referenced user key was not included in the seed insert.");

        var insertSql = $"INSERT INTO {Q(schema)}.{Q(table)} ({string.Join(", ", insertColumns)}) VALUES ({string.Join(", ", insertValues)});";

        await using var insertCommand = new Npgsql.NpgsqlCommand(insertSql, connection);
        foreach (var parameter in parameters)
            insertCommand.Parameters.AddWithValue(parameter.Name, parameter.Value);

        await insertCommand.ExecuteNonQueryAsync();
    }
}