namespace Tsumugi.Domain.Entities;

/// <summary>確定請求に含まれる受給者単位の追記型スナップショット。</summary>
public sealed record ClaimDetail : Entity
{
    public required Guid ClaimBatchId { get; init; }
    public required Guid RecipientId { get; init; }
    public required string SnapshotSchemaVersion { get; init; }
    public required string ClaimMasterVersion { get; init; }
    public required string CsvSpecificationVersion { get; init; }
    public required string ReportSpecificationVersion { get; init; }
    public required string SnapshotApplicationVersion { get; init; }
    public required string InputSnapshotJson { get; init; }
    public required string CalculationSnapshotJson { get; init; }
    public required int TotalUnits { get; init; }
    public required int TotalCostYen { get; init; }
    public required int BenefitYen { get; init; }
    public required int BurdenYen { get; init; }

    public static ClaimDetail Create(
        Guid id,
        Guid claimBatchId,
        Guid recipientId,
        string snapshotSchemaVersion,
        string claimMasterVersion,
        string csvSpecificationVersion,
        string reportSpecificationVersion,
        string snapshotApplicationVersion,
        string inputSnapshotJson,
        string calculationSnapshotJson,
        int totalUnits,
        int totalCostYen,
        int benefitYen,
        int burdenYen,
        string createdBy,
        DateTimeOffset createdAt)
    {
        RequireIdentity(id, nameof(id));
        RequireIdentity(claimBatchId, nameof(claimBatchId));
        RequireIdentity(recipientId, nameof(recipientId));
        ArgumentException.ThrowIfNullOrWhiteSpace(snapshotSchemaVersion);
        ArgumentException.ThrowIfNullOrWhiteSpace(claimMasterVersion);
        ArgumentException.ThrowIfNullOrWhiteSpace(csvSpecificationVersion);
        ArgumentException.ThrowIfNullOrWhiteSpace(reportSpecificationVersion);
        ArgumentException.ThrowIfNullOrWhiteSpace(snapshotApplicationVersion);
        ArgumentException.ThrowIfNullOrWhiteSpace(inputSnapshotJson);
        ArgumentException.ThrowIfNullOrWhiteSpace(calculationSnapshotJson);
        ArgumentOutOfRangeException.ThrowIfNegative(totalUnits);
        ArgumentOutOfRangeException.ThrowIfNegative(totalCostYen);
        ArgumentOutOfRangeException.ThrowIfNegative(benefitYen);
        ArgumentOutOfRangeException.ThrowIfNegative(burdenYen);
        ArgumentException.ThrowIfNullOrWhiteSpace(createdBy);

        return new ClaimDetail
        {
            Id = id,
            ClaimBatchId = claimBatchId,
            RecipientId = recipientId,
            SnapshotSchemaVersion = snapshotSchemaVersion,
            ClaimMasterVersion = claimMasterVersion,
            CsvSpecificationVersion = csvSpecificationVersion,
            ReportSpecificationVersion = reportSpecificationVersion,
            SnapshotApplicationVersion = snapshotApplicationVersion,
            InputSnapshotJson = inputSnapshotJson,
            CalculationSnapshotJson = calculationSnapshotJson,
            TotalUnits = totalUnits,
            TotalCostYen = totalCostYen,
            BenefitYen = benefitYen,
            BurdenYen = burdenYen,
            CreatedBy = createdBy,
            CreatedAt = createdAt,
            ConcurrencyToken = Guid.Empty,
        };
    }

    private static void RequireIdentity(Guid value, string parameterName)
    {
        if (value == Guid.Empty)
            throw new ArgumentException("IDが空です。", parameterName);
    }
}
