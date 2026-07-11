using System.Globalization;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Tsumugi.Infrastructure.Persistence;

namespace Tsumugi.Infrastructure.Tests;

public sealed class ClaimBatchMigrationTests
{
    private const string MigrationSuffix = "_AddClaimBatchAndDetail";

    [Fact]
    public async Task Latest_migration_creates_claim_tables_indexes_columns_and_restrict_foreign_keys()
    {
        await using var database = await TemporarySqliteDatabase.CreateAsync();
        var (latest, _) = ResolveClaimMigration(database.Context);

        await database.Context.GetService<IMigrator>().MigrateAsync(latest);

        (await TableExistsAsync(database.Connection, "ClaimBatches")).Should().BeTrue();
        (await TableExistsAsync(database.Connection, "ClaimDetails")).Should().BeTrue();

        var batchIndexes = await ReadIndexesAsync(database.Connection, "ClaimBatches");
        batchIndexes.Should().Contain(new KeyValuePair<string, bool>(
            "UX_ClaimBatches_OfficeId_ServiceMonthKey_NewOnly", true));
        batchIndexes.Should().Contain(new KeyValuePair<string, bool>(
            "UX_ClaimBatches_FinalizationOperationId", true));
        batchIndexes.Should().Contain(new KeyValuePair<string, bool>(
            "UX_ClaimBatches_OfficeId_ServiceMonthKey_Revision", true));
        batchIndexes.Should().Contain(new KeyValuePair<string, bool>("IX_ClaimBatches_OriginId", false));
        batchIndexes.Should().Contain(new KeyValuePair<string, bool>("IX_ClaimBatches_ExpectedHeadBatchId", false));

        var detailIndexes = await ReadIndexesAsync(database.Connection, "ClaimDetails");
        detailIndexes.Should().Contain(new KeyValuePair<string, bool>(
            "UX_ClaimDetails_ClaimBatchId_RecipientId", true));
        detailIndexes.Should().Contain(new KeyValuePair<string, bool>("IX_ClaimDetails_ClaimBatchId", false));

        var batchColumns = await ReadColumnsAsync(database.Connection, "ClaimBatches");
        batchColumns.Should().Contain(
            "ServiceMonthKey",
            "Revision",
            "ExpectedHeadRevision",
            "FinalizationOperationId",
            "ClaimMasterVersion",
            "CsvSpecificationVersion",
            "ReportSpecificationVersion",
            "SnapshotApplicationVersion",
            "OperationApplicationVersion",
            "OperationPayloadSchemaVersion",
            "OperationPayloadSha256");

        var detailColumns = await ReadColumnsAsync(database.Connection, "ClaimDetails");
        detailColumns.Should().Contain(
            "SnapshotSchemaVersion",
            "InputSnapshotJson",
            "CalculationSnapshotJson");

        var batchForeignKeys = await ReadForeignKeysAsync(database.Connection, "ClaimBatches");
        batchForeignKeys.Should().BeEquivalentTo(new[]
        {
            new SqliteForeignKey("OriginId", "ClaimBatches", "Id", "RESTRICT"),
            new SqliteForeignKey("ExpectedHeadBatchId", "ClaimBatches", "Id", "RESTRICT"),
        });

        var detailForeignKeys = await ReadForeignKeysAsync(database.Connection, "ClaimDetails");
        detailForeignKeys.Should().BeEquivalentTo(new[]
        {
            new SqliteForeignKey("ClaimBatchId", "ClaimBatches", "Id", "RESTRICT"),
        });

        var batchSql = await ReadCreateTableSqlAsync(database.Connection, "ClaimBatches");
        batchSql.Should().Contain("FK_ClaimBatches_ClaimBatches_OriginId");
        batchSql.Should().Contain("FK_ClaimBatches_ClaimBatches_ExpectedHeadBatchId");
        var detailSql = await ReadCreateTableSqlAsync(database.Connection, "ClaimDetails");
        detailSql.Should().Contain("FK_ClaimDetails_ClaimBatches_ClaimBatchId");
    }

    [Fact]
    public async Task Claim_migration_rolls_back_to_previous_and_reapplies()
    {
        await using var database = await TemporarySqliteDatabase.CreateAsync();
        var (latest, previous) = ResolveClaimMigration(database.Context);
        var migrator = database.Context.GetService<IMigrator>();

        await migrator.MigrateAsync(latest);
        (await TableExistsAsync(database.Connection, "ClaimBatches")).Should().BeTrue();
        (await TableExistsAsync(database.Connection, "ClaimDetails")).Should().BeTrue();

        await migrator.MigrateAsync(previous);
        (await TableExistsAsync(database.Connection, "ClaimBatches")).Should().BeFalse();
        (await TableExistsAsync(database.Connection, "ClaimDetails")).Should().BeFalse();

        await migrator.MigrateAsync(latest);
        (await TableExistsAsync(database.Connection, "ClaimBatches")).Should().BeTrue();
        (await TableExistsAsync(database.Connection, "ClaimDetails")).Should().BeTrue();
        (await ReadIndexesAsync(database.Connection, "ClaimBatches")).Should().ContainKey(
            "UX_ClaimBatches_OfficeId_ServiceMonthKey_NewOnly");
        (await ReadIndexesAsync(database.Connection, "ClaimDetails")).Should().ContainKey(
            "UX_ClaimDetails_ClaimBatchId_RecipientId");
        (await ReadForeignKeysAsync(database.Connection, "ClaimBatches")).Should().HaveCount(2);
        (await ReadForeignKeysAsync(database.Connection, "ClaimDetails")).Should().HaveCount(1);
    }

    private static (string Latest, string Previous) ResolveClaimMigration(TsumugiDbContext context)
    {
        var migrations = context.Database.GetMigrations().ToArray();
        var latest = migrations.Single(migration =>
            migration.EndsWith(MigrationSuffix, StringComparison.Ordinal));
        latest.Should().Be(migrations[^1]);
        migrations.Should().HaveCountGreaterThan(1);
        return (latest, migrations[^2]);
    }

    private static async Task<bool> TableExistsAsync(SqliteConnection connection, string tableName)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = $name;";
        command.Parameters.AddWithValue("$name", tableName);
        return Convert.ToInt64(
            await command.ExecuteScalarAsync(),
            CultureInfo.InvariantCulture) == 1;
    }

    private static async Task<Dictionary<string, bool>> ReadIndexesAsync(
        SqliteConnection connection,
        string tableName)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA index_list(\"{tableName}\");";
        await using var reader = await command.ExecuteReaderAsync();
        var indexes = new Dictionary<string, bool>(StringComparer.Ordinal);
        while (await reader.ReadAsync())
            indexes.Add(reader.GetString(1), reader.GetInt64(2) == 1);
        return indexes;
    }

    private static async Task<HashSet<string>> ReadColumnsAsync(SqliteConnection connection, string tableName)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA table_info(\"{tableName}\");";
        await using var reader = await command.ExecuteReaderAsync();
        var columns = new HashSet<string>(StringComparer.Ordinal);
        while (await reader.ReadAsync())
            columns.Add(reader.GetString(1));
        return columns;
    }

    private static async Task<IReadOnlyList<SqliteForeignKey>> ReadForeignKeysAsync(
        SqliteConnection connection,
        string tableName)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA foreign_key_list(\"{tableName}\");";
        await using var reader = await command.ExecuteReaderAsync();
        var foreignKeys = new List<SqliteForeignKey>();
        while (await reader.ReadAsync())
        {
            foreignKeys.Add(new SqliteForeignKey(
                reader.GetString(3),
                reader.GetString(2),
                reader.GetString(4),
                reader.GetString(6)));
        }

        return foreignKeys;
    }

    private static async Task<string> ReadCreateTableSqlAsync(SqliteConnection connection, string tableName)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT sql FROM sqlite_master WHERE type = 'table' AND name = $name;";
        command.Parameters.AddWithValue("$name", tableName);
        return (string)(await command.ExecuteScalarAsync())!;
    }

    private sealed record SqliteForeignKey(
        string FromColumn,
        string PrincipalTable,
        string PrincipalColumn,
        string OnDelete);

    private sealed class TemporarySqliteDatabase : IAsyncDisposable
    {
        private readonly string _path;

        private TemporarySqliteDatabase(
            string path,
            SqliteConnection connection,
            TsumugiDbContext context)
        {
            _path = path;
            Connection = connection;
            Context = context;
        }

        public SqliteConnection Connection { get; }
        public TsumugiDbContext Context { get; }

        public static async Task<TemporarySqliteDatabase> CreateAsync()
        {
            var path = Path.Combine(Path.GetTempPath(), $"tsumugi-claim-migration-{Guid.NewGuid():N}.db");
            var connectionString = new SqliteConnectionStringBuilder
            {
                DataSource = path,
                ForeignKeys = true,
            }.ToString();
            var connection = new SqliteConnection(connectionString);
            await connection.OpenAsync();

            await using (var command = connection.CreateCommand())
            {
                command.CommandText = "PRAGMA foreign_keys;";
                Convert.ToInt64(
                    await command.ExecuteScalarAsync(),
                    CultureInfo.InvariantCulture).Should().Be(1);
            }

            var options = new DbContextOptionsBuilder<TsumugiDbContext>()
                .UseSqlite(connection)
                .Options;
            return new TemporarySqliteDatabase(path, connection, new TsumugiDbContext(options));
        }

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            await Connection.DisposeAsync();
            SqliteConnection.ClearAllPools();

            foreach (var path in new[] { _path, _path + "-shm", _path + "-wal" })
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
        }
    }
}
