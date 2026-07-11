using Tsumugi.Domain.Enums;
using Tsumugi.Domain.Logic.Claim.Models;
using Tsumugi.Domain.ValueObjects;

namespace Tsumugi.Domain.Entities;

/// <summary>正式な利用者負担上限額管理結果票の追記型header。</summary>
public sealed record UpperLimitManagementStatement : Entity
{
    public required Guid RootId { get; init; }
    public required int Revision { get; init; }
    public required RecordKind Kind { get; init; }
    public Guid? ExpectedHeadId { get; init; }
    public required ServiceMonth ServiceMonth { get; init; }
    public required Guid RecipientId { get; init; }
    public required Guid CertificateId { get; init; }
    public required Guid ManagingOfficeId { get; init; }
    public required string MunicipalityNumber { get; init; }
    public required string CertificateNumber { get; init; }
    public required EnteredYen CertificateMonthlyCostCap { get; init; }
    public required UpperLimitManagementApplicability UpperLimitManagementApplicability { get; init; }
    public required string CertificateManagingOfficeNumber { get; init; }
    public required string ManagingOfficeNumber { get; init; }
    public required string ManagingOfficeName { get; init; }
    public required string OriginalCreationKind { get; init; }
    public DateTimeOffset? ReceivedAt { get; init; }
    public string? OriginalDocumentReference { get; init; }
    public required bool IsConfirmed { get; init; }
    public DateTimeOffset? ConfirmedAt { get; init; }
    public string? ConfirmedBy { get; init; }
    public string? ConfirmationReason { get; init; }
    public required UpperLimitManagementResult Result { get; init; }
    public required EnteredYen TotalCostYen { get; init; }
    public required EnteredYen TotalPreManagementBurdenYen { get; init; }
    public required EnteredYen TotalManagedBurdenYen { get; init; }
}
