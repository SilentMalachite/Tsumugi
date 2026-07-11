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

    [Fact]
    public void Canonicalize_accepts_NFC_actor_with_nonspacing_mark_that_has_no_composed_form()
    {
        var draft = Draft() with { CreatedBy = "a\u0338" };

        FluentActions.Invoking(() => new ClaimFinalizationOperationV1().Canonicalize(draft))
            .Should().NotThrow();
    }

    [Fact]
    public void Canonicalize_accepts_all_bounded_fields_at_64_characters()
    {
        var value = new string('a', 64);
        var envelope = Envelope("{}", value, value);
        var draft = Draft() with
        {
            CreatedBy = value,
            OperationApplicationVersion = value,
            ClaimMasterVersion = value,
            CsvSpecificationVersion = value,
            ReportSpecificationVersion = value,
            SnapshotApplicationVersion = value,
            Details = [Draft().Details[0] with
            {
                SnapshotSchemaVersion = value,
                ClaimMasterVersion = value,
                CsvSpecificationVersion = value,
                ReportSpecificationVersion = value,
                SnapshotApplicationVersion = value,
                InputSnapshotEnvelope = envelope,
                CalculationSnapshotEnvelope = envelope,
            }],
        };

        FluentActions.Invoking(() => new ClaimFinalizationOperationV1().Canonicalize(draft))
            .Should().NotThrow();
    }

    [Theory]
    [InlineData("createdBy")]
    [InlineData("operationApplicationVersion")]
    [InlineData("claimMasterVersion")]
    [InlineData("csvSpecificationVersion")]
    [InlineData("reportSpecificationVersion")]
    [InlineData("snapshotApplicationVersion")]
    [InlineData("detailSnapshotSchemaVersion")]
    [InlineData("detailClaimMasterVersion")]
    [InlineData("detailCsvSpecificationVersion")]
    [InlineData("detailReportSpecificationVersion")]
    [InlineData("detailSnapshotApplicationVersion")]
    [InlineData("envelopeSchemaVersion")]
    [InlineData("validationCodecId")]
    public void Canonicalize_rejects_each_bounded_field_at_65_characters(string field)
    {
        var value = new string('a', 65);
        var draft = WithField(Draft(), field, value);

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

    internal static ValidatedClaimSnapshotEnvelope Envelope(
        string json,
        string schemaVersion = "claim-snapshot-v1",
        string validationCodecId = "test-codec-v1")
    {
        var bytes = Encoding.UTF8.GetBytes(json);
        return ValidatedClaimSnapshotEnvelope.CreateValidated(
            schemaVersion,
            validationCodecId,
            Convert.ToHexStringLower(SHA256.HashData(bytes)),
            bytes,
            TestMarker.Instance);
    }

    private static ClaimFinalizationDraft WithField(
        ClaimFinalizationDraft draft,
        string field,
        string value) => field switch
        {
            "createdBy" => draft with { CreatedBy = value },
            "operationApplicationVersion" => draft with { OperationApplicationVersion = value },
            "claimMasterVersion" => draft with { ClaimMasterVersion = value },
            "csvSpecificationVersion" => draft with { CsvSpecificationVersion = value },
            "reportSpecificationVersion" => draft with { ReportSpecificationVersion = value },
            "snapshotApplicationVersion" => draft with { SnapshotApplicationVersion = value },
            "detailSnapshotSchemaVersion" => WithDetail(
                draft, draft.Details[0] with { SnapshotSchemaVersion = value }),
            "detailClaimMasterVersion" => WithDetail(
                draft, draft.Details[0] with { ClaimMasterVersion = value }),
            "detailCsvSpecificationVersion" => WithDetail(
                draft, draft.Details[0] with { CsvSpecificationVersion = value }),
            "detailReportSpecificationVersion" => WithDetail(
                draft, draft.Details[0] with { ReportSpecificationVersion = value }),
            "detailSnapshotApplicationVersion" => WithDetail(
                draft, draft.Details[0] with { SnapshotApplicationVersion = value }),
            "envelopeSchemaVersion" => WithDetail(
                draft,
                draft.Details[0] with
                {
                    InputSnapshotEnvelope = Envelope("{}", value, "test-codec-v1"),
                }),
            "validationCodecId" => WithDetail(
                draft,
                draft.Details[0] with
                {
                    InputSnapshotEnvelope = Envelope("{}", "claim-snapshot-v1", value),
                }),
            _ => throw new ArgumentOutOfRangeException(nameof(field)),
        };

    private static ClaimFinalizationDraft WithDetail(
        ClaimFinalizationDraft draft,
        ClaimFinalizationDetailDraft detail) => draft with { Details = [detail] };

    internal sealed class TestMarker
    {
        internal static TestMarker Instance { get; } = new();
    }
}
