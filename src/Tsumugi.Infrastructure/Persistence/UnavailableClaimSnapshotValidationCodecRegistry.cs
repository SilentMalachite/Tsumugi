using Tsumugi.Application.Abstractions;

namespace Tsumugi.Infrastructure.Persistence;

/// <summary>Phase 3-0ではproduction snapshot codecを公開しない。</summary>
public sealed class UnavailableClaimSnapshotValidationCodecRegistry
    : IClaimSnapshotValidationCodecRegistry
{
    public bool HasWriteSupport => false;

    public IClaimSnapshotValidationCodec? Find(string schemaVersion, string validationCodecId) => null;
}
