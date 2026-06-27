namespace Tsumugi.Application.Dtos;

public sealed record ExpiringCertificateDto(
    Guid CertificateId, Guid RecipientId, string CertificateNumber,
    DateOnly ExpiryDate, int RemainingDays);
