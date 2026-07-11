namespace Tsumugi.Domain.Entities;

/// <summary>
/// 受給者証「サービス事業者記入欄」の 1 行（1 事業所との契約）。
/// 1 受給者証は複数の事業所と契約しうるため、Certificate と 1:N の関係を持つ。
/// 自身の事業所（Tsumugi 利用事業所）に加え、計画相談以外の他事業所との契約状況も記録できる。
/// </summary>
public sealed record ContractedProvider : Entity
{
    public required Guid CertificateId { get; init; }
    /// <summary>事業所番号（10桁）。</summary>
    public required string ProviderNumber { get; init; }
    public required string ProviderName { get; init; }
    /// <summary>サービス種別（例: 就労継続支援B型、生活介護 等）。</summary>
    public required string ServiceCategory { get; init; }
    /// <summary>当該支給決定における支給量（日/月）。</summary>
    public required int ContractedSupplyDays { get; init; }
    /// <summary>契約日。</summary>
    public required DateOnly ContractDate { get; init; }
    /// <summary>契約終了日（null の場合は継続中）。</summary>
    public DateOnly? TerminationDate { get; init; }
    /// <summary>備考（自由記述）。</summary>
    public string? Notes { get; init; }
    /// <summary>サービス提供事業者記入欄の番号（J121:05:011、0～99）。</summary>
    public int? CertificateEntryNumber { get; init; }

    public static ContractedProvider Create(
        Guid id,
        Guid certificateId,
        string providerNumber,
        string providerName,
        string serviceCategory,
        int contractedSupplyDays,
        DateOnly contractDate,
        string createdBy,
        DateTimeOffset createdAt,
        Guid concurrencyToken,
        DateOnly? terminationDate = null,
        string? notes = null,
        int? certificateEntryNumber = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(contractedSupplyDays);
        if (certificateEntryNumber is < 0 or > 99)
            throw new ArgumentOutOfRangeException(
                nameof(certificateEntryNumber), "証書記入欄番号は0から99の範囲で指定してください。");
        return new()
        {
            Id = id,
            CertificateId = certificateId,
            ProviderNumber = providerNumber,
            ProviderName = providerName,
            ServiceCategory = serviceCategory,
            ContractedSupplyDays = contractedSupplyDays,
            ContractDate = contractDate,
            TerminationDate = terminationDate,
            Notes = notes,
            CertificateEntryNumber = certificateEntryNumber,
            CreatedBy = createdBy,
            CreatedAt = createdAt,
            ConcurrencyToken = concurrencyToken,
        };
    }
}
