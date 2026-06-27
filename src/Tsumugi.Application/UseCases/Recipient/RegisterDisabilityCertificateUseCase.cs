using Tsumugi.Application.Abstractions;
using Tsumugi.Application.Dtos;
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.Enums;

namespace Tsumugi.Application.UseCases.Recipient;

/// <summary>
/// 障害者手帳の新規登録／更新（追記）。
/// 等級改定や更新時は同じ利用者に対して新しいレコードを追加する（append-only）。
/// </summary>
public sealed class RegisterDisabilityCertificateUseCase(
    IDisabilityCertificateRepository repo, IUnitOfWork uow, TimeProvider clock)
{
    public async Task<DisabilityCertificateDto> ExecuteAsync(
        Guid recipientId,
        DisabilityCertificateType type,
        string grade,
        DateOnly issuedDate,
        string issuingAuthority,
        string actor,
        CancellationToken ct,
        string? subtype = null,
        DateOnly? nextRenewalDate = null,
        string? certificateNumber = null,
        string? notes = null)
    {
        if (recipientId == Guid.Empty)
            throw new ArgumentException("利用者IDが指定されていません。", nameof(recipientId));
        if (string.IsNullOrWhiteSpace(actor))
            throw new ArgumentException("操作者は必須です。", nameof(actor));

        var entity = DisabilityCertificate.Create(
            Guid.NewGuid(), recipientId, type, grade, issuedDate, issuingAuthority,
            actor, clock.GetUtcNow(), Guid.NewGuid(),
            subtype: subtype, nextRenewalDate: nextRenewalDate,
            certificateNumber: certificateNumber, notes: notes);

        await repo.AddAsync(entity, ct);
        await uow.SaveChangesAsync(ct);
        return ToDto(entity);
    }

    internal static DisabilityCertificateDto ToDto(DisabilityCertificate e) => new(
        e.Id, e.RecipientId, e.Type, e.Grade, e.Subtype,
        e.IssuedDate, e.NextRenewalDate, e.IssuingAuthority,
        e.CertificateNumber, e.Notes);
}
