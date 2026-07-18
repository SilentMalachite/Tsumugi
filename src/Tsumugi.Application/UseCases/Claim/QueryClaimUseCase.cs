using Tsumugi.Application.Abstractions;
using Tsumugi.Application.Dtos;

namespace Tsumugi.Application.UseCases.Claim;

/// <summary>確定請求履歴（header＋details）をrevision順のDTOへ写像する読み取り専用use case。</summary>
public sealed class QueryClaimUseCase(IClaimBatchRepository batchRepository)
{
    public async Task<IReadOnlyList<ClaimBatchHistoryDto>> ExecuteAsync(
        QueryClaimRequest request, CancellationToken ct)
    {
        ClaimPreparationGuard.Validate(request?.OfficeId, request?.ServiceMonth);

        var aggregates = await batchRepository.ListHistoryAggregatesAsync(
            request!.OfficeId, request.ServiceMonth, ct);
        return aggregates
            .OrderBy(aggregate => aggregate.Header.Revision)
            .Select(aggregate => new ClaimBatchHistoryDto(
                aggregate.Header.Id,
                aggregate.Header.Revision,
                aggregate.Header.Kind,
                aggregate.Header.OriginId,
                aggregate.Header.FinalizationOperationId,
                aggregate.Header.ClaimMasterVersion,
                aggregate.Header.TotalUnits,
                aggregate.Header.TotalCostYen,
                aggregate.Header.TotalBenefitYen,
                aggregate.Header.TotalBurdenYen,
                aggregate.Header.CreatedAt,
                aggregate.Header.CreatedBy,
                aggregate.Details
                    .OrderBy(detail => detail.RecipientId)
                    .Select(detail => new ClaimBatchDetailDto(
                        detail.RecipientId,
                        detail.TotalUnits,
                        detail.TotalCostYen,
                        detail.BenefitYen,
                        detail.BurdenYen))
                    .ToArray()))
            .ToArray();
    }
}
