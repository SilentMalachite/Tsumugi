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
        => await ExecuteAsync(
            officeNumber, name, category, region,
            postalCode: null, address: null, phoneNumber: null,
            representativeTitleAndName: null, actor, ct);

    public async Task<OfficeDto> ExecuteAsync(
        string officeNumber, string name, ServiceCategory category, RegionGrade region,
        string? postalCode, string? address, string? phoneNumber,
        string? representativeTitleAndName,
        string actor, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(officeNumber))
            throw new ArgumentException("事業所番号は必須です。", nameof(officeNumber));
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("事業所名は必須です。", nameof(name));
        ValidateOptionalInput(postalCode, 16, nameof(postalCode), "郵便番号");
        ValidateOptionalInput(address, 256, nameof(address), "住所");
        ValidateOptionalInput(phoneNumber, 32, nameof(phoneNumber), "電話番号");
        ValidateOptionalInput(
            representativeTitleAndName, 128,
            nameof(representativeTitleAndName), "代表者職氏名");

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
            concurrencyToken: Guid.NewGuid(),
            postalCode: postalCode,
            address: address,
            phoneNumber: phoneNumber,
            representativeTitleAndName: representativeTitleAndName);

        await repository.AddAsync(office, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return new OfficeDto(
            office.Id, office.OfficeNumber, office.Name,
            office.ServiceCategory, office.RegionGrade, office.ConcurrencyToken,
            office.PostalCode, office.Address, office.PhoneNumber,
            office.RepresentativeTitleAndName);
    }

    internal static void ValidateOptionalInput(
        string? value, int maxLength, string parameterName, string displayName)
    {
        if (value is null)
            return;
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException($"{displayName}は空白以外を指定してください。", parameterName);
        if (value.Length > maxLength)
            throw new ArgumentException($"{displayName}は{maxLength}文字以内で指定してください。", parameterName);
    }
}
