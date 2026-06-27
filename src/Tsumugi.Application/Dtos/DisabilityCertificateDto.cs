using Tsumugi.Domain.Enums;

namespace Tsumugi.Application.Dtos;

public sealed record DisabilityCertificateDto(
    Guid Id,
    Guid RecipientId,
    DisabilityCertificateType Type,
    string Grade,
    string? Subtype,
    DateOnly IssuedDate,
    DateOnly? NextRenewalDate,
    string IssuingAuthority,
    string? CertificateNumber,
    string? Notes);
