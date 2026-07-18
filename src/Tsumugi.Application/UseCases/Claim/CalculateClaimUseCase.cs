using Tsumugi.Application.Abstractions;
using Tsumugi.Application.Claim;
using Tsumugi.Application.Dtos;
using Tsumugi.Domain.ValueObjects;

namespace Tsumugi.Application.UseCases.Claim;

/// <summary>
/// 算定プレビュー。readinessが不成立なら算定をスキップしIssues付き<see cref="ClaimPreviewDto"/>を返す。
/// 成立時は<see cref="Domain.Logic.Claim.ClaimCalculator"/>の結果とPreviewHash（確定時と同一手順の
/// canonical SHA-256）をDTO化する。
/// </summary>
public sealed class CalculateClaimUseCase(
    IClaimCalculationSnapshotReader snapshotReader,
    IClaimMasterProvider masterProvider,
    IOfficeRepository officeRepository,
    IClaimBillingTokenProvider tokenProvider,
    ClaimPreparationReadiness readiness)
{
    private readonly ClaimPreviewPipeline _pipeline = new(
        snapshotReader, masterProvider, officeRepository, tokenProvider, readiness);

    public async Task<ClaimPreviewDto> ExecuteAsync(CalculateClaimRequest request, CancellationToken ct)
    {
        ClaimPreparationGuard.Validate(request?.OfficeId, request?.ServiceMonth);

        var computation = await _pipeline.ComputeAsync(request!.OfficeId, request.ServiceMonth, ct);
        if (computation.Result is not { } result)
        {
            return ClaimPreviewDto.NotReady(
                request.ServiceMonth, computation.ClaimMasterVersion, computation.Issues);
        }

        return new ClaimPreviewDto(
            request.ServiceMonth,
            computation.ClaimMasterVersion,
            computation.PreviewHash,
            result.Details
                .Select(detail => new ClaimPreviewDetailDto(
                    detail.RecipientId,
                    detail.ServiceCode,
                    detail.BilledDays,
                    detail.TotalUnits,
                    detail.TotalCostYen,
                    detail.BenefitYen,
                    detail.BurdenYen))
                .ToArray(),
            result.TotalUnits,
            result.TotalCostYen,
            result.TotalBenefitYen,
            result.TotalBurdenYen,
            Issues: [],
            IsReady: true);
    }
}

/// <summary>Task 9 use case群共通の入力ガード（値の妥当性以前の呼び出しミスは例外で拒否）。</summary>
internal static class ClaimPreparationGuard
{
    public static void Validate(Guid? officeId, ServiceMonth? serviceMonth)
    {
        if (officeId is null || serviceMonth is null)
            throw new ArgumentNullException(nameof(officeId), "リクエストが未指定です。");
        if (officeId == Guid.Empty)
            throw new ArgumentException("OfficeIdが未指定です。", nameof(officeId));
        _ = serviceMonth.Value.ToInt();
    }

    public static void ValidateActor(string actor)
    {
        if (string.IsNullOrWhiteSpace(actor))
            throw new ArgumentException("操作者が未指定です。", nameof(actor));
    }
}
