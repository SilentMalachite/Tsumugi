using Tsumugi.Application.Abstractions;
using Tsumugi.Application.Dtos;
using Tsumugi.Application.Validation;
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.Logic;
using Tsumugi.Domain.ValueObjects;

namespace Tsumugi.Application.UseCases.Certificate;

public sealed class RegisterCertificateUseCase(
    ICertificateRepository repo, IUnitOfWork uow, TimeProvider clock)
{
    public async Task<(CertificateDto Dto, IReadOnlyList<string> Warnings)> ExecuteAsync(
        Guid recipientId, string certificateNumber, DateRange validity,
        int supplyDays, int monthlyCostCap, string municipality,
        string actor, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(certificateNumber))
            throw new ArgumentException("受給者証番号は必須です。", nameof(certificateNumber));
        DateValidator.EnsureRange(validity.Start, validity.End, nameof(validity));

        var existing = await repo.ListByRecipientAsync(recipientId, ct);
        var warnings = new List<string>();
        var ranges = existing.Select(c => c.Validity).Append(validity).ToArray();
        var overlaps = PeriodPolicy.DetectOverlaps(ranges);
        if (overlaps.Count > 0)
            warnings.Add("同一利用者の受給者証期間が重複しています。意図的か確認してください。");
        var gaps = PeriodPolicy.DetectGaps(ranges);
        if (gaps.Count > 0)
            warnings.Add("受給者証期間に空白があります。連続性を確認してください。");

        var entity = Domain.Entities.Certificate.Create(
            Guid.NewGuid(), recipientId, certificateNumber, validity,
            supplyDays, monthlyCostCap, municipality,
            actor, clock.GetUtcNow(), Guid.NewGuid());

        await repo.AddAsync(entity, ct);
        await uow.SaveChangesAsync(ct);

        return (new CertificateDto(entity.Id, entity.RecipientId, entity.CertificateNumber,
            entity.Validity, entity.SupplyDays, entity.MonthlyCostCap, entity.Municipality),
            warnings);
    }
}
