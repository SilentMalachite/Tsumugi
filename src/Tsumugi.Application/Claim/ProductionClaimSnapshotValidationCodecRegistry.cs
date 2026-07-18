using Tsumugi.Application.Abstractions;

namespace Tsumugi.Application.Claim;

/// <summary>production snapshot codec registry。現行v1のみを書込み可能として公開する。</summary>
public sealed class ProductionClaimSnapshotValidationCodecRegistry : IClaimSnapshotValidationCodecRegistry
{
    private readonly ClaimSnapshotValidationCodecV1 _codecV1 = new();

    public bool HasWriteSupport => true;

    public IClaimSnapshotValidationCodec? Find(string schemaVersion, string validationCodecId)
        => schemaVersion == _codecV1.SchemaVersion && validationCodecId == _codecV1.ValidationCodecId
            ? _codecV1
            : null;
}
