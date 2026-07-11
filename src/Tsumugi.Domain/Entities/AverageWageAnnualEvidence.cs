using Tsumugi.Domain.Enums;
using Tsumugi.Domain.Logic.Claim.Models;

namespace Tsumugi.Domain.Entities;

/// <summary>請求用平均工賃の前年度実績と確認根拠を保持する追記型スナップショット。</summary>
public sealed record AverageWageAnnualEvidence : Entity
{
    public required Guid OfficeId { get; init; }
    public required int SourceFiscalYear { get; init; }
    public required DateOnly PeriodStart { get; init; }
    public required DateOnly PeriodEnd { get; init; }
    public required Guid RootId { get; init; }
    public required int Revision { get; init; }
    public required RecordKind Kind { get; init; }
    public Guid? ExpectedHeadId { get; init; }
    public int? AnnualWagePaidYen { get; init; }
    public int? AnnualExtendedUsers { get; init; }
    public int? AnnualOpeningDays { get; init; }
    public FiscalYearCompleteness? Completeness { get; init; }
    public string? EvidenceDocumentId { get; init; }
    public string? DailyEvidenceReference { get; init; }
    public string? MonthlyEvidenceReference { get; init; }
    public DateTimeOffset? ConfirmedAt { get; init; }
    public string? ConfirmedBy { get; init; }
    public string? ConfirmationReason { get; init; }
}
