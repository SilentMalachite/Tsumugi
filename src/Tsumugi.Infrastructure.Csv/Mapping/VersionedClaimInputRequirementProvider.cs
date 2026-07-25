using Tsumugi.Application.Abstractions;
using Tsumugi.Application.Claim;

namespace Tsumugi.Infrastructure.Csv.Mapping;

/// <summary>
/// 仕様版ごとの readiness 要件。版レジストリに登録された全版分を保持し、版を指定して引く。
/// </summary>
/// <remarks>
/// 未登録の版は fail-close する。「requirement が引けないから現行版で代用する」と、
/// 新版で必須になった項目を確認せずに通してしまう。
/// </remarks>
public sealed class VersionedClaimInputRequirementProvider : IClaimInputRequirementProvider
{
    private readonly IReadOnlyDictionary<string, ClaimInputRequirementProvider> _byVersion;

    internal VersionedClaimInputRequirementProvider(
        IReadOnlyDictionary<string, ClaimInputRequirementProvider> byVersion)
    {
        ArgumentNullException.ThrowIfNull(byVersion);
        if (byVersion.Count == 0)
        {
            throw new InvalidDataException("readiness 要件は少なくとも 1 版分が必要です。");
        }

        _byVersion = byVersion;
    }

    /// <summary>登録されている仕様版。</summary>
    public IReadOnlyCollection<string> Versions => (IReadOnlyCollection<string>)_byVersion.Keys;

    public IReadOnlyList<ClaimInputRequirement> GetRequirements(string specificationVersion)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(specificationVersion);
        return _byVersion.TryGetValue(specificationVersion, out var provider)
            ? provider.GetRequirements()
            : throw new InvalidOperationException(
                $"CSV仕様版 '{specificationVersion}' の readiness 要件が登録されていません。"
                + $"（登録済み: {string.Join(", ", _byVersion.Keys)}）");
    }
}
