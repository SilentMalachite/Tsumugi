using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using Tsumugi.Application.Claim;
using Tsumugi.Application.Dtos.Claim.Reports;
using Tsumugi.Application.UseCases.Claim;
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.Enums;
using Tsumugi.Domain.ValueObjects;
using Tsumugi.Infrastructure.ClaimMasters;
using Tsumugi.Infrastructure.Csv.Generation;
using Tsumugi.Infrastructure.Csv.Specifications;
using Tsumugi.Infrastructure.Csv.Writer;
using Tsumugi.Infrastructure.Persistence;

namespace Tsumugi.Infrastructure.Tests.Claim;

/// <summary>
/// CompositionRoot が組み立てる実装クラス（実 <see cref="JsonClaimMasterProvider"/>・
/// 実埋め込み CSV spec・実 SQLite リポジトリ）をそのまま通し、確定済み請求から
/// CP932 CSV が生成され出力履歴が追記されることを一気通貫で検証する。
/// </summary>
public sealed class ClaimCsvExportProductionWiringTests : IClassFixture<SqliteFixture>
{
    private static readonly DateTimeOffset FixedNow =
        new(2026, 8, 10, 12, 34, 56, TimeSpan.Zero);

    private readonly SqliteFixture _fixture;

    public ClaimCsvExportProductionWiringTests(SqliteFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Real_wiring_generates_cp932_csv_and_appends_the_export_history()
    {
        await using var context = _fixture.NewContext();
        var (officeId, serviceMonth) = await SeedFinalizedBatchAsync(context, month: 7);

        var result = await CreateUseCase(context).ExecuteAsync(
            officeId, serviceMonth, new ProcessingMonth(2026, 8), "tester", default);

        result.Bytes.Should().NotBeEmpty();
        result.Sha256.Should().Be(Convert.ToHexStringLower(SHA256.HashData(result.Bytes)));
        result.SuggestedFileName.Should().Match("kokuho_1312345678_202608_*.csv");

        var text = CsvCellEncoder.Cp932.GetString(result.Bytes);
        text.Should().EndWith("\r\n");
        text.Split("\r\n", StringSplitOptions.RemoveEmptyEntries)[0].Should().StartWith("1,");

        var history = await new ClaimCsvExportRepository(context)
            .ListByBatchAsync(await LatestBatchIdAsync(context, officeId, serviceMonth), default);
        history.Should().ContainSingle();
        history[0].Sha256.Should().Be(result.Sha256);
        history[0].ProcessingMonth.Should().Be(new ProcessingMonth(2026, 8));
        history[0].ByteLength.Should().Be(result.Bytes.Length);
    }

    // AC3-7: 独立入力の ProcessingMonth がコントロールレコードへ入り、サービス提供年月とは別に扱われる。
    [Fact]
    public async Task Real_wiring_writes_the_processing_month_independently_from_the_service_month()
    {
        await using var context = _fixture.NewContext();
        var (officeId, serviceMonth) = await SeedFinalizedBatchAsync(context, month: 9);

        var result = await CreateUseCase(context).ExecuteAsync(
            officeId, serviceMonth, new ProcessingMonth(2026, 11), "tester", default);

        var control = CsvCellEncoder.Cp932.GetString(result.Bytes).Split("\r\n")[0].Split(',');
        control[9].Should().Be("202611");
        control.Should().NotContain("202609");
    }

    [Fact]
    public async Task Real_wiring_is_byte_deterministic_for_the_same_finalized_batch()
    {
        await using var context = _fixture.NewContext();
        var (officeId, serviceMonth) = await SeedFinalizedBatchAsync(context, month: 10);
        var useCase = CreateUseCase(context);

        var first = await useCase.ExecuteAsync(
            officeId, serviceMonth, new ProcessingMonth(2026, 11), "tester", default);
        var second = await useCase.ExecuteAsync(
            officeId, serviceMonth, new ProcessingMonth(2026, 11), "tester", default);

        second.Bytes.Should().Equal(first.Bytes);
        second.Sha256.Should().Be(first.Sha256);
    }

    // 出力は「確定済みの実効 ClaimBatch」からしか作れない。
    [Fact]
    public async Task Real_wiring_fails_closed_when_no_finalized_batch_exists()
    {
        await using var context = _fixture.NewContext();

        var act = async () => await CreateUseCase(context).ExecuteAsync(
            Guid.NewGuid(), new ServiceMonth(2026, 12), new ProcessingMonth(2027, 1), "tester", default);

        await act.Should().ThrowAsync<ClaimBatchNotFinalizedException>();
    }

    [Fact]
    public async Task Real_wiring_does_not_append_history_when_generation_fails()
    {
        await using var context = _fixture.NewContext();
        // CP932 に無い文字を氏名カナへ混ぜ、encoder の fail-close 経路を通す。
        var (officeId, serviceMonth) = await SeedFinalizedBatchAsync(context, month: 6, kanaName: "🍣");

        var act = async () => await CreateUseCase(context).ExecuteAsync(
            officeId, serviceMonth, new ProcessingMonth(2026, 8), "tester", default);

        (await act.Should().ThrowAsync<ClaimCsvExportFailedException>())
            .Which.Reason.Should().Be("NonRepresentableCharacter");

        var history = await new ClaimCsvExportRepository(context)
            .ListByBatchAsync(await LatestBatchIdAsync(context, officeId, serviceMonth), default);
        history.Should().BeEmpty();
    }

    private static ExportClaimCsvUseCase CreateUseCase(TsumugiDbContext context) => new(
        new ClaimBatchRepository(context),
        JsonClaimMasterProvider.LoadEmbedded(),
        new ClaimCsvGenerator(CsvSpecificationLoader.LoadEmbedded()),
        new ClaimCsvExportRepository(context),
        new FixedTimeProvider(FixedNow));

    private static async Task<Guid> LatestBatchIdAsync(
        TsumugiDbContext context, Guid officeId, ServiceMonth serviceMonth)
    {
        var aggregates = await new ClaimBatchRepository(context)
            .ListHistoryAggregatesAsync(officeId, serviceMonth, default);
        return aggregates[^1].Header.Id;
    }

    private static async Task<(Guid OfficeId, ServiceMonth ServiceMonth)> SeedFinalizedBatchAsync(
        TsumugiDbContext context,
        int month,
        string kanaName = "ﾂﾑｷﾞ ﾀﾛｳ")
    {
        var officeId = Guid.NewGuid();
        var serviceMonth = new ServiceMonth(2026, month);
        var batch = ClaimBatch.NewRecord(
            Guid.NewGuid(), officeId, serviceMonth,
            totalUnits: 3000, totalCostYen: 30000, totalBenefitYen: 27000, totalBurdenYen: 3000,
            "master-v1", "r7-10", "report-v1", "snapshot-app-v1", "operation-app-v1",
            Guid.NewGuid(), ClaimBatch.CurrentOperationPayloadSchemaVersion, new string('a', 64),
            "tester", FixedNow);
        var snapshot = BuildSnapshot(serviceMonth, kanaName);
        var detail = ClaimDetail.Create(
            Guid.NewGuid(), batch.Id, snapshot.RecipientId, "claim-snapshot-v2",
            "master-v1", "r7-10", "report-v1", "snapshot-app-v1",
            "{}",
            Encoding.UTF8.GetString(ClaimFinalizationSnapshotWriter.Write(snapshot)),
            totalUnits: 3000, totalCostYen: 30000, benefitYen: 27000, burdenYen: 3000,
            "tester", FixedNow);

        context.Add(batch);
        context.Add(detail);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        return (officeId, serviceMonth);
    }

    private static ClaimFinalizationSnapshot BuildSnapshot(ServiceMonth serviceMonth, string kanaName) => new(
        RecipientId: Guid.Parse("11111111-1111-1111-1111-111111111111"),
        ServiceMonth: serviceMonth,
        ClaimMasterVersion: "master-v1",
        CsvSpecificationVersion: "r7-10",
        ReportSpecificationVersion: "report-v1",
        Office: new ClaimFinalizationOfficeSnapshot(
            "1312345678", "つむぎ事業所", RegionGrade.Grade6, "1000001", "東京都千代田区1-1", "0312345678", "管理者 紡"),
        Recipient: new ClaimFinalizationRecipientSnapshot("紡 太郎", kanaName),
        Certificate: new ClaimFinalizationCertificateSnapshot(
            "1234567890", "131016", null, 9300, null, null),
        ClaimInput: new ClaimFinalizationClaimInputSnapshot(null, null, null, null, null, null, null),
        DailyRecords:
        [
            Day(serviceMonth, 1),
            Day(serviceMonth, 2),
            Day(serviceMonth, 3),
            Day(serviceMonth, 6),
            Day(serviceMonth, 7),
        ],
        IntensiveSupportEpisode: null,
        ClaimLines:
        [
            new ClaimFinalizationClaimLineSnapshot(ClaimDetailLineKind.Basic, "462980", 566, 5, 28300),
            new ClaimFinalizationClaimLineSnapshot(ClaimDetailLineKind.Addition, "466010", 34, 5, 1700),
        ],
        BilledDays: 5,
        TotalUnits: 3000,
        TotalCostYen: 30000,
        BenefitYen: 27000,
        BurdenYen: 3000);

    private static ClaimFinalizationDailyRecordSnapshot Day(ServiceMonth month, int day) => new(
        new DateOnly(month.Year, month.Month, day),
        Attendance.Present,
        MealProvided: false,
        Transport: TransportKind.None,
        AbsenceResponseNote: null,
        ServiceStartTime: new TimeOnly(9, 30),
        ServiceEndTime: new TimeOnly(15, 30),
        SpecialVisitSupportMinutes: null,
        OffsiteSupportApplied: false,
        MedicalCoordinationType: null,
        TrialUseSupportType: null,
        RegionalCollaborationApplied: false,
        IntensiveSupportApplied: false,
        EmergencyAdmissionApplied: false,
        RecipientConfirmation: true);

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
