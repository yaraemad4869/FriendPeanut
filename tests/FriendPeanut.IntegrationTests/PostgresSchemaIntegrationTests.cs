using System.Globalization;
using Npgsql;
using Xunit;

namespace FriendPeanut.IntegrationTests;

public sealed class PostgresSchemaIntegrationTests
{
    private static string ConnectionString =>
        Environment.GetEnvironmentVariable("FP_TEST_DATABASE")
        ?? throw new InvalidOperationException(
            "FP_TEST_DATABASE must point to the isolated Sprint 2 test database.");

    private static async Task<NpgsqlConnection> OpenConnectionAsync()
    {
        var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        return connection;
    }

    [Fact]
    public async Task Test_database_is_reachable_and_uses_expected_schema()
    {
        await using var connection = await OpenConnectionAsync();

        await using var command = new NpgsqlCommand(
            "SELECT current_database(), current_setting('client_encoding'), " +
            "EXISTS (SELECT 1 FROM information_schema.schemata " +
            "WHERE schema_name = 'friend_peanut');",
            connection);

        await using var reader = await command.ExecuteReaderAsync();

        Assert.True(await reader.ReadAsync());
        Assert.Equal("friend_peanut_sprint2_test", reader.GetString(0));
        Assert.Equal("UTF8", reader.GetString(1));
        Assert.True(reader.GetBoolean(2));
    }

    [Fact]
    public async Task Required_stock_tables_exist()
    {
        await using var connection = await OpenConnectionAsync();

        const string sql = """
            SELECT COUNT(*)
            FROM information_schema.tables
            WHERE table_schema = 'friend_peanut'
              AND table_type = 'BASE TABLE'
              AND table_name = ANY (@names);
            """;

        string[] names =
        [
            "documents",
            "stock_documents",
            "stock_document_lines",
            "posting_batches",
            "stock_ledger_entries",
            "stock_balances",
            "idempotency_records",
            "audit_events"
        ];

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("names", names);

        var count = Convert.ToInt32(await command.ExecuteScalarAsync(), CultureInfo.InvariantCulture);

        Assert.Equal(names.Length, count);
    }

    [Fact]
    public async Task Schema_has_foreign_keys_and_valid_constraints()
    {
        await using var connection = await OpenConnectionAsync();

        const string sql = """
            SELECT
                COUNT(*) FILTER (WHERE contype = 'f'),
                COUNT(*) FILTER (WHERE contype = 'p'),
                COUNT(*) FILTER (WHERE NOT convalidated)
            FROM pg_constraint
            WHERE connamespace = 'friend_peanut'::regnamespace;
            """;

        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync();

        Assert.True(await reader.ReadAsync());
        Assert.True(reader.GetInt64(0) > 0);
        Assert.True(reader.GetInt64(1) > 0);
        Assert.Equal(0L, reader.GetInt64(2));
    }

    [Fact]
    public async Task Database_transaction_can_rollback_test_changes()
    {
        await using var connection = await OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        await using (var create = new NpgsqlCommand(
            "CREATE TEMP TABLE sprint2_test_rollback (id integer PRIMARY KEY);",
            connection,
            transaction))
        {
            await create.ExecuteNonQueryAsync();
        }

        await using (var insert = new NpgsqlCommand(
            "INSERT INTO sprint2_test_rollback (id) VALUES (1);",
            connection,
            transaction))
        {
            await insert.ExecuteNonQueryAsync();
        }

        await transaction.RollbackAsync();

        await using var verify = new NpgsqlCommand(
            "SELECT to_regclass('pg_temp.sprint2_test_rollback') IS NULL;",
            connection);

        Assert.True(Convert.ToBoolean(await verify.ExecuteScalarAsync(), CultureInfo.InvariantCulture));
    }
}