using Tsumugi.Application.Abstractions;
using Tsumugi.Application.Dtos;

namespace Tsumugi.Application.UseCases.Certificate;

public sealed record CorrectCertificateInput(
    Guid RootCertificateId,
    Guid ExpectedHeadCertificateId,
    string MunicipalityNumber)
{
    public string? SubsidyMunicipalityNumber { get; init; }
    public string? UpperLimitManagementProviderNumber { get; init; }
}

public sealed class CorrectCertificateUseCase(
    ICertificateRepository repo,
    IUnitOfWork uow,
    TimeProvider clock)
{
    public async Task<CertificateDto> ExecuteAsync(
        CorrectCertificateInput input,
        string actor,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(input);
        RequireIdentity(input.RootCertificateId, nameof(input.RootCertificateId));
        RequireIdentity(input.ExpectedHeadCertificateId, nameof(input.ExpectedHeadCertificateId));
        ValidateDigits(input.MunicipalityNumber, 6, nameof(input.MunicipalityNumber), required: true);
        ValidateDigits(
            input.SubsidyMunicipalityNumber,
            6,
            nameof(input.SubsidyMunicipalityNumber),
            required: false);
        ValidateDigits(
            input.UpperLimitManagementProviderNumber,
            10,
            nameof(input.UpperLimitManagementProviderNumber),
            required: false);

        var head = await repo.FindHeadByRootIdAsync(input.RootCertificateId, ct)
            ?? throw new InvalidOperationException("訂正対象の受給者証rootが見つかりません。");
        if (head.RootCertificateId != input.RootCertificateId
            || head.Id != input.ExpectedHeadCertificateId)
            throw new InvalidOperationException(
                "受給者証は既に訂正されています。最新状態を再読込してください。");

        var correction = head with
        {
            Id = Guid.NewGuid(),
            Revision = checked(head.Revision + 1),
            ExpectedHeadCertificateId = head.Id,
            MunicipalityNumber = input.MunicipalityNumber,
            SubsidyMunicipalityNumber = input.SubsidyMunicipalityNumber,
            UpperLimitManagementProviderNumber = input.UpperLimitManagementProviderNumber,
            CreatedBy = actor,
            CreatedAt = clock.GetUtcNow(),
            ConcurrencyToken = Guid.NewGuid(),
        };

        await repo.AddAsync(correction, ct);
        await uow.SaveChangesAsync(ct);
        return RegisterCertificateUseCase.MapToDto(correction);
    }

    private static void RequireIdentity(Guid value, string parameterName)
    {
        if (value == Guid.Empty)
            throw new ArgumentException("IDが指定されていません。", parameterName);
    }

    private static void ValidateDigits(
        string? value,
        int length,
        string parameterName,
        bool required)
    {
        if (!required && value is null)
            return;
        if (value is null
            || value.Length != length
            || value.Any(character => character is not (>= '0' and <= '9')))
            throw new ArgumentException(
                $"{parameterName}は{length}桁の半角数字で入力してください。",
                parameterName);
    }
}
