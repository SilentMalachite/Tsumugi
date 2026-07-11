using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using Tsumugi.Application.Abstractions;
using Tsumugi.Application.Claim;
using Tsumugi.Domain.Enums;
using Tsumugi.Domain.ValueObjects;

namespace Tsumugi.Application.Tests.Claim;

public sealed class ClaimFinalizationOperationV1Tests
{
    [Fact]
    public void Canonicalize_writes_fixed_order_compact_json_and_sorts_details_by_guid_text()
    {
        var draft = Draft();

        var payload = new ClaimFinalizationOperationV1().Canonicalize(draft);

        Encoding.UTF8.GetString(payload.GetCanonicalUtf8Bytes()).Should().Be(
            "{\"schemaVersion\":\"claim-finalization-operation-v1\",\"kind\":1,\"officeId\":\"10000000-0000-0000-0000-000000000000\",\"serviceMonth\":\"2026-06\",\"rootBatchId\":null,\"expectedHeadBatchId\":null,\"expectedHeadRevision\":null,\"createdBy\":\"actor\",\"operationApplicationVersion\":\"operation-app-v1\",\"claimMasterVersion\":\"master-v1\",\"csvSpecificationVersion\":\"csv-v1\",\"reportSpecificationVersion\":\"report-v1\",\"snapshotApplicationVersion\":\"snapshot-app-v1\",\"totalUnits\":3,\"totalCostYen\":30,\"totalBenefitYen\":20,\"totalBurdenYen\":10,\"details\":[{\"recipientId\":\"20000000-0000-0000-0000-000000000000\",\"snapshotSchemaVersion\":\"claim-snapshot-v1\",\"claimMasterVersion\":\"master-v1\",\"csvSpecificationVersion\":\"csv-v1\",\"reportSpecificationVersion\":\"report-v1\",\"snapshotApplicationVersion\":\"snapshot-app-v1\",\"inputSnapshotEnvelope\":{\"type\":\"input\"},\"calculationSnapshotEnvelope\":{\"type\":\"calculation\"},\"totalUnits\":3,\"totalCostYen\":30,\"benefitYen\":20,\"burdenYen\":10}]}");
        payload.Sha256.Should().Be(Convert.ToHexStringLower(
            SHA256.HashData(payload.GetCanonicalUtf8Bytes())));
    }

    [Fact]
    public void Canonicalize_rejects_non_normalized_actor()
    {
        var draft = Draft() with { CreatedBy = "e\u0301" };

        FluentActions.Invoking(() => new ClaimFinalizationOperationV1().Canonicalize(draft))
            .Should().Throw<ClaimFinalizationException>()
            .Which.Code.Should().Be(ClaimErrorCode.InvalidOperationPayload);
    }

    [Fact]
    public void Canonicalize_rejects_envelope_that_is_not_a_json_object()
    {
        var bytes = Encoding.UTF8.GetBytes("\"raw\"");
        var envelope = ValidatedClaimSnapshotEnvelope.CreateValidated(
            "claim-snapshot-v1", "test-codec-v1",
            Convert.ToHexStringLower(SHA256.HashData(bytes)), bytes, TestMarker.Instance);
        var draft = Draft() with
        {
            Details = [Draft().Details[0] with { InputSnapshotEnvelope = envelope }],
        };

        FluentActions.Invoking(() => new ClaimFinalizationOperationV1().Canonicalize(draft))
            .Should().Throw<ClaimFinalizationException>()
            .Which.Code.Should().Be(ClaimErrorCode.InvalidOperationPayload);
    }

    private static ClaimFinalizationDraft Draft() => new(
        Guid.Parse("30000000-0000-0000-0000-000000000000"),
        RecordKind.New,
        Guid.Parse("10000000-0000-0000-0000-000000000000"),
        new ServiceMonth(2026, 6),
        null,
        null,
        "actor",
        "operation-app-v1",
        "master-v1",
        "csv-v1",
        "report-v1",
        "snapshot-app-v1",
        3,
        30,
        20,
        10,
        [new ClaimFinalizationDetailDraft(
            Guid.Parse("20000000-0000-0000-0000-000000000000"),
            "claim-snapshot-v1",
            "master-v1",
            "csv-v1",
            "report-v1",
            "snapshot-app-v1",
            Envelope("{\"type\":\"input\"}"),
            Envelope("{\"type\":\"calculation\"}"),
            3,
            30,
            20,
            10)]);

    internal static ValidatedClaimSnapshotEnvelope Envelope(string json)
    {
        var bytes = Encoding.UTF8.GetBytes(json);
        return ValidatedClaimSnapshotEnvelope.CreateValidated(
            "claim-snapshot-v1",
            "test-codec-v1",
            Convert.ToHexStringLower(SHA256.HashData(bytes)),
            bytes,
            TestMarker.Instance);
    }

    internal sealed class TestMarker
    {
        internal static TestMarker Instance { get; } = new();
    }
}
