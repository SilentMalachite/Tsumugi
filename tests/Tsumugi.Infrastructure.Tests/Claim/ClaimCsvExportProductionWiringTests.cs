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

    // Phase 3-3 コードレビューで判明: 明細書「契約情報」レコード（provider:J121:05）が要求する
    // 契約支給量・契約開始年月日・事業者記入欄番号、および開始年月日（provider:J121:02:008）は
    // finalization snapshot v2 に含まれない。当月の日次記録から推測せず fail-close する。
    // snapshot が契約情報を持つようになれば、ここは生成成功の証跡へ置き換える。
    [Fact]
    public async Task Real_wiring_fails_closed_when_the_finalized_snapshot_has_no_contract_information()
    {
        await using var context = _fixture.NewContext();
        var (officeId, serviceMonth) = await SeedFinalizedBatchAsync(context, month: 7);

        var act = async () => await CreateUseCase(context).ExecuteAsync(
            officeId, serviceMonth, new ProcessingMonth(2026, 8), "tester", default);

        var exception = (await act.Should().ThrowAsync<ClaimCsvExportFailedException>()).Which;
        exception.Reason.Should().Be(nameof(CsvEncodingReason.MissingRequired));
        exception.FieldId.Should().BeOneOf(
            "provider:J121:02:008", "provider:J121:05:008",
            "provider:J121:05:009", "provider:J121:05:011");

        var history = await new ClaimCsvExportRepository(context)
            .ListByBatchAsync(await LatestBatchIdAsync(context, officeId, serviceMonth), default);
        history.Should().BeEmpty();
    }

    // NOTE(teeth): head は Cancel を含む最大 Revision。取消済みの請求から過去 revision を
    // 復活させて出力してはならない。
    [Fact]
    public async Task Real_wiring_refuses_to_export_when_the_head_revision_is_a_cancellation()
    {
        await using var context = _fixture.NewContext();
        var (officeId, serviceMonth) = await SeedFinalizedBatchAsync(context, month: 5);
        await SeedCancellationAsync(context, officeId, serviceMonth);

        var act = async () => await CreateUseCase(context).ExecuteAsync(
            officeId, serviceMonth, new ProcessingMonth(2026, 8), "tester", default);

        await act.Should().ThrowAsync<ClaimBatchNotFinalizedException>();
    }

    // NOTE(teeth): 確定時に記録した CSV 仕様版と生成時の版が食い違ったら出力しない。
    [Fact]
    public async Task Real_wiring_refuses_to_export_when_the_finalized_specification_version_differs()
    {
        await using var context = _fixture.NewContext();
        var (officeId, serviceMonth) = await SeedFinalizedBatchAsync(
            context, month: 4, csvSpecificationVersion: "r9-99");

        var act = async () => await CreateUseCase(context).ExecuteAsync(
            officeId, serviceMonth, new ProcessingMonth(2026, 8), "tester", default);

        (await act.Should().ThrowAsync<ClaimCsvExportFailedException>())
            .Which.Reason.Should().Be("CsvSpecificationVersionMismatch");
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
        new ClaimMasterCsvOfficeContextProvider(JsonClaimMasterProvider.LoadEmbedded()),
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

    private static async Task SeedCancellationAsync(
        TsumugiDbContext context, Guid officeId, ServiceMonth serviceMonth)
    {
        var head = await LatestBatchIdAsync(context, officeId, serviceMonth);
        context.Add(ClaimBatch.Cancellation(
            Guid.NewGuid(), officeId, serviceMonth, revision: 2, originId: head,
            expectedHeadBatchId: head, expectedHeadRevision: 1,
            "master-v1", "r7-10", "report-v1", "snapshot-app-v1", "operation-app-v1",
            Guid.NewGuid(), ClaimBatch.CurrentOperationPayloadSchemaVersion, new string('b', 64),
            "tester", FixedNow));
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
    }

    private static async Task<(Guid OfficeId, ServiceMonth ServiceMonth)> SeedFinalizedBatchAsync(
        TsumugiDbContext context,
        int month,
        string kanaName = "ﾂﾑｷﾞ ﾀﾛｳ",
        string csvSpecificationVersion = "r7-10")
    {
        var officeId = Guid.NewGuid();
        var serviceMonth = new ServiceMonth(2026, month);
        var batch = ClaimBatch.NewRecord(
            Guid.NewGuid(), officeId, serviceMonth,
            totalUnits: 3000, totalCostYen: 30000, totalBenefitYen: 27000, totalBurdenYen: 3000,
            "master-v1", csvSpecificationVersion, "report-v1", "snapshot-app-v1", "operation-app-v1",
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
