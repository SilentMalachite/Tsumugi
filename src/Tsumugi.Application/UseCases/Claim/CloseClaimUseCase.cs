using Tsumugi.Application.Abstractions;
using Tsumugi.Application.Claim;
using Tsumugi.Application.Dtos;
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.Enums;
using Tsumugi.Domain.Logic.Claim;
using Tsumugi.Domain.ValueObjects;

namespace Tsumugi.Application.UseCases.Claim;

/// <summary>
/// 請求確定。プレビューと同一手順で再算定し、PreviewHashが<c>ExpectedPreviewHash</c>と一致した
/// 場合だけ確定draft（履歴が空なら<see cref="RecordKind.New"/>、head存在時は
/// <see cref="RecordKind.Correct"/>）を構築して<see cref="IClaimFinalizationStore"/>へ渡す。
/// 不一致・readiness不成立は<see cref="ClaimFinalizationException"/>
/// （<see cref="ClaimErrorCode.InvalidOperationPayload"/>）で拒否する。
/// 各受給者の<c>CalculationSnapshotEnvelope</c>は、プレビューと共有する軽量計算snapshotではなく、
/// <see cref="IOperationLocalSnapshotReader"/>が確定時点のOffice/Recipient/Certificate/
/// DailyRecord/IntensiveSupportEpisode/ClaimInputを集約したv2 finalization payload
/// （spec §6, <see cref="ClaimFinalizationSnapshot"/>）に置き換える（3帳票が
/// <see cref="IClaimBatchRepository"/>経由のsnapshotだけから決定論的に描画できるようにするため）。
/// </summary>
public sealed class CloseClaimUseCase(
    IClaimCalculationSnapshotReader snapshotReader,
    IClaimMasterProvider masterProvider,
    IOfficeRepository officeRepository,
    IClaimBillingTokenProvider tokenProvider,
    ClaimPreparationReadiness readiness,
    IClaimBatchRepository batchRepository,
    IClaimFinalizationStore finalizationStore,
    IOperationLocalSnapshotReader operationSnapshotReader)
{
    private readonly ClaimPreviewPipeline _pipeline = new(
        snapshotReader, masterProvider, officeRepository, tokenProvider, readiness);

    // ClaimPreviewPipelineと同じ直接インスタンス化パターン（Task 2で確立）。単一固定codecの
    // envelope化にDI registry解決は不要。
    private static readonly ClaimSnapshotValidationCodecV2 SnapshotCodec = new();

    public async Task<ClaimBatchRevisionDto> ExecuteAsync(
        CloseClaimRequest request, string actor, CancellationToken ct)
    {
        ClaimPreparationGuard.Validate(request?.OfficeId, request?.ServiceMonth);
        ClaimPreparationGuard.ValidateActor(actor);
        if (string.IsNullOrWhiteSpace(request!.ExpectedPreviewHash))
            throw new ClaimFinalizationException(ClaimErrorCode.InvalidOperationPayload);

        var computation = await _pipeline.ComputeAsync(request.OfficeId, request.ServiceMonth, ct);
        if (computation.Result is not { } result)
            throw new ClaimFinalizationException(ClaimErrorCode.InvalidOperationPayload);

        // 再算定hashの照合はdraft構築より先。プレビュー後に入力が変わった確定要求をここで拒否する。
        if (!string.Equals(computation.PreviewHash, request.ExpectedPreviewHash, StringComparison.Ordinal))
            throw new ClaimFinalizationException(ClaimErrorCode.InvalidOperationPayload);

        var head = await ClaimBatchHeadResolver.ResolveAsync(
            batchRepository, request.OfficeId, request.ServiceMonth, ct);
        var detailDrafts = await BuildFinalizationDetailDraftsAsync(
            request.OfficeId, request.ServiceMonth, computation, result, ct);
        var draft = new ClaimFinalizationDraft(
            Guid.NewGuid(),
            head is null ? RecordKind.New : RecordKind.Correct,
            request.OfficeId,
            request.ServiceMonth,
            head is null ? null : head.OriginId ?? head.Id,
            head is null ? null : new ClaimExpectedHead(head.Id, head.Revision),
            actor,
            ClaimFinalizationVersions.OperationApplicationVersion,
            computation.ClaimMasterVersion,
            ClaimFinalizationVersions.CsvSpecificationVersion,
            ClaimFinalizationVersions.ReportSpecificationVersion,
            ClaimFinalizationVersions.SnapshotApplicationVersion,
            result.TotalUnits,
            result.TotalCostYen,
            result.TotalBenefitYen,
            result.TotalBurdenYen,
            detailDrafts);

        var committed = await finalizationStore.CommitAsync(draft, ct);
        return new ClaimBatchRevisionDto(committed.BatchId, committed.Revision, committed.IsReplay);
    }

    /// <summary>
    /// プレビューと共有する<see cref="ClaimPreviewComputation.DetailDrafts"/>のうち
    /// <c>CalculationSnapshotEnvelope</c>だけをv2 finalization payloadへ置き換える。
    /// <c>InputSnapshotEnvelope</c>・受給者ごとの集計値（TotalUnits等）はプレビューと確定で
    /// 共有する既存契約のまま変更しない。
    /// </summary>
    private async Task<IReadOnlyList<ClaimFinalizationDetailDraft>> BuildFinalizationDetailDraftsAsync(
        Guid officeId,
        ServiceMonth serviceMonth,
        ClaimPreviewComputation computation,
        ClaimCalculationResult result,
        CancellationToken ct)
    {
        var resultByRecipient = result.Details.ToDictionary(detail => detail.RecipientId);
        var updated = new List<ClaimFinalizationDetailDraft>(computation.DetailDrafts.Count);
        foreach (var draft in computation.DetailDrafts)
        {
            var calculationResult = resultByRecipient[draft.RecipientId];
            var finalizationSnapshot = await operationSnapshotReader.ReadAsync(
                officeId,
                draft.RecipientId,
                serviceMonth,
                calculationResult,
                computation.ClaimMasterVersion,
                ClaimFinalizationVersions.CsvSpecificationVersion,
                ClaimFinalizationVersions.ReportSpecificationVersion,
                ct);
            var finalizationBytes = ClaimFinalizationSnapshotWriter.Write(finalizationSnapshot);
            var finalizationEnvelope = SnapshotCodec.CreateEnvelope(finalizationBytes);
            updated.Add(draft with { CalculationSnapshotEnvelope = finalizationEnvelope });
        }
        return updated;
    }
}

/// <summary>履歴からheadを解決する。履歴不正はInvalidHistoryへ写像しフェイルクローズする。</summary>
internal static class ClaimBatchHeadResolver
{
    public static async Task<ClaimBatch?> ResolveAsync(
        IClaimBatchRepository batchRepository,
        Guid officeId,
        Domain.ValueObjects.ServiceMonth serviceMonth,
        CancellationToken ct)
    {
        var aggregates = await batchRepository.ListHistoryAggregatesAsync(officeId, serviceMonth, ct);
        try
        {
            return ClaimBatchPolicy.Head(aggregates.Select(aggregate => aggregate.Header).ToArray());
        }
        catch (InvalidOperationException exception)
        {
            throw new ClaimFinalizationException(ClaimErrorCode.InvalidHistory, path: null, exception);
        }
    }
}
