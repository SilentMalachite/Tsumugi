using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using Tsumugi.Application.Claim;

namespace Tsumugi.Application.Tests.Claim;

public sealed class ValidatedClaimSnapshotEnvelopeTests
{
    [Fact]
    public void Envelope_is_opaque_and_deep_copies_canonical_bytes()
    {
        var bytes = Encoding.UTF8.GetBytes("{\"schemaVersion\":\"claim-snapshot-v1\",\"payload\":{}}");
        var hash = Convert.ToHexStringLower(SHA256.HashData(bytes));
        var marker = new object();

        var envelope = ValidatedClaimSnapshotEnvelope.CreateValidated(
            "claim-snapshot-v1", "test-codec-v1", hash, bytes, marker);
        bytes[0] = (byte)'!';
        var first = envelope.GetCanonicalUtf8Bytes();
        first[0] = (byte)'?';

        envelope.GetCanonicalUtf8Bytes()[0].Should().Be((byte)'{');
        envelope.SchemaVersion.Should().Be("claim-snapshot-v1");
        envelope.ValidationCodecId.Should().Be("test-codec-v1");
        envelope.PayloadSha256.Should().Be(hash);
        envelope.ValidationMarker.Should().BeSameAs(marker);
    }

    [Fact]
    public void Envelope_has_no_public_constructor_or_raw_factory()
    {
        typeof(ValidatedClaimSnapshotEnvelope).GetConstructors().Should().BeEmpty();
        typeof(ValidatedClaimSnapshotEnvelope).GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Should().BeEmpty();
    }
}
