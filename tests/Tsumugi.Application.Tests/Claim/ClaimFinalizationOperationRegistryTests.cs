using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using Tsumugi.Application.Claim;

namespace Tsumugi.Application.Tests.Claim;

public sealed class ClaimFinalizationOperationRegistryTests
{
    [Fact]
    public void Operation_contract_exposes_versioned_aggregate_rebuild()
    {
        typeof(IClaimFinalizationOperation).GetMethod(nameof(ClaimFinalizationOperationV1.Rebuild))
            .Should().NotBeNull();
    }

    [Fact]
    public void Read_support_remains_available_when_v1_write_is_disabled()
    {
        var v1 = new ClaimFinalizationOperationV1();
        var v2 = new StubOperation("claim-finalization-operation-v2");
        var registry = new ClaimFinalizationOperationRegistry([
            new ClaimFinalizationOperationEntry(v1, CanWrite: false),
            new ClaimFinalizationOperationEntry(v2, CanWrite: true),
        ]);

        registry.GetReadEntry(ClaimFinalizationOperationV1.SchemaVersion)!.Operation.Should().BeSameAs(v1);
        registry.GetWriteEntry(ClaimFinalizationOperationV1.SchemaVersion).Should().BeNull();
        registry.GetWriteEntry(v2.SchemaVersion)!.Operation.Should().BeSameAs(v2);
        var draft = ClaimFinalizationOperationV1Tests.Draft();
        var v1Bytes = v1.Canonicalize(draft).GetCanonicalUtf8Bytes();
        var v2Bytes = registry.GetWriteEntry(v2.SchemaVersion)!.Operation
            .Canonicalize(draft)
            .GetCanonicalUtf8Bytes();
        Encoding.UTF8.GetString(v2Bytes).Should().Be(
            "{\"schemaVersion\":\"claim-finalization-operation-v2\"}");
        v2Bytes.Should().NotEqual(v1Bytes);
    }

    private sealed class StubOperation(string schemaVersion) : IClaimFinalizationOperation
    {
        public string SchemaVersion { get; } = schemaVersion;
        public ClaimFinalizationOperationPayload Canonicalize(
            Tsumugi.Application.Abstractions.ClaimFinalizationDraft draft)
        {
            var bytes = Encoding.UTF8.GetBytes($"{{\"schemaVersion\":\"{SchemaVersion}\"}}");
            return new ClaimFinalizationOperationPayload(
                bytes,
                Convert.ToHexStringLower(SHA256.HashData(bytes)));
        }

        public ClaimFinalizationOperationPayload Rebuild(
            Tsumugi.Application.Abstractions.ClaimBatchAggregate aggregate,
            IReadOnlyList<Tsumugi.Application.Abstractions.ClaimFinalizationDetailDraft> details)
            => Canonicalize(null!);
    }
}
