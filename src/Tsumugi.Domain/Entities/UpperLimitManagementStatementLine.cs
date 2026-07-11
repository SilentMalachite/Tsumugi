using Tsumugi.Domain.Logic.Claim.Models;

namespace Tsumugi.Domain.Entities;

/// <summary>正式な利用者負担上限額管理結果票の事業所行。</summary>
public sealed record UpperLimitManagementStatementLine : Entity
{
    public required Guid StatementId { get; init; }
    public required int LineNumber { get; init; }
    public required string OfficeNumber { get; init; }
    public required string OfficeName { get; init; }
    public required EnteredYen TotalCostYen { get; init; }
    public required EnteredYen PreManagementBurdenYen { get; init; }
    public required EnteredYen ManagedBurdenYen { get; init; }
}
