using Tsumugi.Application.Abstractions;
using Tsumugi.Application.Claim;
using Tsumugi.Application.Dtos;
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.Enums;
using Tsumugi.Domain.Logic.Claim;

namespace Tsumugi.Application.UseCases.Claim;

/// <summary>
/// 請求確定。プレビューと同一手順で再算定し、PreviewHashが<c>ExpectedPreviewHash</c>と一致した
/// 場合だけ確定draft（履歴が空なら<see cref="RecordKind.New"/>、head存在時は
/// <see cref="RecordKind.Correct"/>）を構築して<see cref="IClaimFinalizationStore"/>へ渡す。
/// 不一致・readiness不成立は<see cref="ClaimFinalizationException"/>
/// （<see cref="ClaimErrorCode.InvalidOperationPayload"/>）で拒否する。
/// </summary>
public sealed class CloseClaimUseCase(
    IClaimCalculationSnapshotReader snapshotReader,
    IClaimMasterProvider masterProvider,
    IOfficeRepository officeRepository,
    IClaimBillingTokenProvider tokenProvider,
    ClaimPreparationReadiness readiness,
    IClaimBatchRepository batchRepository,
    IClaimFinalizationStore finalizationStore)
{
    private readonly ClaimPreviewPipeline _pipeline = new(
        snapshotReader, masterProvider, officeRepository, tokenProvider, readiness);

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
            computation.DetailDrafts);

        var committed = await finalizationStore.CommitAsync(draft, ct);
        return new ClaimBatchRevisionDto(committed.BatchId, committed.Revision, committed.IsReplay);
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
