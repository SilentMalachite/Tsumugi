using System.Text;
using FluentAssertions;
using Tsumugi.Application.Abstractions;
using Tsumugi.Application.Claim;
using Tsumugi.Application.Dtos.Claim.Reports;
using Tsumugi.Application.UseCases.Claim;
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.Enums;
using Tsumugi.Domain.ValueObjects;
using Xunit;
using Kit = Tsumugi.Application.Tests.UseCases.Claim.ClaimPreparationTestKit;

namespace Tsumugi.Application.Tests.UseCases.Claim;

/// <summary>
/// Task 13: GenerateClaimReportsUseCaseは<see cref="IClaimBatchRepository"/>のみを参照し、
/// v2 finalization snapshot（<see cref="ClaimFinalizationSnapshot"/>）をparseして3帳票DTOへ写像、
/// <see cref="IClaimReportGenerator"/>へ委譲する（spec §9）。Moqはこのプロジェクトの依存に無いため、
/// 既存のClaimPreparationTestKit流儀（手書きFake）に合わせる。
/// </summary>
public sealed class GenerateClaimReportsUseCaseTests
{
    private static readonly Guid SecondRecipientId = Guid.Parse("44444444-4444-4444-4444-444444444444");

    [Fact]
    public void Constructor_depends_only_on_IClaimBatchRepository_and_IClaimReportGenerator()
    {
        // Task 13の必須制約（spec §8）: GenerateClaimReportsUseCaseはIOfficeRepository /
        // IRecipientRepository / ICertificateRepository / IDailyRecordRepositoryを一切参照しない。
        // コンストラクタ引数の型集合を直接検証することで、将来の実装がこれらを混入させたら
        // 即座に検知できるようにする。
        var constructor = typeof(GenerateClaimReportsUseCase).GetConstructors().Should().ContainSingle().Subject;
        var parameterTypes = constructor.GetParameters().Select(p => p.ParameterType).ToArray();

        parameterTypes.Should().BeEquivalentTo([typeof(IClaimBatchRepository), typeof(IClaimReportGenerator)]);
    }

    [Fact]
    public async Task GenerateServiceProvisionRecordAsync_parses_v2_payload_and_invokes_generator_with_mapped_dto()
    {
        var snapshot = BuildSnapshot(Kit.RecipientId, Kit.Month);
        var header = Kit.Batch(revision: 1, kind: RecordKind.New);
        var detail = BuildDetail(header.Id, snapshot);
        var repository = new Kit.FakeBatchRepository([new ClaimBatchAggregate(header, [detail])]);
        var generator = new FakeClaimReportGenerator();
        var useCase = new GenerateClaimReportsUseCase(repository, generator);

        var bytes = await useCase.GenerateServiceProvisionRecordAsync(
            Kit.OfficeId, Kit.Month, Kit.RecipientId, CancellationToken.None);

        bytes.Should().Equal(generator.ServiceProvisionRecordResult);
        generator.LastServiceProvisionRecordDto.Should().NotBeNull();
        var dto = generator.LastServiceProvisionRecordDto!;
        dto.Recipient.KanjiName.Should().Be("山田太郎");
        dto.Office.OfficeNumber.Should().Be("1310000001");
        dto.Certificate.CertificateNumber.Should().Be("certificate-no-1");
        dto.YearMonth.Should().Be(new YearMonth(Kit.Month.Year, Kit.Month.Month));
        dto.Days.Should().ContainSingle();
        dto.Days[0].ServiceStartTime.Should().Be(new TimeOnly(9, 0));
        dto.Days[0].ServiceDate.Should().Be(new DateOnly(Kit.Month.Year, Kit.Month.Month, 1));
        dto.IntensiveSupport.Should().BeNull();
        dto.SpecVersion.ClaimMasterVersion.Should().Be("master-v1");
    }

    [Fact]
    public async Task GenerateServiceProvisionRecordAsync_throws_when_recipientId_not_found_in_latest_revision()
    {
        var snapshot = BuildSnapshot(Kit.RecipientId, Kit.Month);
        var header = Kit.Batch(revision: 1, kind: RecordKind.New);
        var detail = BuildDetail(header.Id, snapshot);
        var repository = new Kit.FakeBatchRepository([new ClaimBatchAggregate(header, [detail])]);
        var useCase = new GenerateClaimReportsUseCase(repository, new FakeClaimReportGenerator());

        await FluentActions.Invoking(() => useCase.GenerateServiceProvisionRecordAsync(
                Kit.OfficeId, Kit.Month, SecondRecipientId, CancellationToken.None))
            .Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task GenerateClaimInvoiceAsync_selects_latest_non_cancel_revision()
    {
        // revision1(New)→revision2(Correct)→revision3(Cancel)。Cancelは合計0が強制されるため、
        // dto.TotalUnitがrevision2の値と一致することは「Cancelを飛ばしてrevision2を選んだ」ことの
        // 直接証拠になる（revision3を誤って選べば0、revision1を誤って選べば1000になり判別できる）。
        var snapshotR1 = BuildSnapshot(Kit.RecipientId, Kit.Month);
        var headerR1 = Kit.Batch(revision: 1, kind: RecordKind.New, totalUnits: 1000, totalCostYen: 10_000,
            totalBenefitYen: 9_000, totalBurdenYen: 1_000);
        var detailR1 = BuildDetail(headerR1.Id, snapshotR1);

        var snapshotR2 = BuildSnapshot(Kit.RecipientId, Kit.Month);
        var headerR2 = Kit.Batch(revision: 2, kind: RecordKind.Correct, originId: headerR1.Id,
            expectedHeadBatchId: headerR1.Id, expectedHeadRevision: 1,
            totalUnits: 2000, totalCostYen: 20_000, totalBenefitYen: 18_000, totalBurdenYen: 2_000);
        var detailR2 = BuildDetail(headerR2.Id, snapshotR2);

        var headerR3 = Kit.Batch(revision: 3, kind: RecordKind.Cancel, originId: headerR1.Id,
            expectedHeadBatchId: headerR2.Id, expectedHeadRevision: 2);

        var repository = new Kit.FakeBatchRepository([
            new ClaimBatchAggregate(headerR1, [detailR1]),
            new ClaimBatchAggregate(headerR2, [detailR2]),
            new ClaimBatchAggregate(headerR3, []),
        ]);
        var generator = new FakeClaimReportGenerator();
        var useCase = new GenerateClaimReportsUseCase(repository, generator);

        var bytes = await useCase.GenerateClaimInvoiceAsync(Kit.OfficeId, Kit.Month, CancellationToken.None);

        bytes.Should().Equal(generator.ClaimInvoiceResult);
        generator.LastClaimInvoiceDto.Should().NotBeNull();
        var dto = generator.LastClaimInvoiceDto!;
        dto.TotalUnit.Should().Be(2000);
        dto.TotalCostYen.Should().Be(20_000);
        dto.TotalBenefitYen.Should().Be(18_000);
        dto.TotalBurdenYen.Should().Be(2_000);
        dto.Office.OfficeNumber.Should().Be("1310000001");
    }

    [Fact]
    public async Task GenerateClaimInvoiceAsync_throws_when_no_finalized_revision_exists()
    {
        var repository = new Kit.FakeBatchRepository([]);
        var useCase = new GenerateClaimReportsUseCase(repository, new FakeClaimReportGenerator());

        await FluentActions.Invoking(() => useCase.GenerateClaimInvoiceAsync(
                Kit.OfficeId, Kit.Month, CancellationToken.None))
            .Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task GenerateClaimInvoiceAsync_throws_when_all_revisions_are_cancelled()
    {
        var headerR1 = Kit.Batch(revision: 1, kind: RecordKind.Cancel);
        var repository = new Kit.FakeBatchRepository([new ClaimBatchAggregate(headerR1, [])]);
        var useCase = new GenerateClaimReportsUseCase(repository, new FakeClaimReportGenerator());

        await FluentActions.Invoking(() => useCase.GenerateClaimInvoiceAsync(
                Kit.OfficeId, Kit.Month, CancellationToken.None))
            .Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task GenerateClaimInvoiceAsync_throws_when_latest_revision_has_no_details()
    {
        var header = Kit.Batch(revision: 1, kind: RecordKind.New);
        var repository = new Kit.FakeBatchRepository([new ClaimBatchAggregate(header, [])]);
        var useCase = new GenerateClaimReportsUseCase(repository, new FakeClaimReportGenerator());

        await FluentActions.Invoking(() => useCase.GenerateClaimInvoiceAsync(
                Kit.OfficeId, Kit.Month, CancellationToken.None))
            .Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task GenerateClaimStatementAsync_aggregates_all_recipients_in_latest_revision()
    {
        // headerの集計値(999...)は各detailの合計(1400+1400=2800等)とは意図的に異なる値にしてあり、
        // dtoがheader値をそのまま素通ししたこと（Σdetailを再計算していないこと）を検証する。
        var snapshot1 = BuildSnapshot(Kit.RecipientId, Kit.Month, kanjiName: "山田太郎", serviceCode: "610000");
        var snapshot2 = BuildSnapshot(SecondRecipientId, Kit.Month, kanjiName: "鈴木花子", serviceCode: "620000");
        var header = Kit.Batch(
            revision: 1, kind: RecordKind.New,
            totalUnits: 9990, totalCostYen: 99_900, totalBenefitYen: 89_910, totalBurdenYen: 9_990);
        var detail1 = BuildDetail(header.Id, snapshot1, totalUnits: 1400, totalCostYen: 14_000,
            benefitYen: 12_600, burdenYen: 1_400);
        var detail2 = BuildDetail(header.Id, snapshot2, totalUnits: 1400, totalCostYen: 14_000,
            benefitYen: 12_600, burdenYen: 1_400);
        var repository = new Kit.FakeBatchRepository([new ClaimBatchAggregate(header, [detail1, detail2])]);
        var generator = new FakeClaimReportGenerator();
        var useCase = new GenerateClaimReportsUseCase(repository, generator);

        var bytes = await useCase.GenerateClaimStatementAsync(Kit.OfficeId, Kit.Month, CancellationToken.None);

        bytes.Should().Equal(generator.ClaimStatementResult);
        generator.LastClaimStatementDto.Should().NotBeNull();
        var dto = generator.LastClaimStatementDto!;
        dto.Recipients.Should().HaveCount(2);
        dto.Recipients.Select(r => r.Recipient.KanjiName).Should().BeEquivalentTo(["山田太郎", "鈴木花子"]);
        dto.Recipients.Single(r => r.Recipient.KanjiName == "山田太郎").Lines.Should().ContainSingle()
            .Which.ServiceCode.Should().Be("610000");
        dto.Recipients.Single(r => r.Recipient.KanjiName == "山田太郎").SubtotalUnit.Should().Be(1400);
        // header値の素通し（Σdetailの再計算ではない）ことの確認。
        dto.TotalUnit.Should().Be(9990);
        dto.TotalCostYen.Should().Be(99_900);
        dto.TotalBenefitYen.Should().Be(89_910);
        dto.TotalBurdenYen.Should().Be(9_990);
    }

    private static ClaimFinalizationSnapshot BuildSnapshot(
        Guid recipientId,
        ServiceMonth serviceMonth,
        string kanjiName = "山田太郎",
        string kanaName = "ヤマダタロウ",
        string serviceCode = "610000")
        => new(
            recipientId,
            serviceMonth,
            "master-v1",
            "csv-spec-v1",
            "report-spec-v1",
            new ClaimFinalizationOfficeSnapshot(
                "1310000001", "テスト事業所", RegionGrade.Grade2,
                "100-0001", "東京都千代田区1-1", "03-0000-0000", "施設長 テスト"),
            new ClaimFinalizationRecipientSnapshot(kanjiName, kanaName),
            new ClaimFinalizationCertificateSnapshot("certificate-no-1", "131016", null, 37_200, null, null),
            new ClaimFinalizationClaimInputSnapshot(null, null, null, null, null, null, null),
            [
                new ClaimFinalizationDailyRecordSnapshot(
                    new DateOnly(serviceMonth.Year, serviceMonth.Month, 1),
                    Attendance.Present,
                    true,
                    TransportKind.Round,
                    null,
                    new TimeOnly(9, 0),
                    new TimeOnly(16, 0),
                    null,
                    false,
                    null,
                    null,
                    false,
                    false,
                    false,
                    true),
            ],
            null,
            [
                new ClaimFinalizationClaimLineSnapshot(ClaimDetailLineKind.Basic, serviceCode, 700, 20, 14_000),
            ],
            BilledDays: 20,
            TotalUnits: 14_000,
            TotalCostYen: 14_000,
            BenefitYen: 12_600,
            BurdenYen: 1_400);

    private static ClaimDetail BuildDetail(
        Guid claimBatchId,
        ClaimFinalizationSnapshot snapshot,
        int totalUnits = 1400,
        int totalCostYen = 14_000,
        int benefitYen = 12_600,
        int burdenYen = 1_400)
        => ClaimDetail.Create(
            Guid.NewGuid(),
            claimBatchId,
            snapshot.RecipientId,
            snapshotSchemaVersion: ClaimSnapshotValidationCodecV2.SchemaVersionValue,
            claimMasterVersion: snapshot.ClaimMasterVersion,
            csvSpecificationVersion: snapshot.CsvSpecificationVersion,
            reportSpecificationVersion: snapshot.ReportSpecificationVersion,
            snapshotApplicationVersion: "snapshot-app-v1",
            inputSnapshotJson: "{}",
            calculationSnapshotJson: Encoding.UTF8.GetString(ClaimFinalizationSnapshotWriter.Write(snapshot)),
            totalUnits: totalUnits,
            totalCostYen: totalCostYen,
            benefitYen: benefitYen,
            burdenYen: burdenYen,
            createdBy: "tester",
            createdAt: Kit.Now);

    /// <summary>
    /// このプロジェクトにMoqの依存が無いため、既存Fake流儀（<see cref="ClaimPreparationTestKit"/>）に
    /// 合わせて渡されたDTOを捕捉する手書きFake。
    /// </summary>
    private sealed class FakeClaimReportGenerator : IClaimReportGenerator
    {
        public byte[] ServiceProvisionRecordResult { get; } = [1, 2, 3];
        public byte[] ClaimInvoiceResult { get; } = [4, 5, 6];
        public byte[] ClaimStatementResult { get; } = [7, 8, 9];

        public ServiceProvisionRecordDto? LastServiceProvisionRecordDto { get; private set; }
        public ClaimInvoiceDto? LastClaimInvoiceDto { get; private set; }
        public ClaimStatementDto? LastClaimStatementDto { get; private set; }

        public byte[] GenerateServiceProvisionRecord(ServiceProvisionRecordDto dto)
        {
            LastServiceProvisionRecordDto = dto;
            return ServiceProvisionRecordResult;
        }

        public byte[] GenerateClaimInvoice(ClaimInvoiceDto dto)
        {
            LastClaimInvoiceDto = dto;
            return ClaimInvoiceResult;
        }

        public byte[] GenerateClaimStatement(ClaimStatementDto dto)
        {
            LastClaimStatementDto = dto;
            return ClaimStatementResult;
        }
    }
}
