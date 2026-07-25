using System.Text;
using FluentAssertions;
using Tsumugi.Application.Abstractions;
using Tsumugi.Application.Claim;
using Tsumugi.Application.UseCases.Claim;
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.Enums;
using Tsumugi.Domain.ValueObjects;
using Xunit;
using Kit = Tsumugi.Application.Tests.UseCases.Claim.ClaimPreparationTestKit;

namespace Tsumugi.Application.Tests.Claim;

/// <summary>
/// 国保連へ渡す成果物（請求CSV・3帳票）の入口。<see cref="IClaimBatchRepository"/> が返す未検証
/// raw aggregate を検証し、実効 revision だけを <see cref="VerifiedClaimBatch"/> として渡す。
/// </summary>
public sealed class VerifiedClaimBatchProviderTests
{
    [Fact]
    public async Task An_empty_history_has_no_effective_revision()
    {
        var provider = Kit.VerifiedProvider();

        var result = await provider.FindEffectiveAsync(Kit.OfficeId, Kit.Month, CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task The_effective_revision_is_the_latest_correction()
    {
        var headerR1 = Kit.Batch(revision: 1, kind: RecordKind.New);
        var headerR2 = Kit.Batch(revision: 2, kind: RecordKind.Correct, originId: headerR1.Id,
            expectedHeadBatchId: headerR1.Id, expectedHeadRevision: 1);
        var provider = Kit.VerifiedProvider(
            new ClaimBatchAggregate(headerR1, [Detail(headerR1)]),
            new ClaimBatchAggregate(headerR2, [Detail(headerR2)]));

        var result = await provider.FindEffectiveAsync(Kit.OfficeId, Kit.Month, CancellationToken.None);

        result.Should().NotBeNull();
        result!.Header.Revision.Should().Be(2);
        result.Details.Should().ContainSingle();
    }

    // NOTE(teeth): Cancel を除外してから最大 revision を採る実装に戻すと、取消済みの請求が
    // revision 1 から復活してここが RED になる。
    [Fact]
    public async Task A_cancelled_head_has_no_effective_revision()
    {
        var headerR1 = Kit.Batch(revision: 1, kind: RecordKind.New);
        var headerR2 = Kit.Batch(revision: 2, kind: RecordKind.Cancel, originId: headerR1.Id,
            expectedHeadBatchId: headerR1.Id, expectedHeadRevision: 1);
        var provider = Kit.VerifiedProvider(
            new ClaimBatchAggregate(headerR1, [Detail(headerR1)]),
            new ClaimBatchAggregate(headerR2, []));

        var result = await provider.FindEffectiveAsync(Kit.OfficeId, Kit.Month, CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task A_broken_revision_sequence_fails_closed()
    {
        // revision 1 が欠けた履歴（ClaimBatchPolicy 違反）。
        var header = Kit.Batch(revision: 2, kind: RecordKind.New);
        var provider = Kit.VerifiedProvider(new ClaimBatchAggregate(header, [Detail(header)]));

        await FluentActions.Invoking(() => provider.FindEffectiveAsync(
                Kit.OfficeId, Kit.Month, CancellationToken.None))
            .Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task A_header_whose_totals_disagree_with_its_details_fails_closed()
    {
        var header = Kit.Batch(revision: 1, kind: RecordKind.New);
        var detail = Detail(header) with { TotalUnits = header.TotalUnits - 1 };
        var provider = Kit.VerifiedProvider(new ClaimBatchAggregate(header, [detail]));

        await FluentActions.Invoking(() => provider.FindEffectiveAsync(
                Kit.OfficeId, Kit.Month, CancellationToken.None))
            .Should().ThrowAsync<ClaimFinalizationException>()
            .Where(exception => exception.Code == ClaimErrorCode.InvalidHistory);
    }

    [Fact]
    public async Task A_detail_whose_specification_version_differs_from_the_header_fails_closed()
    {
        var header = Kit.Batch(revision: 1, kind: RecordKind.New);
        var detail = Detail(header) with { CsvSpecificationVersion = "r9-99" };
        var provider = Kit.VerifiedProvider(new ClaimBatchAggregate(header, [detail]));

        await FluentActions.Invoking(() => provider.FindEffectiveAsync(
                Kit.OfficeId, Kit.Month, CancellationToken.None))
            .Should().ThrowAsync<ClaimFinalizationException>()
            .Where(exception => exception.Code == ClaimErrorCode.InvalidHistory);
    }

    [Fact]
    public async Task A_persisted_snapshot_that_is_not_a_canonical_envelope_fails_closed()
    {
        var header = Kit.Batch(revision: 1, kind: RecordKind.New);
        var detail = Detail(header) with { CalculationSnapshotJson = "{}" };
        var provider = new VerifiedClaimBatchProvider(
            new Kit.FakeBatchRepository([new ClaimBatchAggregate(header, [detail])]), Kit.Verifier());

        await FluentActions.Invoking(() => provider.FindEffectiveAsync(
                Kit.OfficeId, Kit.Month, CancellationToken.None))
            .Should().ThrowAsync<ClaimFinalizationException>()
            .Where(exception => exception.Code == ClaimErrorCode.InvalidSnapshotEnvelope);
    }

    [Fact]
    public async Task A_history_whose_operation_payload_hash_does_not_match_fails_closed()
    {
        // 署名し直さない（Kit.Batch のプレースホルダ 0×64 のまま）履歴は、確定操作 payload の
        // 再構築ハッシュと一致しないため拒否される。
        var header = Kit.Batch(revision: 1, kind: RecordKind.New);
        var provider = new VerifiedClaimBatchProvider(
            new Kit.FakeBatchRepository([new ClaimBatchAggregate(header, [Detail(header)])]),
            Kit.Verifier());

        await FluentActions.Invoking(() => provider.FindEffectiveAsync(
                Kit.OfficeId, Kit.Month, CancellationToken.None))
            .Should().ThrowAsync<ClaimFinalizationException>()
            .Where(exception => exception.Code == ClaimErrorCode.InvalidHistory);
    }

    // NOTE(teeth): raw aggregate から VerifiedClaimBatch を組み立てられるようにすると（public な
    // コンストラクタや factory を足すと）検証の迂回路ができるためここが RED になる。
    [Fact]
    public void A_verified_batch_cannot_be_constructed_from_outside_the_application_assembly()
    {
        typeof(VerifiedClaimBatch).GetConstructors().Should().BeEmpty();
        typeof(VerifiedClaimBatch).GetMethods()
            .Where(method => method.IsStatic && method.IsPublic)
            .Should().BeEmpty();
    }

    // NOTE(teeth): どちらかの consumer が IClaimBatchRepository を直接受け取る形に戻ると RED。
    [Theory]
    [InlineData(typeof(ExportClaimCsvUseCase))]
    [InlineData(typeof(GenerateClaimReportsUseCase))]
    public void Artifact_generating_use_cases_do_not_take_the_unverified_repository(Type useCaseType)
    {
        ArgumentNullException.ThrowIfNull(useCaseType);
        var parameterTypes = useCaseType.GetConstructors().Should().ContainSingle().Subject
            .GetParameters().Select(parameter => parameter.ParameterType).ToArray();

        parameterTypes.Should().Contain(typeof(VerifiedClaimBatchProvider));
        parameterTypes.Should().NotContain(typeof(IClaimBatchRepository));
    }

    // NOTE(teeth): 処理対象年月に適用される CSV 仕様版が無ければ、推測で現行版を使わず出力しない
    // （ADR 0039）。版が解決できない月を出力に使わせないための入口の歯。
    [Fact]
    public async Task Exporting_for_a_processing_month_without_an_applicable_specification_fails_closed()
    {
        var header = Kit.Batch(revision: 1, kind: RecordKind.New);
        var unavailable = new ProcessingMonth(2020, 1);
        var versions = new Kit.FakeCsvSpecificationVersions(header.CsvSpecificationVersion)
        {
            UnavailableMonth = unavailable,
        };
        var useCase = new ExportClaimCsvUseCase(
            Kit.VerifiedProvider(new ClaimBatchAggregate(header, [Detail(header)])),
            new Kit.FixedCsvOfficeContextProvider(),
            versions,
            new Kit.ThrowingCsvGenerator(),
            new Kit.NoOpCsvExportRepository(),
            TimeProvider.System);

        await FluentActions.Invoking(() => useCase.ExecuteAsync(
                Kit.OfficeId, Kit.Month, unavailable, "tester", CancellationToken.None))
            .Should().ThrowAsync<ClaimCsvExportFailedException>()
            .Where(exception => exception.Reason == "CsvSpecificationVersionUnavailable");
    }

    private static ClaimDetail Detail(ClaimBatch header) => ClaimDetail.Create(
        Guid.NewGuid(),
        header.Id,
        Kit.RecipientId,
        snapshotSchemaVersion: ClaimSnapshotValidationCodecV2.SchemaVersionValue,
        claimMasterVersion: header.ClaimMasterVersion,
        csvSpecificationVersion: header.CsvSpecificationVersion,
        reportSpecificationVersion: header.ReportSpecificationVersion,
        snapshotApplicationVersion: header.SnapshotApplicationVersion,
        inputSnapshotJson: Kit.MinimalEnvelopeJson,
        calculationSnapshotJson: Encoding.UTF8.GetString(
            ClaimFinalizationSnapshotWriter.Write(Kit.FinalizationSnapshot())),
        totalUnits: header.TotalUnits,
        totalCostYen: header.TotalCostYen,
        benefitYen: header.TotalBenefitYen,
        burdenYen: header.TotalBurdenYen,
        createdBy: header.CreatedBy,
        createdAt: Kit.Now);
}
