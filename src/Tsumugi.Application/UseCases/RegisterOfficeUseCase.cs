using Tsumugi.Application.Abstractions;
using Tsumugi.Application.Dtos;
using Tsumugi.Domain.Enums;
using OfficeEntity = Tsumugi.Domain.Entities.Office;

namespace Tsumugi.Application.UseCases;

public sealed class RegisterOfficeUseCase(
    IOfficeRepository repository,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
{
    public async Task<OfficeDto> ExecuteAsync(
        string officeNumber, string name, ServiceCategory category, RegionGrade region,
        string actor, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(officeNumber))
            throw new ArgumentException("事業所番号は必須です。", nameof(officeNumber));
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("事業所名は必須です。", nameof(name));

        if (await repository.FindByNumberAsync(officeNumber, ct) is not null)
            throw new InvalidOperationException("同一の事業所番号が既に登録されています。");

        var office = OfficeEntity.Create(
            id: Guid.NewGuid(),
            officeNumber: officeNumber,
            name: name,
            serviceCategory: category,
            regionGrade: region,
            createdBy: actor,
            createdAt: timeProvider.GetUtcNow(),
            concurrencyToken: Guid.NewGuid());

        await repository.AddAsync(office, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return new OfficeDto(
            office.Id, office.OfficeNumber, office.Name,
            office.ServiceCategory, office.RegionGrade, office.ConcurrencyToken);
    }
}
