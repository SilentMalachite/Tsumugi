using Tsumugi.Application.Claim;
using Tsumugi.Domain.Enums;
using Tsumugi.Domain.ValueObjects;

namespace Tsumugi.Application.Abstractions;

public sealed record ClaimExpectedHead(Guid BatchId, int Revision);

public sealed record ClaimFinalizationDetailDraft(
    Guid RecipientId,
    string SnapshotSchemaVersion,
    string ClaimMasterVersion,
    string CsvSpecificationVersion,
    string ReportSpecificationVersion,
    string SnapshotApplicationVersion,
    ValidatedClaimSnapshotEnvelope InputSnapshotEnvelope,
    ValidatedClaimSnapshotEnvelope CalculationSnapshotEnvelope,
    int TotalUnits,
    int TotalCostYen,
    int BenefitYen,
    int BurdenYen);

/// <summary>請求確定操作のtyped入力。永続ID、revision、時刻及びraw JSONは受け取らない。</summary>
public sealed record ClaimFinalizationDraft(
    Guid FinalizationOperationId,
    RecordKind Kind,
    Guid OfficeId,
    ServiceMonth ServiceMonth,
    Guid? RootBatchId,
    ClaimExpectedHead? ExpectedHead,
    string CreatedBy,
    string OperationApplicationVersion,
    string ClaimMasterVersion,
    string CsvSpecificationVersion,
    string ReportSpecificationVersion,
    string SnapshotApplicationVersion,
    int TotalUnits,
    int TotalCostYen,
    int TotalBenefitYen,
    int TotalBurdenYen,
    IReadOnlyList<ClaimFinalizationDetailDraft> Details);

public sealed record ClaimFinalizationResult(Guid BatchId, int Revision, bool IsReplay);

public enum ClaimErrorCode
{
    InvalidOperationPayload = 1,
    InvalidSnapshotEnvelope = 2,
    UnsupportedOperationSchema = 3,
    UnsupportedSnapshotCodec = 4,
    InvalidHistory = 5,
    ExpectedHeadMismatch = 6,
    OperationIdCollision = 7,
    PersistenceFailure = 8,
}

public abstract record ClaimJsonPathSegment
{
    private ClaimJsonPathSegment() { }

    public sealed record PropertyToken : ClaimJsonPathSegment
    {
        public PropertyToken(string token)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(token);
            if (token.Any(character => character is not (>= 'A' and <= 'Z')
                    and not (>= 'a' and <= 'z') and not (>= '0' and <= '9') and not '_'))
                throw new ArgumentException("property tokenには英数字とunderscoreだけを使用できます。", nameof(token));
            Token = token;
        }

        public string Token { get; }
    }

    public sealed record ArrayIndex : ClaimJsonPathSegment
    {
        public ArrayIndex(int index)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(index);
            Index = index;
        }

        public int Index { get; }
    }
}

public sealed record ClaimJsonPath
{
    public ClaimJsonPath(IEnumerable<ClaimJsonPathSegment> segments)
    {
        ArgumentNullException.ThrowIfNull(segments);
        Segments = segments.ToArray();
    }

    public IReadOnlyList<ClaimJsonPathSegment> Segments { get; }
}

public sealed class ClaimFinalizationException(
    ClaimErrorCode code,
    ClaimJsonPath? path = null,
    Exception? innerException = null)
    : Exception($"請求確定処理に失敗しました ({code})。", innerException)
{
    public ClaimErrorCode Code { get; } = code;
    public ClaimJsonPath? Path { get; } = path;
}
