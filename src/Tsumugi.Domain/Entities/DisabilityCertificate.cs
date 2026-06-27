using Tsumugi.Domain.Enums;

namespace Tsumugi.Domain.Entities;

/// <summary>
/// 障害者手帳。利用者と 1:N。厳密追記（append-only）。
/// 等級改定や更新時は新レコードを追加し、最新を「現行」として扱う。
/// 等級表記は手帳種類により形式が異なるため文字列で保持する
/// （身体: "1級"、療育: "A1" や "1度"、精神: "2級" 等）。
/// </summary>
public sealed record DisabilityCertificate : Entity
{
    public required Guid RecipientId { get; init; }
    public required DisabilityCertificateType Type { get; init; }
    /// <summary>等級・度数の表記。種類ごとに体系が異なるため文字列で持つ。</summary>
    public required string Grade { get; init; }
    /// <summary>身体障害者手帳の「種別」(1種/2種)。他種類では null。</summary>
    public string? Subtype { get; init; }
    public required DateOnly IssuedDate { get; init; }
    /// <summary>次回更新予定日 (精神は 2 年ごと等)。期限の無い種類では null。</summary>
    public DateOnly? NextRenewalDate { get; init; }
    /// <summary>発行自治体 (都道府県名・政令市名)。</summary>
    public required string IssuingAuthority { get; init; }
    /// <summary>手帳番号 (任意・自治体運用に依存)。</summary>
    public string? CertificateNumber { get; init; }
    /// <summary>備考 (障害名・部位 等)。</summary>
    public string? Notes { get; init; }

    public static DisabilityCertificate Create(
        Guid id,
        Guid recipientId,
        DisabilityCertificateType type,
        string grade,
        DateOnly issuedDate,
        string issuingAuthority,
        string createdBy,
        DateTimeOffset createdAt,
        Guid concurrencyToken,
        string? subtype = null,
        DateOnly? nextRenewalDate = null,
        string? certificateNumber = null,
        string? notes = null)
    {
        if (string.IsNullOrWhiteSpace(grade))
            throw new ArgumentException("等級は必須です。", nameof(grade));
        if (string.IsNullOrWhiteSpace(issuingAuthority))
            throw new ArgumentException("発行自治体は必須です。", nameof(issuingAuthority));
        if (nextRenewalDate is { } nr && nr < issuedDate)
            throw new ArgumentException("次回更新予定日は交付日以後である必要があります。", nameof(nextRenewalDate));

        return new()
        {
            Id = id,
            RecipientId = recipientId,
            Type = type,
            Grade = grade,
            Subtype = subtype,
            IssuedDate = issuedDate,
            NextRenewalDate = nextRenewalDate,
            IssuingAuthority = issuingAuthority,
            CertificateNumber = certificateNumber,
            Notes = notes,
            CreatedBy = createdBy,
            CreatedAt = createdAt,
            ConcurrencyToken = concurrencyToken,
        };
    }
}
