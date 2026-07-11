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
        CertificateClaimInputValidator.Validate(
            input.MunicipalityNumber,
            input.SubsidyMunicipalityNumber,
            input.UpperLimitManagementProviderNumber);

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
}
