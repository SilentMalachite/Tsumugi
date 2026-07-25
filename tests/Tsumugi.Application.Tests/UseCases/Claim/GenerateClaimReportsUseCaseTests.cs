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
    public void Constructor_depends_only_on_VerifiedClaimBatchProvider_and_IClaimReportGenerator()
    {
        // Task 13の必須制約（spec §8）: GenerateClaimReportsUseCaseはIOfficeRepository /
        // IRecipientRepository / ICertificateRepository / IDailyRecordRepositoryを一切参照しない。
        // さらにIClaimBatchRepository（未検証raw aggregateを返すport）も直接持たない。
        // NOTE(teeth): 引数をIClaimBatchRepositoryへ戻すとここがRED。
        var constructor = typeof(GenerateClaimReportsUseCase).GetConstructors().Should().ContainSingle().Subject;
        var parameterTypes = constructor.GetParameters().Select(p => p.ParameterType).ToArray();

        parameterTypes.Should().BeEquivalentTo(
            [typeof(VerifiedClaimBatchProvider), typeof(IClaimReportGenerator)]);
        parameterTypes.Should().NotContain(typeof(IClaimBatchRepository));
    }

    [Fact]
    public async Task GenerateServiceProvisionRecordAsync_parses_v2_payload_and_invokes_generator_with_mapped_dto()
    {
        var snapshot = BuildSnapshot(Kit.RecipientId, Kit.Month);
        var header = Kit.Batch(revision: 1, kind: RecordKind.New);
        var detail = BuildDetail(header, snapshot);
        var generator = new FakeClaimReportGenerator();
        var useCase = new GenerateClaimReportsUseCase(
            Kit.VerifiedProvider(new ClaimBatchAggregate(header, [detail])), generator);

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
        var detail = BuildDetail(header, snapshot);
        var useCase = new GenerateClaimReportsUseCase(
            Kit.VerifiedProvider(new ClaimBatchAggregate(header, [detail])),
            new FakeClaimReportGenerator());

        await FluentActions.Invoking(() => useCase.GenerateServiceProvisionRecordAsync(
                Kit.OfficeId, Kit.Month, SecondRecipientId, CancellationToken.None))
            .Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task GenerateClaimInvoiceAsync_refuses_when_the_head_revision_is_cancelled()
    {
        // revision1(New)→revision2(Correct)→revision3(Cancel)。取消済みの月に請求書を出さない。
        // spec §9「Cancel状態やrevision不在の場合はInvalidOperationException（fail-closed）」。
        // NOTE(teeth): Cancelを除外してから最大revisionを採る実装に戻すと、revision2の請求書が
        // 出てしまうためここがRED。
        var headerR1 = Kit.Batch(revision: 1, kind: RecordKind.New);
        var detailR1 = BuildDetail(headerR1, BuildSnapshot(Kit.RecipientId, Kit.Month));

        var headerR2 = Kit.Batch(revision: 2, kind: RecordKind.Correct, originId: headerR1.Id,
            expectedHeadBatchId: headerR1.Id, expectedHeadRevision: 1);
        var detailR2 = BuildDetail(headerR2, BuildSnapshot(Kit.RecipientId, Kit.Month));

        var headerR3 = Kit.Batch(revision: 3, kind: RecordKind.Cancel, originId: headerR1.Id,
            expectedHeadBatchId: headerR2.Id, expectedHeadRevision: 2);

        var generator = new FakeClaimReportGenerator();
        var useCase = new GenerateClaimReportsUseCase(
            Kit.VerifiedProvider(
                new ClaimBatchAggregate(headerR1, [detailR1]),
                new ClaimBatchAggregate(headerR2, [detailR2]),
                new ClaimBatchAggregate(headerR3, [])),
            generator);

        await FluentActions.Invoking(() => useCase.GenerateClaimInvoiceAsync(
                Kit.OfficeId, Kit.Month, CancellationToken.None))
            .Should().ThrowAsync<InvalidOperationException>();
        generator.LastClaimInvoiceDto.Should().BeNull();
    }

    [Fact]
    public async Task GenerateClaimInvoiceAsync_selects_the_latest_correction_revision()
    {
        // revision1(New, 合計1400)→revision2(Correct, 受給者2名で合計2800)。head=revision2を選んだ
        // 直接証拠としてdto.TotalUnitが2800になる（revision1を誤って選べば1400で判別できる）。
        var headerR1 = Kit.Batch(revision: 1, kind: RecordKind.New);
        var detailR1 = BuildDetail(headerR1, BuildSnapshot(Kit.RecipientId, Kit.Month));

        var headerR2 = Kit.Batch(revision: 2, kind: RecordKind.Correct, originId: headerR1.Id,
            expectedHeadBatchId: headerR1.Id, expectedHeadRevision: 1,
            totalUnits: 2800, totalCostYen: 28_000, totalBenefitYen: 25_200, totalBurdenYen: 2_800);
        var detailR2a = BuildDetail(headerR2, BuildSnapshot(Kit.RecipientId, Kit.Month));
        var detailR2b = BuildDetail(headerR2, BuildSnapshot(SecondRecipientId, Kit.Month));

        var generator = new FakeClaimReportGenerator();
        var useCase = new GenerateClaimReportsUseCase(
            Kit.VerifiedProvider(
                new ClaimBatchAggregate(headerR1, [detailR1]),
                new ClaimBatchAggregate(headerR2, [detailR2a, detailR2b])),
            generator);

        var bytes = await useCase.GenerateClaimInvoiceAsync(Kit.OfficeId, Kit.Month, CancellationToken.None);

        bytes.Should().Equal(generator.ClaimInvoiceResult);
        generator.LastClaimInvoiceDto.Should().NotBeNull();
        var dto = generator.LastClaimInvoiceDto!;
        dto.TotalUnit.Should().Be(2800);
        dto.TotalCostYen.Should().Be(28_000);
        dto.TotalBenefitYen.Should().Be(25_200);
        dto.TotalBurdenYen.Should().Be(2_800);
        dto.Office.OfficeNumber.Should().Be("1310000001");
    }

    [Fact]
    public async Task GenerateClaimInvoiceAsync_rejects_a_history_whose_persisted_snapshot_was_tampered_with()
    {
        // 確定操作payloadのSHA-256はsnapshot JSONを含めて再構築されるため、DBのsnapshotを書き換えると
        // 照合に失敗する。未検証raw aggregateを直接読む実装に戻すとここがRED。
        var header = Kit.Batch(revision: 1, kind: RecordKind.New);
        var detail = BuildDetail(header, BuildSnapshot(Kit.RecipientId, Kit.Month));
        var signed = Kit.Sign(new ClaimBatchAggregate(header, [detail]));
        var tampered = new ClaimBatchAggregate(
            signed.Header,
            [
                detail with
                {
                    CalculationSnapshotJson = detail.CalculationSnapshotJson.Replace(
                        "山田太郎", "鈴木花子", StringComparison.Ordinal),
                },
            ]);
        var useCase = new GenerateClaimReportsUseCase(
            new VerifiedClaimBatchProvider(new Kit.FakeBatchRepository([tampered]), Kit.Verifier()),
            new FakeClaimReportGenerator());

        await FluentActions.Invoking(() => useCase.GenerateClaimInvoiceAsync(
                Kit.OfficeId, Kit.Month, CancellationToken.None))
            .Should().ThrowAsync<ClaimFinalizationException>()
            .Where(exception => exception.Code == ClaimErrorCode.InvalidHistory);
    }

    [Fact]
    public async Task GenerateClaimInvoiceAsync_throws_when_no_finalized_revision_exists()
    {
        var useCase = new GenerateClaimReportsUseCase(
            Kit.VerifiedProvider(), new FakeClaimReportGenerator());

        await FluentActions.Invoking(() => useCase.GenerateClaimInvoiceAsync(
                Kit.OfficeId, Kit.Month, CancellationToken.None))
            .Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task GenerateClaimInvoiceAsync_throws_when_all_revisions_are_cancelled()
    {
        // revision 1 は New でなければならない（ClaimBatchPolicy）ため、Cancel だけの履歴は
        // 「実効請求なし」ではなく「壊れた履歴」として落ちる。
        var headerR1 = Kit.Batch(revision: 1, kind: RecordKind.Cancel);
        var useCase = new GenerateClaimReportsUseCase(
            Kit.VerifiedProvider(new ClaimBatchAggregate(headerR1, [])),
            new FakeClaimReportGenerator());

        await FluentActions.Invoking(() => useCase.GenerateClaimInvoiceAsync(
                Kit.OfficeId, Kit.Month, CancellationToken.None))
            .Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task GenerateClaimInvoiceAsync_throws_when_latest_revision_has_no_details()
    {
        // detail 0 件の New は合計 0 でなければ履歴として成立しない。
        var header = Kit.Batch(revision: 1, kind: RecordKind.New,
            totalUnits: 0, totalCostYen: 0, totalBenefitYen: 0, totalBurdenYen: 0);
        var useCase = new GenerateClaimReportsUseCase(
            Kit.VerifiedProvider(new ClaimBatchAggregate(header, [])),
            new FakeClaimReportGenerator());

        await FluentActions.Invoking(() => useCase.GenerateClaimInvoiceAsync(
                Kit.OfficeId, Kit.Month, CancellationToken.None))
            .Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task GenerateClaimStatementAsync_aggregates_all_recipients_in_latest_revision()
    {
        // header の集計値は Σdetail と一致していなければ履歴として成立しない
        // （ClaimHistoryVerifier が Σdetail＝header を検証するため、両者が食い違うDBは検証で落ちる）。
        // 「snapshot 内部の TotalUnits(14_000) ではなく detail 行の値(1400) を使っている」ことは
        // SubtotalUnit の検証で担保する。
        var snapshot1 = BuildSnapshot(Kit.RecipientId, Kit.Month, kanjiName: "山田太郎", serviceCode: "610000");
        var snapshot2 = BuildSnapshot(SecondRecipientId, Kit.Month, kanjiName: "鈴木花子", serviceCode: "620000");
        var header = Kit.Batch(
            revision: 1, kind: RecordKind.New,
            totalUnits: 2800, totalCostYen: 28_000, totalBenefitYen: 25_200, totalBurdenYen: 2_800);
        var detail1 = BuildDetail(header, snapshot1, totalUnits: 1400, totalCostYen: 14_000,
            benefitYen: 12_600, burdenYen: 1_400);
        var detail2 = BuildDetail(header, snapshot2, totalUnits: 1400, totalCostYen: 14_000,
            benefitYen: 12_600, burdenYen: 1_400);
        var generator = new FakeClaimReportGenerator();
        var useCase = new GenerateClaimReportsUseCase(
            Kit.VerifiedProvider(new ClaimBatchAggregate(header, [detail1, detail2])), generator);

        var bytes = await useCase.GenerateClaimStatementAsync(Kit.OfficeId, Kit.Month, CancellationToken.None);

        bytes.Should().Equal(generator.ClaimStatementResult);
        generator.LastClaimStatementDto.Should().NotBeNull();
        var dto = generator.LastClaimStatementDto!;
        dto.Recipients.Should().HaveCount(2);
        dto.Recipients.Select(r => r.Recipient.KanjiName).Should().BeEquivalentTo(["山田太郎", "鈴木花子"]);
        dto.Recipients.Single(r => r.Recipient.KanjiName == "山田太郎").Lines.Should().ContainSingle()
            .Which.ServiceCode.Should().Be("610000");
        dto.Recipients.Single(r => r.Recipient.KanjiName == "山田太郎").SubtotalUnit.Should().Be(1400);
        // header値の素通し（snapshot内部のTotalUnits=14_000ではない）ことの確認。
        dto.TotalUnit.Should().Be(2800);
        dto.TotalCostYen.Should().Be(28_000);
        dto.TotalBenefitYen.Should().Be(25_200);
        dto.TotalBurdenYen.Should().Be(2_800);
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

    /// <summary>
    /// header と版・作成者・batchId が整合した detail を作る（<see cref="ClaimHistoryVerifier"/> は
    /// header と detail の版一致まで検証するため、独立した版文字列では履歴として成立しない）。
    /// </summary>
    private static ClaimDetail BuildDetail(
        ClaimBatch header,
        ClaimFinalizationSnapshot snapshot,
        int totalUnits = 1400,
        int totalCostYen = 14_000,
        int benefitYen = 12_600,
        int burdenYen = 1_400)
        => ClaimDetail.Create(
            Guid.NewGuid(),
            header.Id,
            snapshot.RecipientId,
            snapshotSchemaVersion: ClaimSnapshotValidationCodecV2.SchemaVersionValue,
            claimMasterVersion: header.ClaimMasterVersion,
            csvSpecificationVersion: header.CsvSpecificationVersion,
            reportSpecificationVersion: header.ReportSpecificationVersion,
            snapshotApplicationVersion: header.SnapshotApplicationVersion,
            inputSnapshotJson: Kit.MinimalEnvelopeJson,
            calculationSnapshotJson: Encoding.UTF8.GetString(ClaimFinalizationSnapshotWriter.Write(snapshot)),
            totalUnits: totalUnits,
            totalCostYen: totalCostYen,
            benefitYen: benefitYen,
            burdenYen: burdenYen,
            createdBy: header.CreatedBy,
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
