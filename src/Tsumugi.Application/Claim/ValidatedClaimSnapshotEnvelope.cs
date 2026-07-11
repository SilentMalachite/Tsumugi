namespace Tsumugi.Application.Claim;

/// <summary>versioned codecが検証したcanonical snapshot envelope。raw生成経路はassembly外へ公開しない。</summary>
public sealed class ValidatedClaimSnapshotEnvelope
{
    private readonly byte[] _canonicalUtf8;

    private ValidatedClaimSnapshotEnvelope(
        string schemaVersion,
        string validationCodecId,
        string payloadSha256,
        ReadOnlySpan<byte> canonicalUtf8,
        object validationMarker)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(schemaVersion);
        ArgumentException.ThrowIfNullOrWhiteSpace(validationCodecId);
        ArgumentException.ThrowIfNullOrWhiteSpace(payloadSha256);
        ArgumentNullException.ThrowIfNull(validationMarker);
        SchemaVersion = schemaVersion;
        ValidationCodecId = validationCodecId;
        PayloadSha256 = payloadSha256;
        _canonicalUtf8 = canonicalUtf8.ToArray();
        ValidationMarker = validationMarker;
    }

    public string SchemaVersion { get; }
    public string ValidationCodecId { get; }
    public string PayloadSha256 { get; }

    internal object ValidationMarker { get; }

    public byte[] GetCanonicalUtf8Bytes() => [.. _canonicalUtf8];

    internal static ValidatedClaimSnapshotEnvelope CreateValidated(
        string schemaVersion,
        string validationCodecId,
        string payloadSha256,
        ReadOnlySpan<byte> canonicalUtf8,
        object validationMarker) => new(
            schemaVersion,
            validationCodecId,
            payloadSha256,
            canonicalUtf8,
            validationMarker);
}
