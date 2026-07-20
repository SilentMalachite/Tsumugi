using System.Security.Cryptography;
using System.Text.Json;
using Tsumugi.Application.Abstractions;

namespace Tsumugi.Application.Claim;

/// <summary>
/// snapshot envelopeのcanonical JSON表現を検証・生成するproduction codec v2。
/// v1（<c>claim-snapshot-v1</c>）からの破壊的置換（ADR 0029）。canonical bytesは非空JSON objectで、
/// 重複keyを持たず、"schemaVersion"/"validationCodecId"をこのcodec自身の識別子と一致させる。
/// <see cref="ValidatedClaimSnapshotEnvelope.PayloadSha256"/>はcanonical bytes全体のSHA-256。
/// </summary>
public sealed class ClaimSnapshotValidationCodecV2 : IClaimSnapshotValidationCodec
{
    public const string SchemaVersionValue = "claim-snapshot-v2";
    public const string ValidationCodecIdValue = "claim-snapshot-codec-v2";

    private static readonly object Marker = new();

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        AllowDuplicateProperties = false,
    };

    public string SchemaVersion => SchemaVersionValue;
    public string ValidationCodecId => ValidationCodecIdValue;
    public bool CanWrite => true;

    /// <summary>write経路（finalization/calculation双方のsnapshot writer）向け。identity・重複key・
    /// JSON object形状を検証してenvelope化する。read経路(<see cref="ReadValidated"/>)と同一の検証。
    /// このcodecインスタンスの<see cref="SchemaVersion"/>/<see cref="ValidationCodecId"/>に対して照合する。</summary>
    public ValidatedClaimSnapshotEnvelope CreateEnvelope(ReadOnlySpan<byte> canonicalUtf8)
    {
        if (canonicalUtf8.IsEmpty)
            throw new InvalidOperationException("canonical bytes が空です。");

        JsonElement root;
        try
        {
            root = JsonSerializer.Deserialize<JsonElement>(canonicalUtf8, SerializerOptions);
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException("canonical bytes の JSON parse に失敗しました。", exception);
        }

        if (root.ValueKind != JsonValueKind.Object || !root.EnumerateObject().Any())
            throw new InvalidOperationException("canonical bytes は非空の JSON object でなければなりません。");

        if (!root.TryGetProperty("schemaVersion", out var schemaProperty)
            || schemaProperty.ValueKind != JsonValueKind.String
            || schemaProperty.GetString() != SchemaVersion)
            throw new InvalidOperationException($"schemaVersion は '{SchemaVersion}' でなければなりません。");

        if (!root.TryGetProperty("validationCodecId", out var codecProperty)
            || codecProperty.ValueKind != JsonValueKind.String
            || codecProperty.GetString() != ValidationCodecId)
            throw new InvalidOperationException($"validationCodecId は '{ValidationCodecId}' でなければなりません。");

        var hash = ComputeSha256(canonicalUtf8);
        return ValidatedClaimSnapshotEnvelope.CreateValidated(
            SchemaVersion, ValidationCodecId, hash, canonicalUtf8, Marker);
    }

    public ValidatedClaimSnapshotEnvelope ReadValidated(ReadOnlyMemory<byte> canonicalUtf8)
    {
        try
        {
            return CreateEnvelope(canonicalUtf8.Span);
        }
        catch (InvalidOperationException exception)
        {
            throw new ClaimFinalizationException(ClaimErrorCode.InvalidSnapshotEnvelope, path: null, exception);
        }
    }

    public void Validate(ValidatedClaimSnapshotEnvelope envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        if (envelope.SchemaVersion != SchemaVersion
            || envelope.ValidationCodecId != ValidationCodecId)
            throw new ClaimFinalizationException(ClaimErrorCode.InvalidSnapshotEnvelope);

        var hash = ComputeSha256(envelope.GetCanonicalUtf8Bytes());
        if (!string.Equals(hash, envelope.PayloadSha256, StringComparison.Ordinal))
            throw new ClaimFinalizationException(ClaimErrorCode.InvalidSnapshotEnvelope);
    }

    private static string ComputeSha256(ReadOnlySpan<byte> bytes)
        => Convert.ToHexStringLower(SHA256.HashData(bytes));
}
