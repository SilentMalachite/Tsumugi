using Tsumugi.Application.Abstractions;
using Tsumugi.Application.Dtos;
using Tsumugi.Domain.Logic;
using Tsumugi.Domain.ValueObjects;

namespace Tsumugi.Application.UseCases.Certificate;

/// <summary>
/// 実効受給者証の障害種別と、手帳種類ごとの現行1件を照合する。
/// 表示日本語は組み立てない。
/// </summary>
public sealed class QueryDisabilityConsistencyUseCase(
    ICertificateRepository certificates,
    IDisabilityCertificateRepository handbooks)
{
    public async Task<IReadOnlyList<DisabilityConsistencyWarningDto>> ExecuteAsync(
        Guid recipientId, DateOnly asOf, CancellationToken ct)
    {
        var certificate = await certificates.FindEffectiveAsync(recipientId, asOf, ct);
        var disabilities = certificate?.Disabilities ?? DisabilityCategories.None;
        var currentTypes = (await handbooks.ListByRecipientAsync(recipientId, ct))
            .GroupBy(handbook => handbook.Type)
            .Select(group => group
                .OrderByDescending(handbook => handbook.IssuedDate)
                .ThenByDescending(handbook => handbook.CreatedAt)
                .First().Type)
            .ToHashSet();

        return DisabilityConsistencyPolicy.Detect(disabilities, currentTypes)
            .Select(finding => new DisabilityConsistencyWarningDto(
                recipientId, finding.Type, finding.Direction))
            .ToArray();
    }
}
