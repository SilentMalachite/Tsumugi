using Tsumugi.Domain.Enums;
using Tsumugi.Domain.ValueObjects;

namespace Tsumugi.Domain.Logic;

/// <summary>障害種別の不整合の方向（証のみ／手帳のみ）。</summary>
public enum DisabilityConsistencyDirection
{
    /// <summary>受給者証にフラグがあるが対応手帳が無い。</summary>
    CertificateOnly = 1,

    /// <summary>対応手帳があるが受給者証にフラグが無い。</summary>
    HandbookOnly = 2,
}

/// <summary>障害種別整合の検出結果（警告文は上位層で組み立てる）。</summary>
public sealed record DisabilityConsistencyFinding(
    DisabilityCertificateType Type,
    DisabilityConsistencyDirection Direction);

/// <summary>受給者証の障害種別と現行手帳種類の双方向整合検出（純粋関数）。</summary>
public static class DisabilityConsistencyPolicy
{
    /// <summary>
    /// Physical / Intellectual / Mental のみをこの順で双方向検査する。
    /// Intractable は常に無視する。警告文は生成しない。
    /// </summary>
    public static IReadOnlyList<DisabilityConsistencyFinding> Detect(
        DisabilityCategories disabilities,
        IReadOnlySet<DisabilityCertificateType> currentTypes)
    {
        ArgumentNullException.ThrowIfNull(currentTypes);

        var findings = new List<DisabilityConsistencyFinding>();

        Check(findings, DisabilityCertificateType.Physical, disabilities.Physical, currentTypes);
        Check(findings, DisabilityCertificateType.Intellectual, disabilities.Intellectual, currentTypes);
        Check(findings, DisabilityCertificateType.Mental, disabilities.Mental, currentTypes);

        return findings;
    }

    private static void Check(
        List<DisabilityConsistencyFinding> findings,
        DisabilityCertificateType type,
        bool onCertificate,
        IReadOnlySet<DisabilityCertificateType> currentTypes)
    {
        var hasHandbook = currentTypes.Contains(type);
        if (onCertificate && !hasHandbook)
            findings.Add(new DisabilityConsistencyFinding(type, DisabilityConsistencyDirection.CertificateOnly));
        else if (!onCertificate && hasHandbook)
            findings.Add(new DisabilityConsistencyFinding(type, DisabilityConsistencyDirection.HandbookOnly));
    }
}
