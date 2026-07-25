using System.Globalization;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Tsumugi.Infrastructure.Persistence;

namespace Tsumugi.Infrastructure.Tests;

/// <summary>
/// 日次実績から導出できない3項目（訪問支援特別加算の算定回数・算定時間数、施設外支援の累計日数）を
/// 個別入力列として追加したマイグレーションの列とCancel制約を固定する。
/// </summary>
public sealed class Phase33GroupBExplicitAdditionInputsMigrationTests
{
    private const string MigrationSuffix = "_Phase33GroupBExplicitAdditionInputs";

    /// <summary>ClaimInputs へ追加した月次個別入力列（provider:J611:01:052 / provider:J611:01:054）。</summary>
    private static readonly string[] ClaimInputColumns =
    [
        "SpecialVisitSupportBilledCount",
        "OffsiteSupportCumulativeDays",
    ];

    /// <summary>DailyRecords へ追加した日次個別入力列（provider:J611:02:028）。</summary>
    private const string DailyRecordColumn = "SpecialVisitSupportBilledHours";

    [Fact]
    public async Task Target_adds_nullable_integer_columns_and_keeps_no_statutory_limit_in_the_schema()
    {
        await using var database = await TemporarySqliteDatabase.CreateAsync();
        var (target, _) = ResolveMigration(database.Context);

        await database.Context.GetService<IMigrator>().MigrateAsync(target);

        foreach (var column in ClaimInputColumns)
        {
            (await ReadColumnAsync(database.Connection, "ClaimInputs", column))
                .Should().Be(new SqliteColumn("INTEGER", NotNull: false, DefaultValue: null));
        }

        (await ReadColumnAsync(database.Connection, "DailyRecords", DailyRecordColumn))
            .Should().Be(new SqliteColumn("INTEGER", NotNull: false, DefaultValue: null));

        // 上限（施設外支援の年度累計・訪問支援特別加算の月内算定回数）は制度実値なのでDBにも書かない
        // （CLAUDE.md §ハード制約3）。新列に紐づくCHECK制約はCancel payloadのみ。
        var claimInputSql = await ReadCreateTableSqlAsync(database.Connection, "ClaimInputs");
        foreach (var column in ClaimInputColumns)
            claimInputSql.Should().Contain($"\"{column}\" IS NULL");
        var dailyRecordSql = await ReadCreateTableSqlAsync(database.Connection, "DailyRecords");
        dailyRecordSql.Should().NotContain("CHECK");
    }

    [Fact]
    public async Task Target_extends_cancel_payload_check_to_the_two_monthly_columns()
    {
        await using var database = await TemporarySqliteDatabase.CreateAsync();
        var (target, _) = ResolveMigration(database.Context);
        await database.Context.GetService<IMigrator>().MigrateAsync(target);
        var seed = await SeedCancelChainAsync(database.Connection);

        foreach (var column in ClaimInputColumns)
        {
            var act = () => ExecuteNonQueryAsync(
                database.Connection,
                $"UPDATE \"ClaimInputs\" SET \"{column}\" = 1 WHERE \"Id\" = $id;",
                ("$id", seed.CancelId));

            var exception = await act.Should().ThrowAsync<SqliteException>(
                $"Cancel は {column} を持てない");
            exception.Which.SqliteErrorCode.Should().Be(19);
            exception.Which.SqliteExtendedErrorCode.Should().Be(275);
            exception.Which.Message.Should().Contain("CK_ClaimInputs_CancelPayload");
        }

        // Kind=New 側は同じ値を保持できる（Cancel専用の制約であることの対照）。
        await ExecuteNonQueryAsync(
            database.Connection,
            "UPDATE \"ClaimInputs\" SET \"SpecialVisitSupportBilledCount\" = 2, " +
            "\"OffsiteSupportCumulativeDays\" = 181 WHERE \"Id\" = $id;",
            ("$id", seed.RootId));
    }

    [Fact]
    public async Task Down_removes_the_three_columns_and_reup_is_deterministic()
    {
        await using var database = await TemporarySqliteDatabase.CreateAsync();
        var (target, previous) = ResolveMigration(database.Context);
        var migrator = database.Context.GetService<IMigrator>();

        await migrator.MigrateAsync(target);
        var seed = await SeedCancelChainAsync(database.Connection);

        await migrator.MigrateAsync(previous);
        foreach (var column in ClaimInputColumns)
            (await ReadColumnsAsync(database.Connection, "ClaimInputs")).Should().NotContain(column);
        (await ReadColumnsAsync(database.Connection, "DailyRecords")).Should().NotContain(DailyRecordColumn);
        (await CountRowsAsync(database.Connection, "ClaimInputs")).Should().Be(2);

        await migrator.MigrateAsync(target);
        foreach (var column in ClaimInputColumns)
            (await ReadColumnsAsync(database.Connection, "ClaimInputs")).Should().Contain(column);
        (await ReadColumnsAsync(database.Connection, "DailyRecords")).Should().Contain(DailyRecordColumn);
        (await CountRowsAsync(database.Connection, "ClaimInputs")).Should().Be(2);
        (await ReadScalarAsync(
            database.Connection,
            "SELECT \"SpecialVisitSupportBilledCount\" FROM \"ClaimInputs\" WHERE \"Id\" = $id;",
            ("$id", seed.RootId))).Should().BeNull();
    }

    private static (string Target, string Previous) ResolveMigration(TsumugiDbContext context)
    {
        var migrations = context.Database.GetMigrations().ToArray();
        var targetIndex = Array.FindIndex(migrations, migration =>
            migration.EndsWith(MigrationSuffix, StringComparison.Ordinal));
        targetIndex.Should().BeGreaterThan(0);
        return (migrations[targetIndex], migrations[targetIndex - 1]);
    }

    private static async Task<CancelChainSeed> SeedCancelChainAsync(SqliteConnection connection)
    {
        var officeId = Guid.NewGuid();
        var recipientId = Guid.NewGuid();
        var rootId = Guid.NewGuid();
        var cancelId = Guid.NewGuid();
        var createdAt = DateTimeOffset.UnixEpoch;

        await ExecuteNonQueryAsync(
            connection,
            """
            INSERT INTO "Offices"
                ("Id", "OfficeNumber", "Name", "CreatedAt", "CreatedBy", "ConcurrencyToken",
                 "RegionGrade", "ServiceCategory")
            VALUES ($id, '1310000009', '加算入力テスト事業所', $createdAt, 'tester', $token, 1, 1);
            """,
            ("$id", officeId), ("$createdAt", createdAt), ("$token", Guid.NewGuid()));
        await ExecuteNonQueryAsync(
            connection,
            """
            INSERT INTO "Recipients"
                ("Id", "KanjiName", "KanaName", "DateOfBirth", "CreatedAt", "CreatedBy", "ConcurrencyToken")
            VALUES ($id, '加算利用者', 'カサンリヨウシャ', '1990-01-01', $createdAt, 'tester', $token);
            """,
            ("$id", recipientId), ("$createdAt", createdAt), ("$token", Guid.NewGuid()));

        foreach (var (id, revision, kind, expectedHeadId) in new (Guid, int, int, Guid?)[]
                 {
                     (rootId, 1, 1, null),
                     (cancelId, 2, 3, rootId),
                 })
        {
            await ExecuteNonQueryAsync(
                connection,
                """
                INSERT INTO "ClaimInputs"
                    ("Id", "OfficeId", "RecipientId", "ServiceMonthKey", "RootId", "Revision", "Kind",
                     "ExpectedHeadId", "CreatedAt", "CreatedBy", "ConcurrencyToken")
                VALUES ($id, $officeId, $recipientId, 202607, $rootId, $revision, $kind,
                        $expectedHeadId, $createdAt, 'tester', $token);
                """,
                ("$id", id),
                ("$officeId", officeId),
                ("$recipientId", recipientId),
                ("$rootId", rootId),
                ("$revision", revision),
                ("$kind", kind),
                ("$expectedHeadId", expectedHeadId),
                ("$createdAt", createdAt),
                ("$token", Guid.NewGuid()));
        }

        return new CancelChainSeed(rootId, cancelId);
    }

    private static async Task ExecuteNonQueryAsync(
        SqliteConnection connection,
        string sql,
        params (string Name, object? Value)[] parameters)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        foreach (var (name, value) in parameters)
            command.Parameters.AddWithValue(name, value ?? DBNull.Value);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<object?> ReadScalarAsync(
        SqliteConnection connection,
        string sql,
        params (string Name, object? Value)[] parameters)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        foreach (var (name, value) in parameters)
            command.Parameters.AddWithValue(name, value ?? DBNull.Value);
        var scalar = await command.ExecuteScalarAsync();
        return scalar is DBNull ? null : scalar;
    }

    private static async Task<long> CountRowsAsync(SqliteConnection connection, string table) =>
        Convert.ToInt64(
            await ReadScalarAsync(connection, $"SELECT COUNT(*) FROM \"{table}\";"),
            CultureInfo.InvariantCulture);

    private static async Task<SqliteColumn> ReadColumnAsync(
        SqliteConnection connection,
        string table,
        string column)
    {
        var columns = await ReadColumnDetailsAsync(connection, table);
        columns.Should().ContainKey(column);
        return columns[column];
    }

    private static async Task<HashSet<string>> ReadColumnsAsync(SqliteConnection connection, string table) =>
        [.. (await ReadColumnDetailsAsync(connection, table)).Keys];

    private static async Task<Dictionary<string, SqliteColumn>> ReadColumnDetailsAsync(
        SqliteConnection connection,
        string table)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA table_info(\"{table}\");";
        await using var reader = await command.ExecuteReaderAsync();
        var columns = new Dictionary<string, SqliteColumn>(StringComparer.Ordinal);
        while (await reader.ReadAsync())
        {
            columns.Add(
                reader.GetString(1),
                new SqliteColumn(
                    reader.GetString(2),
                    reader.GetInt64(3) == 1,
                    reader.IsDBNull(4) ? null : reader.GetString(4)));
        }

        return columns;
    }

    private static async Task<string> ReadCreateTableSqlAsync(SqliteConnection connection, string table) =>
        (string)(await ReadScalarAsync(
            connection,
            "SELECT sql FROM sqlite_master WHERE type = 'table' AND name = $name;",
            ("$name", table)))!;

    private sealed record CancelChainSeed(Guid RootId, Guid CancelId);

    private sealed record SqliteColumn(string Type, bool NotNull, string? DefaultValue);

    private sealed class TemporarySqliteDatabase : IAsyncDisposable
    {
        private readonly string _path;

        private TemporarySqliteDatabase(string path, SqliteConnection connection, TsumugiDbContext context)
        {
            _path = path;
            Connection = connection;
            Context = context;
        }

        public SqliteConnection Connection { get; }
        public TsumugiDbContext Context { get; }

        public static async Task<TemporarySqliteDatabase> CreateAsync()
        {
            var path = Path.Combine(
                Path.GetTempPath(),
                $"tsumugi-phase33-group-b-{Guid.NewGuid():N}.db");
            var connectionString = new SqliteConnectionStringBuilder
            {
                DataSource = path,
                ForeignKeys = true,
            }.ToString();
            var connection = new SqliteConnection(connectionString);
            await connection.OpenAsync();
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

            foreach (var file in new[] { _path, _path + "-shm", _path + "-wal" })
            {
                if (File.Exists(file))
                    File.Delete(file);
            }
        }
    }
}
