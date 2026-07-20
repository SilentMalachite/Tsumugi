using Tsumugi.Application.Abstractions;

namespace Tsumugi.Application.Claim;

/// <summary>production snapshot codec registry。現行v2のみを書込み可能として公開する。</summary>
public sealed class ProductionClaimSnapshotValidationCodecRegistry : IClaimSnapshotValidationCodecRegistry
{
    private readonly ClaimSnapshotValidationCodecV2 _codecV2 = new();

    public bool HasWriteSupport => true;

    public IClaimSnapshotValidationCodec? Find(string schemaVersion, string validationCodecId)
        => schemaVersion == _codecV2.SchemaVersion && validationCodecId == _codecV2.ValidationCodecId
            ? _codecV2
            : null;
}
