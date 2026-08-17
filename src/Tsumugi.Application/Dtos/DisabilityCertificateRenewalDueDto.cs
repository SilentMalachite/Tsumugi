using Tsumugi.Domain.Enums;

namespace Tsumugi.Application.Dtos;

/// <summary>精神手帳の更新期日アラート1件。表示文言は UI が組み立てる。</summary>
public sealed record DisabilityCertificateRenewalDueDto(
    Guid CertificateId,
    Guid RecipientId,
    DisabilityCertificateType Type,
    string Grade,
    DateOnly NextRenewalDate,
    int RemainingDays);
