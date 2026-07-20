using FluentAssertions;
using Tsumugi.Application.Abstractions;
using Tsumugi.Application.Claim;
using Xunit;

namespace Tsumugi.Application.Tests.Claim;

public sealed class ClaimSnapshotValidationCodecV2Tests
{
    [Fact]
    public void Identity_constants_are_v2()
    {
        ClaimSnapshotValidationCodecV2.SchemaVersionValue.Should().Be("claim-snapshot-v2");
        ClaimSnapshotValidationCodecV2.ValidationCodecIdValue.Should().Be("claim-snapshot-codec-v2");
    }

    [Fact]
    public void CreateEnvelope_produces_matching_sha256_for_same_canonical_bytes()
    {
        var codec = new ClaimSnapshotValidationCodecV2();
        var canonical = """{"schemaVersion":"claim-snapshot-v2","validationCodecId":"claim-snapshot-codec-v2","snapshotKind":"finalization"}"""u8.ToArray();
        var env1 = codec.CreateEnvelope(canonical);
        var env2 = codec.CreateEnvelope(canonical);
        env1.PayloadSha256.Should().Be(env2.PayloadSha256);
        env1.GetCanonicalUtf8Bytes().Should().Equal(env2.GetCanonicalUtf8Bytes());
    }

    [Fact]
    public void CreateEnvelope_rejects_v1_schema_version()
    {
        var codec = new ClaimSnapshotValidationCodecV2();
        var v1canonical = """{"schemaVersion":"claim-snapshot-v1","validationCodecId":"claim-snapshot-codec-v1","snapshotKind":"calculation"}"""u8.ToArray();
        var act = () => codec.CreateEnvelope(v1canonical);
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Codec_exposes_v2_identity_and_write_support()
    {
        var codec = new ClaimSnapshotValidationCodecV2();
        codec.SchemaVersion.Should().Be("claim-snapshot-v2");
        codec.ValidationCodecId.Should().Be("claim-snapshot-codec-v2");
        codec.CanWrite.Should().BeTrue();
    }

    [Fact]
    public void ReadValidated_roundtrips_canonical_json()
    {
        var codec = new ClaimSnapshotValidationCodecV2();
        var bytes = """{"schemaVersion":"claim-snapshot-v2","validationCodecId":"claim-snapshot-codec-v2","snapshotKind":"finalization"}"""u8.ToArray();

        var envelope = codec.CreateEnvelope(bytes);
        var restored = codec.ReadValidated(envelope.GetCanonicalUtf8Bytes());

        restored.SchemaVersion.Should().Be(envelope.SchemaVersion);
        restored.ValidationCodecId.Should().Be(envelope.ValidationCodecId);
        restored.PayloadSha256.Should().Be(envelope.PayloadSha256);
        restored.GetCanonicalUtf8Bytes().Should().Equal(envelope.GetCanonicalUtf8Bytes());
    }

    [Fact]
    public void ReadValidated_rejects_non_json_bytes()
    {
        var codec = new ClaimSnapshotValidationCodecV2();
        var bytes = "not-json-at-all"u8.ToArray();

        FluentActions.Invoking(() => codec.ReadValidated(bytes))
            .Should().Throw<ClaimFinalizationException>()
            .Which.Code.Should().Be(ClaimErrorCode.InvalidSnapshotEnvelope);
    }

    [Fact]
    public void ReadValidated_rejects_empty_bytes()
    {
        var codec = new ClaimSnapshotValidationCodecV2();

        FluentActions.Invoking(() => codec.ReadValidated(ReadOnlyMemory<byte>.Empty))
            .Should().Throw<ClaimFinalizationException>()
            .Which.Code.Should().Be(ClaimErrorCode.InvalidSnapshotEnvelope);
    }

    [Fact]
    public void ReadValidated_rejects_duplicate_top_level_properties()
    {
        var codec = new ClaimSnapshotValidationCodecV2();
        var bytes = """{"schemaVersion":"claim-snapshot-v2","schemaVersion":"claim-snapshot-v2","validationCodecId":"claim-snapshot-codec-v2"}"""u8.ToArray();

        FluentActions.Invoking(() => codec.ReadValidated(bytes))
            .Should().Throw<ClaimFinalizationException>()
            .Which.Code.Should().Be(ClaimErrorCode.InvalidSnapshotEnvelope);
    }

    [Fact]
    public void Validate_accepts_an_envelope_produced_by_this_codec()
    {
        var codec = new ClaimSnapshotValidationCodecV2();
        var bytes = """{"schemaVersion":"claim-snapshot-v2","validationCodecId":"claim-snapshot-codec-v2","snapshotKind":"finalization"}"""u8.ToArray();
        var envelope = codec.CreateEnvelope(bytes);

        FluentActions.Invoking(() => codec.Validate(envelope)).Should().NotThrow();
    }

    [Fact]
    public void Validate_rejects_tampered_payload_hash()
    {
        var bytes = """{"schemaVersion":"claim-snapshot-v2","validationCodecId":"claim-snapshot-codec-v2","snapshotKind":"finalization"}"""u8.ToArray();
        var forged = ValidatedClaimSnapshotEnvelope.CreateValidated(
            "claim-snapshot-v2",
            "claim-snapshot-codec-v2",
            new string('0', 64),
            bytes,
            new object());

        var codec = new ClaimSnapshotValidationCodecV2();
        FluentActions.Invoking(() => codec.Validate(forged))
            .Should().Throw<ClaimFinalizationException>()
            .Which.Code.Should().Be(ClaimErrorCode.InvalidSnapshotEnvelope);
    }

    [Fact]
    public void Registry_exposes_v2_with_write_support_and_rejects_v1()
    {
        var registry = new ProductionClaimSnapshotValidationCodecRegistry();

        registry.HasWriteSupport.Should().BeTrue();
        registry.Find("claim-snapshot-v2", "claim-snapshot-codec-v2").Should().NotBeNull();
        registry.Find("claim-snapshot-v2", "claim-snapshot-codec-v2")!.CanWrite.Should().BeTrue();
        registry.Find("claim-snapshot-v1", "claim-snapshot-codec-v1").Should().BeNull();
        registry.Find("unknown-schema", "unknown-codec").Should().BeNull();
    }
}
