using Tsumugi.Domain.Enums;
using Tsumugi.Domain.Logic.Claim.Models;
using Tsumugi.Domain.ValueObjects;

namespace Tsumugi.Domain.Entities;

/// <summary>受給者証上限と法31条特例を原本確認済み状態で保持する追記型根拠。</summary>
public sealed record CertificateClaimEvidence : Entity
{
    public required Guid CertificateId { get; init; }
    public required DateRange Validity { get; init; }
    public required Guid RootId { get; init; }
    public required int Revision { get; init; }
    public required RecordKind Kind { get; init; }
    public Guid? ExpectedHeadId { get; init; }
    public required EnteredYen MonthlyCostCap { get; init; }
    public required UpperLimitManagementApplicability UpperLimitManagementApplicability { get; init; }
    public string? UpperLimitManagementOfficeNumber { get; init; }
    public required Article31SpecialBurdenStatus Article31Status { get; init; }
    public required EnteredYen Article31AmountYen { get; init; }
    public required DateRange? Article31EffectivePeriod { get; init; }
    public string? OriginalDocumentReference { get; init; }
    public DateTimeOffset? ConfirmedAt { get; init; }
    public string? ConfirmedBy { get; init; }
    public string? ConfirmationReason { get; init; }
}
