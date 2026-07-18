using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using Tsumugi.Application.Abstractions;
using Tsumugi.Application.Claim;

namespace Tsumugi.Application.Tests.Claim;

public sealed class ClaimSnapshotValidationCodecV1Tests
{
    private static readonly ClaimSnapshotValidationCodecV1 Codec = new();

    [Fact]
    public void Codec_exposes_v1_identity_and_write_support()
    {
        Codec.SchemaVersion.Should().Be("claim-snapshot-v1");
        Codec.ValidationCodecId.Should().Be("claim-snapshot-codec-v1");
        Codec.CanWrite.Should().BeTrue();
    }

    [Fact]
    public void ReadValidated_roundtrips_canonical_json()
    {
        var bytes = CanonicalBytes(
            "{\"schemaVersion\":\"claim-snapshot-v1\",\"validationCodecId\":\"claim-snapshot-codec-v1\"," +
            "\"recipientId\":\"20000000-0000-0000-0000-000000000000\",\"totalUnits\":10}");

        var envelope = ClaimSnapshotValidationCodecV1.CreateEnvelope(bytes);
        var restored = Codec.ReadValidated(envelope.GetCanonicalUtf8Bytes());

        restored.SchemaVersion.Should().Be(envelope.SchemaVersion);
        restored.ValidationCodecId.Should().Be(envelope.ValidationCodecId);
        restored.PayloadSha256.Should().Be(envelope.PayloadSha256);
        restored.PayloadSha256.Should().Be(Convert.ToHexStringLower(SHA256.HashData(bytes)));
        restored.GetCanonicalUtf8Bytes().Should().Equal(envelope.GetCanonicalUtf8Bytes());
    }

    [Fact]
    public void CreateEnvelope_is_usable_without_an_instance()
    {
        var bytes = CanonicalBytes(MinimalEnvelopeJson());

        var envelope = ClaimSnapshotValidationCodecV1.CreateEnvelope(bytes);

        envelope.SchemaVersion.Should().Be("claim-snapshot-v1");
        envelope.ValidationCodecId.Should().Be("claim-snapshot-codec-v1");
    }

    [Fact]
    public void ReadValidated_rejects_non_json_bytes()
    {
        var bytes = Encoding.UTF8.GetBytes("not-json-at-all");

        FluentActions.Invoking(() => Codec.ReadValidated(bytes))
            .Should().Throw<ClaimFinalizationException>()
            .Which.Code.Should().Be(ClaimErrorCode.InvalidSnapshotEnvelope);
    }

    [Fact]
    public void ReadValidated_rejects_empty_bytes()
    {
        FluentActions.Invoking(() => Codec.ReadValidated(ReadOnlyMemory<byte>.Empty))
            .Should().Throw<ClaimFinalizationException>()
            .Which.Code.Should().Be(ClaimErrorCode.InvalidSnapshotEnvelope);
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("\"just-a-string\"")]
    [InlineData("123")]
    [InlineData("true")]
    public void ReadValidated_rejects_non_object_payloads(string json)
    {
        var bytes = CanonicalBytes(json);

        FluentActions.Invoking(() => Codec.ReadValidated(bytes))
            .Should().Throw<ClaimFinalizationException>()
            .Which.Code.Should().Be(ClaimErrorCode.InvalidSnapshotEnvelope);
    }

    [Fact]
    public void ReadValidated_rejects_empty_object_payload()
    {
        var bytes = CanonicalBytes("{}");

        FluentActions.Invoking(() => Codec.ReadValidated(bytes))
            .Should().Throw<ClaimFinalizationException>()
            .Which.Code.Should().Be(ClaimErrorCode.InvalidSnapshotEnvelope);
    }

    [Fact]
    public void ReadValidated_rejects_duplicate_top_level_properties()
    {
        var bytes = CanonicalBytes(
            "{\"schemaVersion\":\"claim-snapshot-v1\",\"schemaVersion\":\"claim-snapshot-v1\"," +
            "\"validationCodecId\":\"claim-snapshot-codec-v1\"}");

        FluentActions.Invoking(() => Codec.ReadValidated(bytes))
            .Should().Throw<ClaimFinalizationException>()
            .Which.Code.Should().Be(ClaimErrorCode.InvalidSnapshotEnvelope);
    }

    [Theory]
    [InlineData("{\"schemaVersion\":\"wrong-schema\",\"validationCodecId\":\"claim-snapshot-codec-v1\"}")]
    [InlineData("{\"schemaVersion\":\"claim-snapshot-v1\",\"validationCodecId\":\"wrong-codec\"}")]
    [InlineData("{\"validationCodecId\":\"claim-snapshot-codec-v1\"}")]
    [InlineData("{\"schemaVersion\":\"claim-snapshot-v1\"}")]
    public void ReadValidated_rejects_identity_mismatch_or_missing_fields(string json)
    {
        var bytes = CanonicalBytes(json);

        FluentActions.Invoking(() => Codec.ReadValidated(bytes))
            .Should().Throw<ClaimFinalizationException>()
            .Which.Code.Should().Be(ClaimErrorCode.InvalidSnapshotEnvelope);
    }

    [Fact]
    public void Validate_accepts_an_envelope_produced_by_this_codec()
    {
        var envelope = ClaimSnapshotValidationCodecV1.CreateEnvelope(CanonicalBytes(MinimalEnvelopeJson()));

        FluentActions.Invoking(() => Codec.Validate(envelope)).Should().NotThrow();
    }

    [Fact]
    public void Validate_rejects_tampered_payload_hash()
    {
        var bytes = CanonicalBytes(MinimalEnvelopeJson());
        var forged = ValidatedClaimSnapshotEnvelope.CreateValidated(
            "claim-snapshot-v1",
            "claim-snapshot-codec-v1",
            new string('0', 64),
            bytes,
            new object());

        FluentActions.Invoking(() => Codec.Validate(forged))
            .Should().Throw<ClaimFinalizationException>()
            .Which.Code.Should().Be(ClaimErrorCode.InvalidSnapshotEnvelope);
    }

    [Theory]
    [InlineData("wrong-schema", "claim-snapshot-codec-v1")]
    [InlineData("claim-snapshot-v1", "wrong-codec")]
    public void Validate_rejects_schema_or_codec_identity_mismatch(string schemaVersion, string validationCodecId)
    {
        var bytes = CanonicalBytes(MinimalEnvelopeJson());
        var hash = Convert.ToHexStringLower(SHA256.HashData(bytes));
        var envelope = ValidatedClaimSnapshotEnvelope.CreateValidated(
            schemaVersion, validationCodecId, hash, bytes, new object());

        FluentActions.Invoking(() => Codec.Validate(envelope))
            .Should().Throw<ClaimFinalizationException>()
            .Which.Code.Should().Be(ClaimErrorCode.InvalidSnapshotEnvelope);
    }

    [Fact]
    public void Registry_exposes_v1_with_write_support()
    {
        var registry = new ProductionClaimSnapshotValidationCodecRegistry();

        registry.HasWriteSupport.Should().BeTrue();
        registry.Find("claim-snapshot-v1", "claim-snapshot-codec-v1").Should().NotBeNull();
        registry.Find("claim-snapshot-v1", "claim-snapshot-codec-v1")!.CanWrite.Should().BeTrue();
        registry.Find("unknown-schema", "claim-snapshot-codec-v1").Should().BeNull();
        registry.Find("claim-snapshot-v1", "unknown-codec").Should().BeNull();
        registry.Find("unknown-schema", "unknown-codec").Should().BeNull();
    }

    private static string MinimalEnvelopeJson()
        => "{\"schemaVersion\":\"claim-snapshot-v1\",\"validationCodecId\":\"claim-snapshot-codec-v1\"}";

    private static byte[] CanonicalBytes(string json) => Encoding.UTF8.GetBytes(json);
}
