using System.Collections.Immutable;
using Tsumugi.Domain.ValueObjects;

namespace Tsumugi.Domain.Logic.Claim.Models;

/// <summary>サービス提供年月へinclusiveに適用する請求マスタ版と出典束。</summary>
public sealed record ClaimMasterRelease
{
    public ClaimMasterVersion Version { get; }
    public ServiceMonth EffectiveFrom { get; }
    public ServiceMonth? EffectiveTo { get; }
    public IReadOnlyList<string> SourceDocumentIds { get; }

    public ClaimMasterRelease(
        ClaimMasterVersion version,
        ServiceMonth effectiveFrom,
        ServiceMonth? effectiveTo,
        IReadOnlyList<string> sourceDocumentIds)
    {
        if (string.IsNullOrWhiteSpace(version.Value) || version.Value.Length > ClaimMasterVersion.MaxLength)
            throw new ArgumentException("請求マスタ版が不正です。", nameof(version));

        ValidateMonth(effectiveFrom, nameof(effectiveFrom), version);
        if (effectiveTo is { } end)
        {
            ValidateMonth(end, nameof(effectiveTo), version);
            if (end < effectiveFrom)
            {
                throw new ArgumentException(
                    $"請求マスタ版 '{version}' の開始月 {effectiveFrom} より終了月 {end} が前です。",
                    nameof(effectiveTo));
            }
        }

        ArgumentNullException.ThrowIfNull(sourceDocumentIds);
        var sourceIds = sourceDocumentIds.ToImmutableArray();
        if (sourceIds.IsEmpty)
            throw new ArgumentException($"請求マスタ版 '{version}' には出典document IDが必要です。", nameof(sourceDocumentIds));
        if (sourceIds.Any(string.IsNullOrWhiteSpace))
            throw new ArgumentException($"請求マスタ版 '{version}' の出典document IDを空白にできません。", nameof(sourceDocumentIds));

        var duplicateId = sourceIds
            .GroupBy(id => id, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1)?.Key;
        if (duplicateId is not null)
        {
            throw new ArgumentException(
                $"請求マスタ版 '{version}' の出典document ID '{duplicateId}' が重複しています。",
                nameof(sourceDocumentIds));
        }

        Version = version;
        EffectiveFrom = effectiveFrom;
        EffectiveTo = effectiveTo;
        SourceDocumentIds = sourceIds;
    }

    private static void ValidateMonth(
        ServiceMonth month,
        string parameterName,
        ClaimMasterVersion version)
    {
        if (month.Year is < 1900 or > 2200 || month.Month is < 1 or > 12)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                month,
                $"請求マスタ版 '{version}' の適用月 {month} が不正です。");
        }
    }
}
