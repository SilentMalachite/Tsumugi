using Tsumugi.Application.Abstractions;
using Tsumugi.Application.Audit;
using Tsumugi.Domain.Enums;

namespace Tsumugi.Application.UseCases.Office;

public sealed class UpdateOfficeUseCase(
    IOfficeRepository repo, IUnitOfWork uow, TimeProvider clock, IAuditTrail audit)
{
    public Task ExecuteAsync(
        Guid id, Guid expectedConcurrencyToken,
        string name, ServiceCategory category, RegionGrade region,
        string actor,
        CancellationToken ct)
        => ExecuteCoreAsync(
            id, expectedConcurrencyToken, name, category, region,
            postalCode: null, address: null, phoneNumber: null,
            representativeTitleAndName: null, replaceClaimInputs: false, actor, ct);

    public Task ExecuteAsync(
        Guid id, Guid expectedConcurrencyToken,
        string name, ServiceCategory category, RegionGrade region,
        string? postalCode, string? address, string? phoneNumber,
        string? representativeTitleAndName,
        string actor,
        CancellationToken ct)
        => ExecuteCoreAsync(
            id, expectedConcurrencyToken, name, category, region,
            postalCode, address, phoneNumber, representativeTitleAndName,
            replaceClaimInputs: true, actor, ct);

    private async Task ExecuteCoreAsync(
        Guid id, Guid expectedConcurrencyToken,
        string name, ServiceCategory category, RegionGrade region,
        string? postalCode, string? address, string? phoneNumber,
        string? representativeTitleAndName,
        bool replaceClaimInputs,
        string actor,
        CancellationToken ct)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("事業所IDが指定されていません。", nameof(id));
        if (string.IsNullOrWhiteSpace(actor))
            throw new ArgumentException("操作者は必須です。", nameof(actor));
        var existing = await repo.FindByIdAsync(id, ct)
            ?? throw new InvalidOperationException("事業所が見つかりません。");
        if (existing.ConcurrencyToken != expectedConcurrencyToken)
            throw new OptimisticConcurrencyException(nameof(Tsumugi.Domain.Entities.Office), id);
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("事業所名は必須です。", nameof(name));
        if (replaceClaimInputs)
        {
            RegisterOfficeUseCase.ValidateOptionalInput(
                postalCode, 16, nameof(postalCode), "郵便番号");
            RegisterOfficeUseCase.ValidateOptionalInput(
                address, 256, nameof(address), "住所");
            RegisterOfficeUseCase.ValidateOptionalInput(
                phoneNumber, 32, nameof(phoneNumber), "電話番号");
            RegisterOfficeUseCase.ValidateOptionalInput(
                representativeTitleAndName, 128,
                nameof(representativeTitleAndName), "代表者職氏名");
        }

        var updated = existing with
        {
            Name = name,
            ServiceCategory = category,
            RegionGrade = region,
            PostalCode = replaceClaimInputs ? postalCode : existing.PostalCode,
            Address = replaceClaimInputs ? address : existing.Address,
            PhoneNumber = replaceClaimInputs ? phoneNumber : existing.PhoneNumber,
            RepresentativeTitleAndName = replaceClaimInputs
                ? representativeTitleAndName
                : existing.RepresentativeTitleAndName,
        };
        await repo.UpdateAsync(updated, ct);
        await audit.RecordAsync(actor, AuditAction.Update, nameof(Tsumugi.Domain.Entities.Office),
            id, clock.GetUtcNow(),
            summary: $"name={name}; category={category}; region={region}", ct);
        await uow.SaveChangesAsync(ct);
    }
}
