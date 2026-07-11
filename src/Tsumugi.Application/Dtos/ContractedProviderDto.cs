namespace Tsumugi.Application.Dtos;

/// <summary>
/// 受給者証「サービス事業者記入欄」の 1 行（1 事業所との契約）の DTO。
/// </summary>
public sealed record ContractedProviderDto(
    Guid Id,
    Guid CertificateId,
    string ProviderNumber,
    string ProviderName,
    string ServiceCategory,
    int ContractedSupplyDays,
    DateOnly ContractDate,
    DateOnly? TerminationDate,
    string? Notes,
    Guid ConcurrencyToken,
    int? CertificateEntryNumber = null);
