namespace Tsumugi.Domain.Entities;

/// <summary>
/// 受給者証「サービス事業者記入欄」の 1 行（1 事業所との契約）。
/// 1 受給者証は複数の事業所と契約しうるため、Certificate と 1:N の関係を持つ。
/// 自身の事業所（Tsumugi 利用事業所）に加え、計画相談以外の他事業所との契約状況も記録できる。
/// </summary>
public sealed record ContractedProvider : Entity
{
    /// <summary>
    /// 障害者自立支援法の施行日。初回サービス提供日の下限（事業所編の設定方法および
    /// CSV 仕様の <c>lowerBound=20060401</c> と一致する）。
    /// </summary>
    private static readonly DateOnly SelfSupportActStartDate = new(2006, 4, 1);

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

    /// <summary>
    /// 初回サービス提供日（J121:02:008 開始年月日）。事業所編の設定方法により、就労継続支援B型では
    /// 「自立支援法へ移行した平成18年4月1日以降における最初にサービス提供した日」を設定する。
    /// <b>契約支給量の変更があっても変わらず、当月でなく過去月の日付になりうる</b>ため、
    /// 契約日との前後関係は制約にしない（例: 9/1契約→9/15契約変更でも初回提供日は9/3）。
    /// </summary>
    public DateOnly? FirstServiceDate { get; init; }

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
        int? certificateEntryNumber = null,
        DateOnly? firstServiceDate = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(contractedSupplyDays);
        if (certificateEntryNumber is < 0 or > 99)
            throw new ArgumentOutOfRangeException(
                nameof(certificateEntryNumber), "証書記入欄番号は0から99の範囲で指定してください。");
        if (firstServiceDate is { } first && first < SelfSupportActStartDate)
            throw new ArgumentOutOfRangeException(
                nameof(firstServiceDate),
                "初回サービス提供日は2006年4月1日以降の日付を指定してください。");
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
            FirstServiceDate = firstServiceDate,
            CreatedBy = createdBy,
            CreatedAt = createdAt,
            ConcurrencyToken = concurrencyToken,
        };
    }
}
