using System.Globalization;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.Enums;
using Tsumugi.Domain.Logic.Claim.Models;
using Tsumugi.Infrastructure.Persistence;

namespace Tsumugi.Infrastructure.Tests;

/// <summary>
/// 施設区分（ADR 0021・ADR 0047）を OfficeClaimProfiles へ構造化入力として永続化する
/// マイグレーションの列・enum変換・Cancel制約を固定する。処遇改善加算の一部区分は
/// 指定障害者支援施設で率が別立てになるため、体制届からの推測ではなく事業所ごとの
/// 明示入力として保存できることを確認する。
/// </summary>
public sealed class Phase35OfficeFacilityClassificationMigrationTests
{
    private const string MigrationSuffix = "_Phase35OfficeFacilityClassification";
    private const string NewColumn = "FacilityClassification";

    [Fact]
    public async Task Target_adds_a_nullable_facility_classification_column_and_round_trips_values()
    {
        await using var database = await TemporarySqliteDatabase.CreateAsync();
        var (target, _) = ResolveMigration(database.Context);

        await database.Context.GetService<IMigrator>().MigrateAsync(target);

        (await ReadColumnAsync(database.Connection, "OfficeClaimProfiles", NewColumn))
            .Should().Be(new SqliteColumn("INTEGER", NotNull: false, DefaultValue: null));

        var officeId = await SeedOfficeAsync(database.Connection);
        var withValueId = Guid.NewGuid();
        var withoutValueId = Guid.NewGuid();
        database.Context.Set<OfficeClaimProfile>().AddRange(
            NewProfile(withValueId, officeId, new DateOnly(2026, 4, 1), FacilityClassification.DesignatedSupportFacility),
            NewProfile(withoutValueId, officeId, new DateOnly(2026, 5, 1), facilityClassification: null));
        await database.Context.SaveChangesAsync();
        database.Context.ChangeTracker.Clear();

        var persisted = await database.Context.Set<OfficeClaimProfile>()
            .AsNoTracking()
            .Where(profile => profile.Id == withValueId || profile.Id == withoutValueId)
            .ToDictionaryAsync(profile => profile.Id, profile => profile.FacilityClassification);
        persisted[withValueId].Should().Be(FacilityClassification.DesignatedSupportFacility);
        persisted[withoutValueId].Should().BeNull();
    }

    [Fact]
    public async Task Target_extends_cancel_payload_check_to_the_facility_classification_column()
    {
        await using var database = await TemporarySqliteDatabase.CreateAsync();
        var (target, _) = ResolveMigration(database.Context);
        await database.Context.GetService<IMigrator>().MigrateAsync(target);
        var officeId = await SeedOfficeAsync(database.Connection);

        var rootId = Guid.NewGuid();
        var root = NewProfile(rootId, officeId, new DateOnly(2026, 4, 1), facilityClassification: null);
        database.Context.Set<OfficeClaimProfile>().Add(root);
        await database.Context.SaveChangesAsync();
        database.Context.ChangeTracker.Clear();

        var cancel = new OfficeClaimProfile
        {
            Id = Guid.NewGuid(),
            OfficeId = officeId,
            EffectiveFrom = root.EffectiveFrom,
            RootId = rootId,
            Revision = 2,
            Kind = RecordKind.Cancel,
            ExpectedHeadId = rootId,
            FacilityClassification = FacilityClassification.General,
            CreatedAt = DateTimeOffset.UnixEpoch.AddMinutes(1),
            CreatedBy = "tester",
            ConcurrencyToken = Guid.NewGuid(),
        };
        database.Context.Set<OfficeClaimProfile>().Add(cancel);

        var act = () => database.Context.SaveChangesAsync();

        var exception = await act.Should().ThrowAsync<DbUpdateException>(
            "CancelレコードはFacilityClassificationを持てない");
        exception.Which.InnerException.Should().BeOfType<SqliteException>().Which.Message
            .Should().Contain("CK_OfficeClaimProfiles_CancelPayload");

        // Kind=New側は同じ値を保持できる（Cancel専用の制約であることの対照）。
        database.Context.ChangeTracker.Clear();
        var correction = NewProfile(
            Guid.NewGuid(), officeId, new DateOnly(2026, 5, 1), FacilityClassification.General);
        database.Context.Set<OfficeClaimProfile>().Add(correction);
        await database.Context.SaveChangesAsync();
    }

    [Fact]
    public async Task Target_rejects_a_facility_classification_value_outside_the_closed_set()
    {
        await using var database = await TemporarySqliteDatabase.CreateAsync();
        var (target, _) = ResolveMigration(database.Context);
        await database.Context.GetService<IMigrator>().MigrateAsync(target);
        var officeId = await SeedOfficeAsync(database.Connection);

        // FacilityClassificationはGeneral(1)/DesignatedSupportFacility(2)の閉集合。
        // ReformStatusのCK_OfficeClaimProfiles_ReformStatus_ClosedSetと同様、
        // 未定義の整数値（列挙に無い99）はDB側でも拒否されることを固定する。
        var invalid = NewProfile(
            Guid.NewGuid(), officeId, new DateOnly(2026, 4, 1), (FacilityClassification)99);
        database.Context.Set<OfficeClaimProfile>().Add(invalid);

        var act = () => database.Context.SaveChangesAsync();

        var exception = await act.Should().ThrowAsync<DbUpdateException>(
            "FacilityClassificationは1（General）・2（DesignatedSupportFacility）以外を持てない");
        exception.Which.InnerException.Should().BeOfType<SqliteException>().Which.Message
            .Should().Contain("CK_OfficeClaimProfiles_FacilityClassification_ClosedSet");
    }

    [Fact]
    public async Task Down_removes_the_column_and_reup_is_deterministic()
    {
        await using var database = await TemporarySqliteDatabase.CreateAsync();
        var (target, previous) = ResolveMigration(database.Context);
        var migrator = database.Context.GetService<IMigrator>();

        await migrator.MigrateAsync(target);
        var officeId = await SeedOfficeAsync(database.Connection);
        var rootId = Guid.NewGuid();
        await InsertProfileRawAsync(
            database.Connection, rootId, officeId, new DateOnly(2026, 4, 1), rootId, 1, 1, null,
            (int)FacilityClassification.DesignatedSupportFacility);

        await migrator.MigrateAsync(previous);
        (await ReadColumnsAsync(database.Connection, "OfficeClaimProfiles")).Should().NotContain(NewColumn);
        (await CountRowsAsync(database.Connection, "OfficeClaimProfiles")).Should().Be(1);

        await migrator.MigrateAsync(target);
        (await ReadColumnsAsync(database.Connection, "OfficeClaimProfiles")).Should().Contain(NewColumn);
        (await CountRowsAsync(database.Connection, "OfficeClaimProfiles")).Should().Be(1);
        (await ReadScalarAsync(
            database.Connection,
            $"SELECT \"{NewColumn}\" FROM \"OfficeClaimProfiles\" WHERE \"Id\" = $id;",
            ("$id", rootId))).Should().BeNull();
    }

    private static OfficeClaimProfile NewProfile(
        Guid id,
        Guid officeId,
        DateOnly effectiveFrom,
        FacilityClassification? facilityClassification) => new()
        {
            Id = id,
            OfficeId = officeId,
            EffectiveFrom = effectiveFrom,
            RootId = id,
            Revision = 1,
            Kind = RecordKind.New,
            FacilityClassification = facilityClassification,
            CreatedAt = DateTimeOffset.UnixEpoch,
            CreatedBy = "tester",
            ConcurrencyToken = Guid.NewGuid(),
        };

    private static (string Target, string Previous) ResolveMigration(TsumugiDbContext context)
    {
        var migrations = context.Database.GetMigrations().ToArray();
        var targetIndex = Array.FindIndex(migrations, migration =>
            migration.EndsWith(MigrationSuffix, StringComparison.Ordinal));
        targetIndex.Should().BeGreaterThan(0);
        return (migrations[targetIndex], migrations[targetIndex - 1]);
    }

    private static async Task<Guid> SeedOfficeAsync(SqliteConnection connection)
    {
        var officeId = Guid.NewGuid();
        await ExecuteNonQueryAsync(
            connection,
            """
            INSERT INTO "Offices"
                ("Id", "OfficeNumber", "Name", "CreatedAt", "CreatedBy", "ConcurrencyToken",
                 "RegionGrade", "ServiceCategory")
            VALUES ($id, '1310000009', '施設区分テスト事業所', $createdAt, 'tester', $token, 1, 1);
            """,
            ("$id", officeId), ("$createdAt", DateTimeOffset.UnixEpoch), ("$token", Guid.NewGuid()));
        return officeId;
    }

    private static async Task InsertProfileRawAsync(
        SqliteConnection connection,
        Guid id,
        Guid officeId,
        DateOnly effectiveFrom,
        Guid rootId,
        int revision,
        int kind,
        Guid? expectedHeadId,
        int? facilityClassification)
    {
        await ExecuteNonQueryAsync(
            connection,
            """
            INSERT INTO "OfficeClaimProfiles"
                ("Id", "OfficeId", "EffectiveFrom", "RootId", "Revision", "Kind", "ExpectedHeadId",
                 "FacilityClassification", "CreatedAt", "CreatedBy", "ConcurrencyToken")
            VALUES ($id, $officeId, $effectiveFrom, $rootId, $revision, $kind,
                    $expectedHeadId, $facilityClassification, $createdAt, 'tester', $token);
            """,
            ("$id", id),
            ("$officeId", officeId),
            ("$effectiveFrom", effectiveFrom.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)),
            ("$rootId", rootId),
            ("$revision", revision),
            ("$kind", kind),
            ("$expectedHeadId", expectedHeadId),
            ("$facilityClassification", facilityClassification),
            ("$createdAt", DateTimeOffset.UnixEpoch),
            ("$token", Guid.NewGuid()));
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
                $"tsumugi-phase35-facility-classification-{Guid.NewGuid():N}.db");
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
