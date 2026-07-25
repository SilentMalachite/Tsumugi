using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using Tsumugi.Application.Abstractions;
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

    // 契約情報（サービス事業者記入欄）が確定 snapshot に入っていれば、実データから CSV が出る。
    [Fact]
    public async Task Real_wiring_generates_cp932_csv_and_appends_the_export_history()
    {
        await using var context = _fixture.NewContext();
        var (officeId, serviceMonth) = await SeedFinalizedBatchAsync(context, month: 7, withContract: true);

        var result = await CreateUseCase(context).ExecuteAsync(
            officeId, serviceMonth, new ProcessingMonth(2026, 8), "tester", default);

        result.Bytes.Should().NotBeEmpty();
        result.Sha256.Should().Be(Convert.ToHexStringLower(SHA256.HashData(result.Bytes)));
        // 共通編 1.2.1: 英字で始まる半角英数字 8 桁以内 ＋ ".CSV"。
        result.SuggestedFileName.Should().MatchRegex("^[A-Za-z][A-Za-z0-9]{0,7}\\.CSV$");
        result.SuggestedFileName.Should().StartWith("J112608");

        var text = CsvCellEncoder.Cp932.GetString(result.Bytes);
        text.Should().EndWith("\r\n");
        var lines = text.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        lines[0].Should().StartWith("1,");
        lines[^1].Should().StartWith("3,");
        // 明細書「契約情報」レコードが出ていること（契約支給量 22 / 契約開始年月日 20260401）。
        lines.Should().ContainSingle(line => line.Contains(",J121,05,", StringComparison.Ordinal))
            .Which.Should().ContainAll("22", "20260401");

        var history = await new ClaimCsvExportRepository(context)
            .ListByBatchAsync(await LatestBatchIdAsync(context, officeId, serviceMonth), default);
        history.Should().ContainSingle();
        history[0].Sha256.Should().Be(result.Sha256);
        history[0].ByteLength.Should().Be(result.Bytes.Length);
    }

    // AC3-7: 処理対象年月はサービス提供年月と独立した入力。
    [Fact]
    public async Task Real_wiring_writes_the_processing_month_independently_from_the_service_month()
    {
        await using var context = _fixture.NewContext();
        var (officeId, serviceMonth) = await SeedFinalizedBatchAsync(context, month: 9, withContract: true);

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
        var (officeId, serviceMonth) = await SeedFinalizedBatchAsync(context, month: 10, withContract: true);
        var useCase = CreateUseCase(context);

        var first = await useCase.ExecuteAsync(
            officeId, serviceMonth, new ProcessingMonth(2026, 11), "tester", default);
        var second = await useCase.ExecuteAsync(
            officeId, serviceMonth, new ProcessingMonth(2026, 11), "tester", default);

        second.Bytes.Should().Equal(first.Bytes);
    }

    // NOTE(teeth): 契約情報が未入力の確定分（Phase 3-3 より前に確定した snapshot を含む）は、
    // 当月の日次記録から推測せず fail-close する。
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
        // production と同じ経路: 未検証 raw aggregate ではなく検証済み実効 revision から生成する。
        new VerifiedClaimBatchProvider(
            new ClaimBatchRepository(context),
            new ClaimHistoryVerifier(
                new ClaimFinalizationOperationRegistry(),
                new ProductionClaimSnapshotValidationCodecRegistry())),
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
        var cancellation = ClaimBatch.Cancellation(
            Guid.NewGuid(), officeId, serviceMonth, revision: 2, originId: head,
            expectedHeadBatchId: head, expectedHeadRevision: 1,
            "master-v1", "r7-10", "report-v1", "snapshot-app-v1", "operation-app-v1",
            Guid.NewGuid(), ClaimBatch.CurrentOperationPayloadSchemaVersion, new string('b', 64),
            "tester", FixedNow);
        context.Add(Sign(cancellation, []));
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
    }

    /// <summary>
    /// 直接 seed した行に本物の確定操作 payload ハッシュを付ける。<see cref="ClaimHistoryVerifier"/> は
    /// 出力経路でこのハッシュを再構築して照合するため、プレースホルダのままでは
    /// 「改竄された履歴」として正しく拒否される。
    /// </summary>
    private static ClaimBatch Sign(ClaimBatch batch, IReadOnlyList<ClaimDetail> details) => batch with
    {
        OperationPayloadSha256 = new ClaimHistoryVerifier(
                new ClaimFinalizationOperationRegistry(),
                new ProductionClaimSnapshotValidationCodecRegistry())
            .ComputeOperationPayloadSha256(new ClaimBatchAggregate(batch, details)),
    };

    private static async Task<(Guid OfficeId, ServiceMonth ServiceMonth)> SeedFinalizedBatchAsync(
        TsumugiDbContext context,
        int month,
        string kanaName = "ﾂﾑｷﾞ ﾀﾛｳ",
        string csvSpecificationVersion = "r7-10",
        bool withContract = false)
    {
        var officeId = Guid.NewGuid();
        var serviceMonth = new ServiceMonth(2026, month);
        var batch = ClaimBatch.NewRecord(
            Guid.NewGuid(), officeId, serviceMonth,
            totalUnits: 3000, totalCostYen: 30000, totalBenefitYen: 27000, totalBurdenYen: 3000,
            "master-v1", csvSpecificationVersion, "report-v1", "snapshot-app-v1", "operation-app-v1",
            Guid.NewGuid(), ClaimBatch.CurrentOperationPayloadSchemaVersion, new string('a', 64),
            "tester", FixedNow);
        var snapshot = BuildSnapshot(serviceMonth, kanaName, withContract);
        var detail = ClaimDetail.Create(
            Guid.NewGuid(), batch.Id, snapshot.RecipientId, "claim-snapshot-v2",
            // detail の版は header と一致していなければ履歴として成立しない（ClaimHistoryVerifier）。
            "master-v1", csvSpecificationVersion, "report-v1", "snapshot-app-v1",
            // codec v2 が受け付ける最小の canonical envelope（"{}" では検証を通らない）。
            """{"schemaVersion":"claim-snapshot-v2","validationCodecId":"claim-snapshot-codec-v2"}""",
            Encoding.UTF8.GetString(ClaimFinalizationSnapshotWriter.Write(snapshot)),
            totalUnits: 3000, totalCostYen: 30000, benefitYen: 27000, burdenYen: 3000,
            "tester", FixedNow);

        context.Add(Sign(batch, [detail]));
        context.Add(detail);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        return (officeId, serviceMonth);
    }

    private static ClaimFinalizationSnapshot BuildSnapshot(
        ServiceMonth serviceMonth, string kanaName, bool withContract) => new(
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
        BurdenYen: 3000,
        ContractedProvider: withContract
            ? new ClaimFinalizationContractedProviderSnapshot(
                ContractedSupplyDays: 22,
                ContractDate: new DateOnly(2026, 4, 1),
                TerminationDate: null,
                CertificateEntryNumber: 1,
                FirstServiceDate: new DateOnly(2026, 4, 1))
            : null,
        // サービス利用日数（provider:J121:04:009）は本体報酬算定日数＋加算のみ算定日数。
        ServiceUsageDays: withContract ? 5 : null);

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
