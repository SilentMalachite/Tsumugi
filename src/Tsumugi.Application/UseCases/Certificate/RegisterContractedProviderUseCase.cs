using Tsumugi.Application.Abstractions;
using Tsumugi.Application.Dtos;
using Tsumugi.Domain.Entities;

namespace Tsumugi.Application.UseCases.Certificate;

/// <summary>
/// 受給者証「サービス事業者記入欄」へ 1 行追加する。
/// </summary>
public sealed class RegisterContractedProviderUseCase(
    IContractedProviderRepository repo, IUnitOfWork uow, TimeProvider clock)
{
    public async Task<ContractedProviderDto> ExecuteAsync(
        Guid certificateId,
        string providerNumber,
        string providerName,
        string serviceCategory,
        int contractedSupplyDays,
        DateOnly contractDate,
        DateOnly? terminationDate,
        string? notes,
        string actor,
        CancellationToken ct)
        => await ExecuteAsync(
            certificateId, providerNumber, providerName, serviceCategory,
            contractedSupplyDays, contractDate, terminationDate, notes,
            certificateEntryNumber: null, actor, ct);

    public async Task<ContractedProviderDto> ExecuteAsync(
        Guid certificateId,
        string providerNumber,
        string providerName,
        string serviceCategory,
        int contractedSupplyDays,
        DateOnly contractDate,
        DateOnly? terminationDate,
        string? notes,
        int? certificateEntryNumber,
        string actor,
        CancellationToken ct)
    {
        if (certificateId == Guid.Empty)
            throw new ArgumentException("受給者証IDが指定されていません。", nameof(certificateId));
        if (string.IsNullOrWhiteSpace(providerNumber))
            throw new ArgumentException("事業所番号は必須です。", nameof(providerNumber));
        if (string.IsNullOrWhiteSpace(providerName))
            throw new ArgumentException("事業所名は必須です。", nameof(providerName));
        if (string.IsNullOrWhiteSpace(serviceCategory))
            throw new ArgumentException("サービス種別は必須です。", nameof(serviceCategory));
        ArgumentOutOfRangeException.ThrowIfNegative(contractedSupplyDays);
        if (terminationDate is { } t && t < contractDate)
            throw new ArgumentException("契約終了日は契約日以後である必要があります。", nameof(terminationDate));
        ValidateCertificateEntryNumber(certificateEntryNumber);

        var entity = ContractedProvider.Create(
            id: Guid.NewGuid(),
            certificateId: certificateId,
            providerNumber: providerNumber,
            providerName: providerName,
            serviceCategory: serviceCategory,
            contractedSupplyDays: contractedSupplyDays,
            contractDate: contractDate,
            createdBy: actor,
            createdAt: clock.GetUtcNow(),
            concurrencyToken: Guid.NewGuid(),
            terminationDate: terminationDate,
            notes: notes,
            certificateEntryNumber: certificateEntryNumber);

        await repo.AddAsync(entity, ct);
        await uow.SaveChangesAsync(ct);

        return ToDto(entity);
    }

    internal static ContractedProviderDto ToDto(ContractedProvider e) => new(
        e.Id, e.CertificateId, e.ProviderNumber, e.ProviderName, e.ServiceCategory,
        e.ContractedSupplyDays, e.ContractDate, e.TerminationDate, e.Notes, e.ConcurrencyToken,
        e.CertificateEntryNumber);

    internal static void ValidateCertificateEntryNumber(int? certificateEntryNumber)
    {
        if (certificateEntryNumber is < 0 or > 99)
            throw new ArgumentOutOfRangeException(
                nameof(certificateEntryNumber), "証書記入欄番号は0から99の範囲で指定してください。");
    }
}
