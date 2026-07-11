using Tsumugi.Domain.Enums;
using Tsumugi.Domain.Logic.Claim.Models;
using Tsumugi.Domain.ValueObjects;

namespace Tsumugi.Domain.Entities;

/// <summary>事業所・利用者・サービス月単位の月次請求固有入力。</summary>
public sealed record ClaimInput : Entity
{
    public required Guid OfficeId { get; init; }
    public required Guid RecipientId { get; init; }
    public required ServiceMonth ServiceMonth { get; init; }
    public required Guid RootId { get; init; }
    public required int Revision { get; init; }
    public required RecordKind Kind { get; init; }
    public Guid? ExpectedHeadId { get; init; }
    public UpperLimitManagementResult? UpperLimitManagementResult { get; init; }
    public int? UpperLimitManagedAmountYen { get; init; }
    public int? MunicipalSubsidyAmountYen { get; init; }
    public ServiceMonth? ExceptionalUsageStartMonth { get; init; }
    public ServiceMonth? ExceptionalUsageEndMonth { get; init; }
    public int? ExceptionalUsageDays { get; init; }
    public int? StandardUsageDayTotal { get; init; }
}
