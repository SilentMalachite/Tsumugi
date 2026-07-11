using Tsumugi.Domain.Enums;
using Tsumugi.Domain.Logic.Claim.Models;
using Tsumugi.Domain.ValueObjects;

namespace Tsumugi.Domain.Entities;

/// <summary>指定権者台帳に基づく事業所の請求区分登録を保持する追記型スナップショット。</summary>
public sealed record OfficeClaimProfile : Entity
{
    public required Guid OfficeId { get; init; }
    public required DateOnly EffectiveFrom { get; init; }
    public DateOnly? EffectiveTo { get; init; }
    public required Guid RootId { get; init; }
    public required int Revision { get; init; }
    public required RecordKind Kind { get; init; }
    public Guid? ExpectedHeadId { get; init; }
    public ClaimMasterVersion? MasterVersion { get; init; }
    public R8ReformStatus? ReformStatus { get; init; }
    public AverageWageBandOption? AverageWageBandOption { get; init; }
    public DateOnly? DesignationDate { get; init; }
    public DateOnly? SupportStartDate { get; init; }
    public VersionedAverageWageBandOption? EarlierRegisteredBandOption { get; init; }
    public ServiceMonth? EarlierRegistrationMonth { get; init; }
    public VersionedAverageWageBandOption? LaterRegisteredBandOption { get; init; }
    public ServiceMonth? LaterRegistrationMonth { get; init; }
    public string? ReformComparisonEvidenceDocumentId { get; init; }
    public DateRange? FiledTransitionPeriod { get; init; }
    public string? FiledTransitionEvidenceDocumentId { get; init; }
    public string? EvidenceDocumentId { get; init; }
    public DateTimeOffset? ConfirmedAt { get; init; }
    public string? ConfirmedBy { get; init; }
    public string? ConfirmationReason { get; init; }
}
