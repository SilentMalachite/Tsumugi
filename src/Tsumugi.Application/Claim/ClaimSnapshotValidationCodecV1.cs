using System.Security.Cryptography;
using System.Text.Json;
using Tsumugi.Application.Abstractions;

namespace Tsumugi.Application.Claim;

/// <summary>
/// snapshot envelopeのcanonical JSON表現を検証・生成するproduction codec v1。
/// canonical bytesは非空JSON objectで、重複keyを持たず、"schemaVersion"/"validationCodecId"を
/// このcodec自身の識別子と一致させる。<see cref="ValidatedClaimSnapshotEnvelope.PayloadSha256"/>は
/// canonical bytes全体のSHA-256。
/// </summary>
public sealed class ClaimSnapshotValidationCodecV1 : IClaimSnapshotValidationCodec
{
    public const string SchemaVersionValue = "claim-snapshot-v1";
    public const string ValidationCodecIdValue = "claim-snapshot-codec-v1";

    private static readonly object Marker = new();

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        AllowDuplicateProperties = false,
    };

    public string SchemaVersion => SchemaVersionValue;
    public string ValidationCodecId => ValidationCodecIdValue;
    public bool CanWrite => true;

    /// <summary>write経路(Task 9のClose UseCase)向け。read経路(<see cref="ReadValidated"/>)と同一の検証を行う。</summary>
    public static ValidatedClaimSnapshotEnvelope CreateEnvelope(ReadOnlySpan<byte> canonicalUtf8)
        => Parse(canonicalUtf8);

    public ValidatedClaimSnapshotEnvelope ReadValidated(ReadOnlyMemory<byte> canonicalUtf8)
        => Parse(canonicalUtf8.Span);

    public void Validate(ValidatedClaimSnapshotEnvelope envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        if (envelope.SchemaVersion != SchemaVersionValue
            || envelope.ValidationCodecId != ValidationCodecIdValue)
            throw Error();

        var hash = ComputeSha256(envelope.GetCanonicalUtf8Bytes());
        if (!string.Equals(hash, envelope.PayloadSha256, StringComparison.Ordinal))
            throw Error();
    }

    private static ValidatedClaimSnapshotEnvelope Parse(ReadOnlySpan<byte> canonicalUtf8)
    {
        JsonElement root;
        try
        {
            root = JsonSerializer.Deserialize<JsonElement>(canonicalUtf8, SerializerOptions);
        }
        catch (JsonException exception)
        {
            throw Error(exception);
        }

        if (root.ValueKind != JsonValueKind.Object || !root.EnumerateObject().Any())
            throw Error();

        if (!root.TryGetProperty("schemaVersion", out var schemaProperty)
            || schemaProperty.ValueKind != JsonValueKind.String
            || schemaProperty.GetString() != SchemaVersionValue
            || !root.TryGetProperty("validationCodecId", out var codecProperty)
            || codecProperty.ValueKind != JsonValueKind.String
            || codecProperty.GetString() != ValidationCodecIdValue)
            throw Error();

        var hash = ComputeSha256(canonicalUtf8);
        return ValidatedClaimSnapshotEnvelope.CreateValidated(
            SchemaVersionValue, ValidationCodecIdValue, hash, canonicalUtf8, Marker);
    }

    private static string ComputeSha256(ReadOnlySpan<byte> bytes)
        => Convert.ToHexStringLower(SHA256.HashData(bytes));

    private static ClaimFinalizationException Error(Exception? inner = null)
        => inner is null
            ? new ClaimFinalizationException(ClaimErrorCode.InvalidSnapshotEnvelope)
            : new ClaimFinalizationException(ClaimErrorCode.InvalidSnapshotEnvelope, path: null, inner);
}
