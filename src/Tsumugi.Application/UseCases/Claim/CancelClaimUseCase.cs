using Tsumugi.Application.Abstractions;
using Tsumugi.Application.Claim;
using Tsumugi.Application.Dtos;
using Tsumugi.Domain.Enums;

namespace Tsumugi.Application.UseCases.Claim;

/// <summary>
/// 請求取下げ。有効なhead（未取消）必須。<see cref="RecordKind.Cancel"/>のdraft
/// （合計0・明細なし・snapshot版はheadから引き継ぎ）を確定storeへ渡す。
/// head不在・取消済みは<see cref="ClaimFinalizationException"/>
/// （<see cref="ClaimErrorCode.InvalidHistory"/>）。
/// </summary>
public sealed class CancelClaimUseCase(
    IClaimBatchRepository batchRepository,
    IClaimFinalizationStore finalizationStore)
{
    public async Task<ClaimBatchRevisionDto> ExecuteAsync(
        CancelClaimRequest request, string actor, CancellationToken ct)
    {
        ClaimPreparationGuard.Validate(request?.OfficeId, request?.ServiceMonth);
        ClaimPreparationGuard.ValidateActor(actor);

        var head = await ClaimBatchHeadResolver.ResolveAsync(
            batchRepository, request!.OfficeId, request.ServiceMonth, ct);
        if (head is null || head.Kind == RecordKind.Cancel)
            throw new ClaimFinalizationException(ClaimErrorCode.InvalidHistory);

        var draft = new ClaimFinalizationDraft(
            Guid.NewGuid(),
            RecordKind.Cancel,
            request.OfficeId,
            request.ServiceMonth,
            head.OriginId ?? head.Id,
            new ClaimExpectedHead(head.Id, head.Revision),
            actor,
            ClaimFinalizationVersions.OperationApplicationVersion,
            head.ClaimMasterVersion,
            head.CsvSpecificationVersion,
            head.ReportSpecificationVersion,
            head.SnapshotApplicationVersion,
            TotalUnits: 0,
            TotalCostYen: 0,
            TotalBenefitYen: 0,
            TotalBurdenYen: 0,
            Details: []);

        var committed = await finalizationStore.CommitAsync(draft, ct);
        return new ClaimBatchRevisionDto(committed.BatchId, committed.Revision, committed.IsReplay);
    }
}
