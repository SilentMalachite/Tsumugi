using Tsumugi.Domain.Enums;
using Tsumugi.Domain.ValueObjects;

namespace Tsumugi.Domain.Entities;

/// <summary>確定請求の追記型ヘッダ。訂正と取消も新しいrevisionとして保持する。</summary>
public sealed record ClaimBatch : Entity
{
    public const string CurrentOperationPayloadSchemaVersion = "claim-finalization-operation-v1";

    public required Guid OfficeId { get; init; }
    public required ServiceMonth ServiceMonth { get; init; }
    public required int Revision { get; init; }
    public required RecordKind Kind { get; init; }
    public Guid? OriginId { get; init; }
    public Guid? ExpectedHeadBatchId { get; init; }
    public int? ExpectedHeadRevision { get; init; }
    public required int TotalUnits { get; init; }
    public required int TotalCostYen { get; init; }
    public required int TotalBenefitYen { get; init; }
    public required int TotalBurdenYen { get; init; }
    public required string ClaimMasterVersion { get; init; }
    public required string CsvSpecificationVersion { get; init; }
    public required string ReportSpecificationVersion { get; init; }
    public required string SnapshotApplicationVersion { get; init; }
    public required string OperationApplicationVersion { get; init; }
    public required Guid FinalizationOperationId { get; init; }
    public required string OperationPayloadSchemaVersion { get; init; }
    public required string OperationPayloadSha256 { get; init; }

    public static ClaimBatch NewRecord(
        Guid id,
        Guid officeId,
        ServiceMonth serviceMonth,
        int totalUnits,
        int totalCostYen,
        int totalBenefitYen,
        int totalBurdenYen,
        string claimMasterVersion,
        string csvSpecificationVersion,
        string reportSpecificationVersion,
        string snapshotApplicationVersion,
        string operationApplicationVersion,
        Guid finalizationOperationId,
        string operationPayloadSchemaVersion,
        string operationPayloadSha256,
        string createdBy,
        DateTimeOffset createdAt)
    {
        ValidateCommon(
            id,
            officeId,
            serviceMonth,
            claimMasterVersion,
            csvSpecificationVersion,
            reportSpecificationVersion,
            snapshotApplicationVersion,
            operationApplicationVersion,
            finalizationOperationId,
            operationPayloadSchemaVersion,
            operationPayloadSha256,
            createdBy);
        ValidateTotals(totalUnits, totalCostYen, totalBenefitYen, totalBurdenYen);

        return Create(
            id,
            officeId,
            serviceMonth,
            revision: 1,
            RecordKind.New,
            originId: null,
            expectedHeadBatchId: null,
            expectedHeadRevision: null,
            totalUnits,
            totalCostYen,
            totalBenefitYen,
            totalBurdenYen,
            claimMasterVersion,
            csvSpecificationVersion,
            reportSpecificationVersion,
            snapshotApplicationVersion,
            operationApplicationVersion,
            finalizationOperationId,
            operationPayloadSchemaVersion,
            operationPayloadSha256,
            createdBy,
            createdAt);
    }

    public static ClaimBatch Correction(
        Guid id,
        Guid officeId,
        ServiceMonth serviceMonth,
        int revision,
        Guid originId,
        Guid expectedHeadBatchId,
        int expectedHeadRevision,
        int totalUnits,
        int totalCostYen,
        int totalBenefitYen,
        int totalBurdenYen,
        string claimMasterVersion,
        string csvSpecificationVersion,
        string reportSpecificationVersion,
        string snapshotApplicationVersion,
        string operationApplicationVersion,
        Guid finalizationOperationId,
        string operationPayloadSchemaVersion,
        string operationPayloadSha256,
        string createdBy,
        DateTimeOffset createdAt)
    {
        ValidateCommon(
            id,
            officeId,
            serviceMonth,
            claimMasterVersion,
            csvSpecificationVersion,
            reportSpecificationVersion,
            snapshotApplicationVersion,
            operationApplicationVersion,
            finalizationOperationId,
            operationPayloadSchemaVersion,
            operationPayloadSha256,
            createdBy);
        ValidateRevision(revision, originId, expectedHeadBatchId, expectedHeadRevision);
        ValidateTotals(totalUnits, totalCostYen, totalBenefitYen, totalBurdenYen);

        return Create(
            id,
            officeId,
            serviceMonth,
            revision,
            RecordKind.Correct,
            originId,
            expectedHeadBatchId,
            expectedHeadRevision,
            totalUnits,
            totalCostYen,
            totalBenefitYen,
            totalBurdenYen,
            claimMasterVersion,
            csvSpecificationVersion,
            reportSpecificationVersion,
            snapshotApplicationVersion,
            operationApplicationVersion,
            finalizationOperationId,
            operationPayloadSchemaVersion,
            operationPayloadSha256,
            createdBy,
            createdAt);
    }

    public static ClaimBatch Cancellation(
        Guid id,
        Guid officeId,
        ServiceMonth serviceMonth,
        int revision,
        Guid originId,
        Guid expectedHeadBatchId,
        int expectedHeadRevision,
        string claimMasterVersion,
        string csvSpecificationVersion,
        string reportSpecificationVersion,
        string snapshotApplicationVersion,
        string operationApplicationVersion,
        Guid finalizationOperationId,
        string operationPayloadSchemaVersion,
        string operationPayloadSha256,
        string createdBy,
        DateTimeOffset createdAt)
    {
        ValidateCommon(
            id,
            officeId,
            serviceMonth,
            claimMasterVersion,
            csvSpecificationVersion,
            reportSpecificationVersion,
            snapshotApplicationVersion,
            operationApplicationVersion,
            finalizationOperationId,
            operationPayloadSchemaVersion,
            operationPayloadSha256,
            createdBy);
        ValidateRevision(revision, originId, expectedHeadBatchId, expectedHeadRevision);

        return Create(
            id,
            officeId,
            serviceMonth,
            revision,
            RecordKind.Cancel,
            originId,
            expectedHeadBatchId,
            expectedHeadRevision,
            totalUnits: 0,
            totalCostYen: 0,
            totalBenefitYen: 0,
            totalBurdenYen: 0,
            claimMasterVersion,
            csvSpecificationVersion,
            reportSpecificationVersion,
            snapshotApplicationVersion,
            operationApplicationVersion,
            finalizationOperationId,
            operationPayloadSchemaVersion,
            operationPayloadSha256,
            createdBy,
            createdAt);
    }

    private static ClaimBatch Create(
        Guid id,
        Guid officeId,
        ServiceMonth serviceMonth,
        int revision,
        RecordKind kind,
        Guid? originId,
        Guid? expectedHeadBatchId,
        int? expectedHeadRevision,
        int totalUnits,
        int totalCostYen,
        int totalBenefitYen,
        int totalBurdenYen,
        string claimMasterVersion,
        string csvSpecificationVersion,
        string reportSpecificationVersion,
        string snapshotApplicationVersion,
        string operationApplicationVersion,
        Guid finalizationOperationId,
        string operationPayloadSchemaVersion,
        string operationPayloadSha256,
        string createdBy,
        DateTimeOffset createdAt) => new()
        {
            Id = id,
            OfficeId = officeId,
            ServiceMonth = serviceMonth,
            Revision = revision,
            Kind = kind,
            OriginId = originId,
            ExpectedHeadBatchId = expectedHeadBatchId,
            ExpectedHeadRevision = expectedHeadRevision,
            TotalUnits = totalUnits,
            TotalCostYen = totalCostYen,
            TotalBenefitYen = totalBenefitYen,
            TotalBurdenYen = totalBurdenYen,
            ClaimMasterVersion = claimMasterVersion,
            CsvSpecificationVersion = csvSpecificationVersion,
            ReportSpecificationVersion = reportSpecificationVersion,
            SnapshotApplicationVersion = snapshotApplicationVersion,
            OperationApplicationVersion = operationApplicationVersion,
            FinalizationOperationId = finalizationOperationId,
            OperationPayloadSchemaVersion = operationPayloadSchemaVersion,
            OperationPayloadSha256 = operationPayloadSha256,
            CreatedBy = createdBy,
            CreatedAt = createdAt,
            ConcurrencyToken = Guid.Empty,
        };

    private static void ValidateCommon(
        Guid id,
        Guid officeId,
        ServiceMonth serviceMonth,
        string claimMasterVersion,
        string csvSpecificationVersion,
        string reportSpecificationVersion,
        string snapshotApplicationVersion,
        string operationApplicationVersion,
        Guid finalizationOperationId,
        string operationPayloadSchemaVersion,
        string operationPayloadSha256,
        string createdBy)
    {
        RequireIdentity(id, nameof(id));
        RequireIdentity(officeId, nameof(officeId));
        _ = serviceMonth.ToInt();
        ArgumentException.ThrowIfNullOrWhiteSpace(claimMasterVersion);
        ArgumentException.ThrowIfNullOrWhiteSpace(csvSpecificationVersion);
        ArgumentException.ThrowIfNullOrWhiteSpace(reportSpecificationVersion);
        ArgumentException.ThrowIfNullOrWhiteSpace(snapshotApplicationVersion);
        ArgumentException.ThrowIfNullOrWhiteSpace(operationApplicationVersion);
        RequireIdentity(finalizationOperationId, nameof(finalizationOperationId));
        ArgumentException.ThrowIfNullOrWhiteSpace(createdBy);

        if (!string.Equals(
                operationPayloadSchemaVersion,
                CurrentOperationPayloadSchemaVersion,
                StringComparison.Ordinal))
            throw new ArgumentException(
                $"OperationPayloadSchemaVersion は {CurrentOperationPayloadSchemaVersion} でなければなりません。",
                nameof(operationPayloadSchemaVersion));

        if (operationPayloadSha256 is not { Length: 64 }
            || operationPayloadSha256.Any(character =>
                character is not (>= '0' and <= '9') and not (>= 'a' and <= 'f')))
            throw new ArgumentException(
                "OperationPayloadSha256 は64文字の小文字16進数でなければなりません。",
                nameof(operationPayloadSha256));
    }

    private static void ValidateRevision(
        int revision,
        Guid originId,
        Guid expectedHeadBatchId,
        int expectedHeadRevision)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(revision, 2);
        RequireIdentity(originId, nameof(originId));
        RequireIdentity(expectedHeadBatchId, nameof(expectedHeadBatchId));

        if (expectedHeadRevision != revision - 1)
            throw new ArgumentException(
                "ExpectedHeadRevision はRevisionの直前でなければなりません。",
                nameof(expectedHeadRevision));
    }

    private static void ValidateTotals(
        int totalUnits,
        int totalCostYen,
        int totalBenefitYen,
        int totalBurdenYen)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(totalUnits);
        ArgumentOutOfRangeException.ThrowIfNegative(totalCostYen);
        ArgumentOutOfRangeException.ThrowIfNegative(totalBenefitYen);
        ArgumentOutOfRangeException.ThrowIfNegative(totalBurdenYen);
    }

    private static void RequireIdentity(Guid value, string parameterName)
    {
        if (value == Guid.Empty)
            throw new ArgumentException("IDが空です。", parameterName);
    }
}
