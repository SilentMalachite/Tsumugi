using Tsumugi.Application.Claim;

namespace Tsumugi.Application.Abstractions;

public interface IClaimSnapshotValidationCodecRegistry
{
    bool HasWriteSupport { get; }
    IClaimSnapshotValidationCodec? Find(string schemaVersion, string validationCodecId);
}

public interface IClaimSnapshotValidationCodec
{
    string SchemaVersion { get; }
    string ValidationCodecId { get; }
    bool CanWrite { get; }

    void Validate(ValidatedClaimSnapshotEnvelope envelope);
    ValidatedClaimSnapshotEnvelope ReadValidated(ReadOnlyMemory<byte> canonicalUtf8);
}
