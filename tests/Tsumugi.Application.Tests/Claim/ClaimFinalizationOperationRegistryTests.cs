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
    }

    private sealed class StubOperation(string schemaVersion) : IClaimFinalizationOperation
    {
        public string SchemaVersion { get; } = schemaVersion;
        public ClaimFinalizationOperationPayload Canonicalize(
            Tsumugi.Application.Abstractions.ClaimFinalizationDraft draft) => throw new NotSupportedException();

        public ClaimFinalizationOperationPayload Rebuild(
            Tsumugi.Application.Abstractions.ClaimBatchAggregate aggregate,
            IReadOnlyList<Tsumugi.Application.Abstractions.ClaimFinalizationDetailDraft> details)
            => throw new NotSupportedException();
    }
}
