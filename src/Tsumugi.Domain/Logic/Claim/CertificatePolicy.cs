using Tsumugi.Domain.Entities;

namespace Tsumugi.Domain.Logic.Claim;

/// <summary>受給者証の追記型revision chainを検証し、請求日に実効となる版を解決する。</summary>
public static class CertificatePolicy
{
    public static Certificate? EffectiveVersion(
        IEnumerable<Certificate> certificates,
        DateOnly serviceDate)
    {
        ArgumentNullException.ThrowIfNull(certificates);

        var effective = new List<Certificate>();
        foreach (var rootHistory in certificates.GroupBy(certificate => certificate.RootCertificateId))
        {
            var ordered = rootHistory.OrderBy(certificate => certificate.Revision).ToArray();
            ValidateHistory(rootHistory.Key, ordered);

            var head = ordered[^1];
            if (head.Validity.Contains(serviceDate))
                effective.Add(head);
        }

        return effective.Count switch
        {
            0 => null,
            1 => effective[0],
            _ => throw new InvalidOperationException("請求日に有効な受給者証rootが複数あります。"),
        };
    }

    private static void ValidateHistory(Guid rootCertificateId, Certificate[] ordered)
    {
        if (rootCertificateId == Guid.Empty)
            throw new InvalidOperationException("受給者証root IDが空です。");

        for (var index = 0; index < ordered.Length; index++)
        {
            var certificate = ordered[index];
            var expectedRevision = index + 1;
            if (certificate.Revision != expectedRevision)
                throw new InvalidOperationException(
                    $"受給者証Revision {expectedRevision} が欠落しているか重複しています。");

            if (index == 0)
            {
                if (certificate.Id != rootCertificateId
                    || certificate.ExpectedHeadCertificateId is not null)
                    throw new InvalidOperationException(
                        "受給者証Revision 1はroot自身で、expected headを持たない必要があります。");
                continue;
            }

            if (certificate.ExpectedHeadCertificateId != ordered[index - 1].Id)
                throw new InvalidOperationException(
                    "受給者証expected headは直前Revisionを指す必要があります。");
        }
    }
}
