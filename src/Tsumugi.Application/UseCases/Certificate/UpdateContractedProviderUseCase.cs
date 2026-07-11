using Tsumugi.Application.Abstractions;

namespace Tsumugi.Application.UseCases.Certificate;

/// <summary>受給者証「サービス事業者記入欄」の既存行を楽観ロック付きで更新する。</summary>
public sealed class UpdateContractedProviderUseCase(
    IContractedProviderRepository repo, IUnitOfWork uow)
{
    public async Task ExecuteAsync(
        Guid id,
        Guid expectedConcurrencyToken,
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
        if (id == Guid.Empty)
            throw new ArgumentException("契約事業者IDが指定されていません。", nameof(id));
        if (string.IsNullOrWhiteSpace(actor))
            throw new ArgumentException("操作者は必須です。", nameof(actor));

        var existing = await repo.FindByIdAsync(id, ct)
            ?? throw new InvalidOperationException("契約事業者が見つかりません。");
        if (existing.ConcurrencyToken != expectedConcurrencyToken)
            throw new OptimisticConcurrencyException(
                nameof(Tsumugi.Domain.Entities.ContractedProvider), id);

        if (string.IsNullOrWhiteSpace(providerNumber))
            throw new ArgumentException("事業所番号は必須です。", nameof(providerNumber));
        if (string.IsNullOrWhiteSpace(providerName))
            throw new ArgumentException("事業所名は必須です。", nameof(providerName));
        if (string.IsNullOrWhiteSpace(serviceCategory))
            throw new ArgumentException("サービス種別は必須です。", nameof(serviceCategory));
        ArgumentOutOfRangeException.ThrowIfNegative(contractedSupplyDays);
        if (terminationDate is { } t && t < contractDate)
            throw new ArgumentException(
                "契約終了日は契約日以後である必要があります。", nameof(terminationDate));
        RegisterContractedProviderUseCase.ValidateCertificateEntryNumber(certificateEntryNumber);

        var updated = existing with
        {
            ProviderNumber = providerNumber,
            ProviderName = providerName,
            ServiceCategory = serviceCategory,
            ContractedSupplyDays = contractedSupplyDays,
            ContractDate = contractDate,
            TerminationDate = terminationDate,
            Notes = notes,
            CertificateEntryNumber = certificateEntryNumber,
        };

        await repo.UpdateAsync(updated, ct);
        await uow.SaveChangesAsync(ct);
    }
}
