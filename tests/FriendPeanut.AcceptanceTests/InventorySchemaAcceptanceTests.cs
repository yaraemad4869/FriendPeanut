using System.Globalization;
using Npgsql;
using Xunit;

namespace FriendPeanut.AcceptanceTests;

public sealed class InventorySchemaAcceptanceTests
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
    public async Task Inventory_schema_contains_required_operational_objects()
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

        Assert.Equal(names.Length, Convert.ToInt32(await command.ExecuteScalarAsync(), CultureInfo.InvariantCulture));
    }

    [Fact]
    public async Task Inventory_schema_exposes_expected_views()
    {
        await using var connection = await OpenConnectionAsync();

        const string sql = """
            SELECT COUNT(*)
            FROM information_schema.views
            WHERE table_schema = 'friend_peanut';
            """;

        await using var command = new NpgsqlCommand(sql, connection);

        Assert.True(Convert.ToInt32(await command.ExecuteScalarAsync(), CultureInfo.InvariantCulture) >= 2);
    }

    [Fact]
    public async Task Stock_balances_has_check_and_unique_constraints()
    {
        await using var connection = await OpenConnectionAsync();

        const string sql = """
            SELECT
                COUNT(*) FILTER (WHERE contype = 'c'),
                COUNT(*) FILTER (WHERE contype = 'u')
            FROM pg_constraint
            WHERE conrelid = 'friend_peanut.stock_balances'::regclass;
            """;

        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync();

        Assert.True(await reader.ReadAsync());
        Assert.True(reader.GetInt64(0) > 0);
        Assert.True(reader.GetInt64(1) > 0);
    }

    [Fact]
    public async Task Stock_ledger_has_unique_constraints_for_posting_integrity()
    {
        await using var connection = await OpenConnectionAsync();

        const string sql = """
            SELECT COUNT(*)
            FROM pg_constraint
            WHERE conrelid = 'friend_peanut.stock_ledger_entries'::regclass
              AND contype = 'u';
            """;

        await using var command = new NpgsqlCommand(sql, connection);

        Assert.True(Convert.ToInt32(await command.ExecuteScalarAsync(), CultureInfo.InvariantCulture) > 0);
    }

    [Fact]
    public async Task Schema_has_no_invalid_indexes_or_unvalidated_constraints()
    {
        await using var connection = await OpenConnectionAsync();

        const string sql = """
            SELECT
                (SELECT COUNT(*)
                 FROM pg_index i
                 JOIN pg_class t ON t.oid = i.indrelid
                 JOIN pg_namespace n ON n.oid = t.relnamespace
                 WHERE n.nspname = 'friend_peanut'
                   AND NOT i.indisvalid),
                (SELECT COUNT(*)
                 FROM pg_constraint c
                 JOIN pg_namespace n ON n.oid = c.connamespace
                 WHERE n.nspname = 'friend_peanut'
                   AND NOT c.convalidated);
            """;

        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync();

        Assert.True(await reader.ReadAsync());
        Assert.Equal(0L, reader.GetInt64(0));
        Assert.Equal(0L, reader.GetInt64(1));
    }
}