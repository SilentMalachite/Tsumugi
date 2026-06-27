using Tsumugi.Domain.ValueObjects;

namespace Tsumugi.Application.Dtos;

public sealed record CertificateDto(
    Guid Id, Guid RecipientId, string CertificateNumber, DateRange Validity,
    int SupplyDays, int MonthlyCostCap, string Municipality);
