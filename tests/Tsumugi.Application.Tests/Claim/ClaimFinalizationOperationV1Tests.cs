using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using Tsumugi.Application.Abstractions;
using Tsumugi.Application.Claim;
using Tsumugi.Domain.Entities;
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

    [Theory]
    [InlineData("A\u0309")]
    [InlineData("A\u0323")]
    [InlineData("\u1100\u1161")]
    [InlineData("A\u0323\u0309")]
    public void Canonicalize_rejects_non_NFC_actor_across_unicode(string actor)
    {
        var draft = Draft() with { CreatedBy = actor };

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

    [Theory]
    [InlineData("operationApplicationVersion")]
    [InlineData("claimMasterVersion")]
    [InlineData("csvSpecificationVersion")]
    [InlineData("reportSpecificationVersion")]
    [InlineData("snapshotApplicationVersion")]
    [InlineData("expectedHeadBatchId")]
    [InlineData("expectedHeadRevision")]
    [InlineData("totalUnits")]
    [InlineData("totalCostYen")]
    [InlineData("totalBenefitYen")]
    [InlineData("totalBurdenYen")]
    [InlineData("inputEnvelopeBytes")]
    [InlineData("calculationEnvelopeBytes")]
    public void Canonicalize_hash_changes_for_each_operation_input(string field)
    {
        var draft = Draft() with
        {
            RootBatchId = Guid.Parse("40000000-0000-0000-0000-000000000000"),
            ExpectedHead = new ClaimExpectedHead(
                Guid.Parse("50000000-0000-0000-0000-000000000000"), 7),
        };
        var changed = WithHashField(draft, field);
        var operation = new ClaimFinalizationOperationV1();

        var original = operation.Canonicalize(draft);
        var modified = operation.Canonicalize(changed);

        modified.Sha256.Should().NotBe(original.Sha256);
        modified.GetCanonicalUtf8Bytes().Should().NotEqual(original.GetCanonicalUtf8Bytes());
    }

    [Fact]
    public void Rebuild_excludes_persisted_ids_created_at_and_revision_from_operation_payload()
    {
        var draft = Draft();
        var operation = new ClaimFinalizationOperationV1();
        var first = Aggregate(
            draft,
            Guid.Parse("60000000-0000-0000-0000-000000000000"),
            Guid.Parse("70000000-0000-0000-0000-000000000000"),
            DateTimeOffset.UnixEpoch,
            revision: 1);
        var second = Aggregate(
            draft,
            Guid.Parse("80000000-0000-0000-0000-000000000000"),
            Guid.Parse("90000000-0000-0000-0000-000000000000"),
            DateTimeOffset.UnixEpoch.AddYears(10),
            revision: 99);

        var firstPayload = operation.Rebuild(first, draft.Details);
        var secondPayload = operation.Rebuild(second, draft.Details);

        secondPayload.Sha256.Should().Be(firstPayload.Sha256);
        secondPayload.GetCanonicalUtf8Bytes().Should().Equal(firstPayload.GetCanonicalUtf8Bytes());
    }

    [Fact]
    public void Rebuild_from_persisted_aggregate_matches_original_bytes_and_hash()
    {
        var draft = Draft();
        var operation = new ClaimFinalizationOperationV1();
        var original = operation.Canonicalize(draft);
        var aggregate = Aggregate(
            draft,
            Guid.Parse("60000000-0000-0000-0000-000000000000"),
            Guid.Parse("70000000-0000-0000-0000-000000000000"),
            DateTimeOffset.UnixEpoch,
            revision: 1);

        var rebuilt = operation.Rebuild(aggregate, draft.Details);

        rebuilt.Sha256.Should().Be(original.Sha256);
        rebuilt.GetCanonicalUtf8Bytes().Should().Equal(original.GetCanonicalUtf8Bytes());
    }

    internal static ClaimFinalizationDraft Draft() => new(
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

    private static ClaimFinalizationDraft WithHashField(
        ClaimFinalizationDraft draft,
        string field) => field switch
        {
            "operationApplicationVersion" => draft with { OperationApplicationVersion = "operation-app-v2" },
            "claimMasterVersion" => draft with { ClaimMasterVersion = "master-v2" },
            "csvSpecificationVersion" => draft with { CsvSpecificationVersion = "csv-v2" },
            "reportSpecificationVersion" => draft with { ReportSpecificationVersion = "report-v2" },
            "snapshotApplicationVersion" => draft with { SnapshotApplicationVersion = "snapshot-app-v2" },
            "expectedHeadBatchId" => draft with
            {
                ExpectedHead = draft.ExpectedHead! with { BatchId = Guid.NewGuid() },
            },
            "expectedHeadRevision" => draft with
            {
                ExpectedHead = draft.ExpectedHead! with { Revision = draft.ExpectedHead.Revision + 1 },
            },
            "totalUnits" => draft with { TotalUnits = draft.TotalUnits + 1 },
            "totalCostYen" => draft with { TotalCostYen = draft.TotalCostYen + 1 },
            "totalBenefitYen" => draft with { TotalBenefitYen = draft.TotalBenefitYen + 1 },
            "totalBurdenYen" => draft with { TotalBurdenYen = draft.TotalBurdenYen + 1 },
            "inputEnvelopeBytes" => WithDetail(
                draft,
                draft.Details[0] with { InputSnapshotEnvelope = Envelope("{\"type\":\"input-v2\"}") }),
            "calculationEnvelopeBytes" => WithDetail(
                draft,
                draft.Details[0] with
                {
                    CalculationSnapshotEnvelope = Envelope("{\"type\":\"calculation-v2\"}"),
                }),
            _ => throw new ArgumentOutOfRangeException(nameof(field)),
        };

    private static ClaimBatchAggregate Aggregate(
        ClaimFinalizationDraft draft,
        Guid batchId,
        Guid detailId,
        DateTimeOffset createdAt,
        int revision)
    {
        var payload = new ClaimFinalizationOperationV1().Canonicalize(draft);
        var batch = ClaimBatch.NewRecord(
            batchId,
            draft.OfficeId,
            draft.ServiceMonth,
            draft.TotalUnits,
            draft.TotalCostYen,
            draft.TotalBenefitYen,
            draft.TotalBurdenYen,
            draft.ClaimMasterVersion,
            draft.CsvSpecificationVersion,
            draft.ReportSpecificationVersion,
            draft.SnapshotApplicationVersion,
            draft.OperationApplicationVersion,
            draft.FinalizationOperationId,
            ClaimFinalizationOperationV1.SchemaVersion,
            payload.Sha256,
            draft.CreatedBy,
            createdAt) with
        {
            Revision = revision,
        };
        var detailDraft = draft.Details[0];
        var detail = ClaimDetail.Create(
            detailId,
            batchId,
            detailDraft.RecipientId,
            detailDraft.SnapshotSchemaVersion,
            detailDraft.ClaimMasterVersion,
            detailDraft.CsvSpecificationVersion,
            detailDraft.ReportSpecificationVersion,
            detailDraft.SnapshotApplicationVersion,
            Encoding.UTF8.GetString(detailDraft.InputSnapshotEnvelope.GetCanonicalUtf8Bytes()),
            Encoding.UTF8.GetString(detailDraft.CalculationSnapshotEnvelope.GetCanonicalUtf8Bytes()),
            detailDraft.TotalUnits,
            detailDraft.TotalCostYen,
            detailDraft.BenefitYen,
            detailDraft.BurdenYen,
            draft.CreatedBy,
            createdAt);
        return new ClaimBatchAggregate(batch, [detail]);
    }

    private static ClaimFinalizationDraft WithDetail(
        ClaimFinalizationDraft draft,
        ClaimFinalizationDetailDraft detail) => draft with { Details = [detail] };

    internal sealed class TestMarker
    {
        internal static TestMarker Instance { get; } = new();
    }
}
