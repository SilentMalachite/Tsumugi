using Tsumugi.Application.Abstractions;
using Tsumugi.Application.Claim;
using Tsumugi.Domain.Logic.Claim;
using Tsumugi.Domain.ValueObjects;

namespace Tsumugi.Application.UseCases.Claim;

/// <summary>
/// プレビュー計算の共有結果。<see cref="Result"/>が<c>null</c>のとき算定はスキップされており、
/// <see cref="Issues"/>が必ず理由を運ぶ。<see cref="PreviewHash"/>と<see cref="DetailDrafts"/>は
/// 算定成立時のみ非空。
/// </summary>
internal sealed record ClaimPreviewComputation(
    string ClaimMasterVersion,
    IReadOnlyList<ClaimPreparationIssue> Issues,
    ClaimCalculationResult? Result,
    IReadOnlyList<ClaimFinalizationDetailDraft> DetailDrafts,
    string PreviewHash);

/// <summary>
/// snapshot読取→readiness評価→算定→snapshot envelope→PreviewHash を
/// Calculate/Close両use caseで共有する内部パイプライン（Closeはこの結果を照合・確定する）。
/// </summary>
internal sealed class ClaimPreviewPipeline(
    IClaimCalculationSnapshotReader snapshotReader,
    IClaimMasterProvider masterProvider,
    IOfficeRepository officeRepository,
    IClaimBillingTokenProvider tokenProvider,
    ClaimPreparationReadiness readiness)
{
    private static readonly ClaimSnapshotValidationCodecV2 SnapshotCodec = new();

    public async Task<ClaimPreviewComputation> ComputeAsync(
        Guid officeId, ServiceMonth serviceMonth, CancellationToken ct)
    {
        var snapshot = await snapshotReader.ReadAsync(officeId, serviceMonth, ct);
        var office = await officeRepository.FindByIdAsync(officeId, ct);

        Domain.Logic.Claim.Models.ClaimMasterRelease? release = null;
        try
        {
            release = masterProvider.ResolveVersion(serviceMonth);
        }
        catch (ClaimMasterPolicyUnavailableException)
        {
            // 版が未定義の月はreadiness issue（MasterVersionUnavailable）として可視化する。
        }

        var tokens = office is null ? null : tokenProvider.Resolve(office, snapshot.Profile, serviceMonth);
        var contextResult = ClaimPreparationContextBuilder.Build(
            snapshot, office, masterVersionAvailable: release is not null);
        var readinessResult = readiness.Evaluate(contextResult.Context);
        var requestResult = ClaimCalculationRequestBuilder.Build(snapshot, serviceMonth, tokens);

        var issues = Normalize(
            contextResult.Issues
                .Concat(readinessResult.Issues)
                .Concat(requestResult.Issues));
        var claimMasterVersion = release?.Version.Value ?? "";
        if (issues.Length > 0 || requestResult.Request is not { } request || release is null)
        {
            return new ClaimPreviewComputation(claimMasterVersion, issues, null, [], "");
        }

        var masters = masterProvider.ResolveCalculationMasters(serviceMonth);

        // Task 13 (ADR 0023): 経過措置（対象月のtransition-rules行 × profileの版・R8状態・
        // 版付きoption）の不一致は算定前にフェイルクローズする。R8-06境界で旧版profileや
        // R6区分の残留による無検証の単価請求を生成しない（推測しない）。
        var transitionIssues = OfficeClaimProfileTransitionGuard.Validate(
            masters.TransitionRules, snapshot.Profile);
        if (transitionIssues.Count > 0)
        {
            return new ClaimPreviewComputation(
                claimMasterVersion, Normalize(transitionIssues), null, [], "");
        }

        var result = ClaimCalculator.Calculate(masters, request);
        var detailDrafts = BuildDetailDrafts(
            snapshot, serviceMonth, claimMasterVersion, request, result);
        var previewHash = ClaimPreviewHashing.Compute(
            officeId, serviceMonth, claimMasterVersion, result, detailDrafts);
        return new ClaimPreviewComputation(claimMasterVersion, issues, result, detailDrafts, previewHash);
    }

    private static ClaimFinalizationDetailDraft[] BuildDetailDrafts(
        ClaimCalculationSnapshot snapshot,
        ServiceMonth serviceMonth,
        string claimMasterVersion,
        ClaimCalculationRequest request,
        ClaimCalculationResult result)
    {
        var sourceByRecipient = request.Recipients.ToDictionary(source => source.RecipientId);
        return result.Details
            .Select(detail =>
            {
                var source = sourceByRecipient[detail.RecipientId];
                var claimInput = snapshot.EffectiveClaimInputs
                    .Single(input => input.RecipientId == detail.RecipientId);
                var inputEnvelope = SnapshotCodec.CreateEnvelope(
                    ClaimRecipientSnapshotWriter.WriteInputSnapshot(
                        serviceMonth, request, source, claimInput));
                var calculationEnvelope = SnapshotCodec.CreateEnvelope(
                    ClaimRecipientSnapshotWriter.WriteCalculationSnapshot(
                        serviceMonth, claimMasterVersion, detail));
                return new ClaimFinalizationDetailDraft(
                    detail.RecipientId,
                    ClaimSnapshotValidationCodecV2.SchemaVersionValue,
                    claimMasterVersion,
                    ClaimFinalizationVersions.CsvSpecificationVersion,
                    ClaimFinalizationVersions.ReportSpecificationVersion,
                    ClaimFinalizationVersions.SnapshotApplicationVersion,
                    inputEnvelope,
                    calculationEnvelope,
                    detail.TotalUnits,
                    detail.TotalCostYen,
                    detail.BenefitYen,
                    detail.BurdenYen);
            })
            .ToArray();
    }

    private static ClaimPreparationIssue[] Normalize(
        IEnumerable<ClaimPreparationIssue> issues)
        => issues
            .Distinct()
            .OrderBy(issue => issue.RecipientId.HasValue)
            .ThenBy(issue => issue.RecipientId)
            .ThenBy(issue => issue.Code)
            .ThenBy(issue => issue.FieldCode, StringComparer.Ordinal)
            .ToArray();
}
