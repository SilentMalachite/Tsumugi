using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Tsumugi.Application.Abstractions;

namespace Tsumugi.Application.Claim;

public sealed class ClaimFinalizationOperationV1 : IClaimFinalizationOperation
{
    public const string SchemaVersion = "claim-finalization-operation-v1";

    string IClaimFinalizationOperation.SchemaVersion => SchemaVersion;

    public ClaimFinalizationOperationPayload Canonicalize(ClaimFinalizationDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        Validate(draft);

        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            Indented = false,
        }))
        {
            writer.WriteStartObject();
            writer.WriteString("schemaVersion", SchemaVersion);
            writer.WriteNumber("kind", (int)draft.Kind);
            writer.WriteString("officeId", FormatGuid(draft.OfficeId));
            writer.WriteString("serviceMonth", draft.ServiceMonth.ToString());
            WriteGuidOrNull(writer, "rootBatchId", draft.RootBatchId);
            WriteGuidOrNull(writer, "expectedHeadBatchId", draft.ExpectedHead?.BatchId);
            if (draft.ExpectedHead is null) writer.WriteNull("expectedHeadRevision");
            else writer.WriteNumber("expectedHeadRevision", draft.ExpectedHead.Revision);
            writer.WriteString("createdBy", draft.CreatedBy);
            writer.WriteString("operationApplicationVersion", draft.OperationApplicationVersion);
            writer.WriteString("claimMasterVersion", draft.ClaimMasterVersion);
            writer.WriteString("csvSpecificationVersion", draft.CsvSpecificationVersion);
            writer.WriteString("reportSpecificationVersion", draft.ReportSpecificationVersion);
            writer.WriteString("snapshotApplicationVersion", draft.SnapshotApplicationVersion);
            writer.WriteNumber("totalUnits", draft.TotalUnits);
            writer.WriteNumber("totalCostYen", draft.TotalCostYen);
            writer.WriteNumber("totalBenefitYen", draft.TotalBenefitYen);
            writer.WriteNumber("totalBurdenYen", draft.TotalBurdenYen);
            writer.WriteStartArray("details");

            foreach (var detail in draft.Details.OrderBy(
                         item => FormatGuid(item.RecipientId),
                         StringComparer.Ordinal))
            {
                writer.WriteStartObject();
                writer.WriteString("recipientId", FormatGuid(detail.RecipientId));
                writer.WriteString("snapshotSchemaVersion", detail.SnapshotSchemaVersion);
                writer.WriteString("claimMasterVersion", detail.ClaimMasterVersion);
                writer.WriteString("csvSpecificationVersion", detail.CsvSpecificationVersion);
                writer.WriteString("reportSpecificationVersion", detail.ReportSpecificationVersion);
                writer.WriteString("snapshotApplicationVersion", detail.SnapshotApplicationVersion);
                writer.WritePropertyName("inputSnapshotEnvelope");
                writer.WriteRawValue(detail.InputSnapshotEnvelope.GetCanonicalUtf8Bytes());
                writer.WritePropertyName("calculationSnapshotEnvelope");
                writer.WriteRawValue(detail.CalculationSnapshotEnvelope.GetCanonicalUtf8Bytes());
                writer.WriteNumber("totalUnits", detail.TotalUnits);
                writer.WriteNumber("totalCostYen", detail.TotalCostYen);
                writer.WriteNumber("benefitYen", detail.BenefitYen);
                writer.WriteNumber("burdenYen", detail.BurdenYen);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        var bytes = stream.ToArray();
        return new ClaimFinalizationOperationPayload(
            bytes,
            Convert.ToHexStringLower(SHA256.HashData(bytes)));
    }

    public ClaimFinalizationOperationPayload Rebuild(
        ClaimBatchAggregate aggregate,
        IReadOnlyList<ClaimFinalizationDetailDraft> details)
    {
        ArgumentNullException.ThrowIfNull(aggregate);
        var batch = aggregate.Header;
        return Canonicalize(new ClaimFinalizationDraft(
            batch.FinalizationOperationId,
            batch.Kind,
            batch.OfficeId,
            batch.ServiceMonth,
            batch.OriginId,
            batch.ExpectedHeadBatchId is null
                ? null
                : new ClaimExpectedHead(batch.ExpectedHeadBatchId.Value, batch.ExpectedHeadRevision!.Value),
            batch.CreatedBy,
            batch.OperationApplicationVersion,
            batch.ClaimMasterVersion,
            batch.CsvSpecificationVersion,
            batch.ReportSpecificationVersion,
            batch.SnapshotApplicationVersion,
            batch.TotalUnits,
            batch.TotalCostYen,
            batch.TotalBenefitYen,
            batch.TotalBurdenYen,
            details));
    }

    private static void Validate(ClaimFinalizationDraft draft)
    {
        if (!draft.CreatedBy.IsNormalized(NormalizationForm.FormC)
            || draft.CreatedBy.Any(character =>
                CharUnicodeInfo.GetUnicodeCategory(character) is UnicodeCategory.NonSpacingMark)
            || draft.FinalizationOperationId == Guid.Empty
            || draft.OfficeId == Guid.Empty
            || draft.Details is null
            || !Ascii(draft.OperationApplicationVersion)
            || !Ascii(draft.ClaimMasterVersion)
            || !Ascii(draft.CsvSpecificationVersion)
            || !Ascii(draft.ReportSpecificationVersion)
            || !Ascii(draft.SnapshotApplicationVersion)
            || draft.Details.Any(detail => !Ascii(detail.SnapshotSchemaVersion)
                || !Ascii(detail.ClaimMasterVersion)
                || !Ascii(detail.CsvSpecificationVersion)
                || !Ascii(detail.ReportSpecificationVersion)
                || !Ascii(detail.SnapshotApplicationVersion)
                || !IsJsonObject(detail.InputSnapshotEnvelope)
                || !IsJsonObject(detail.CalculationSnapshotEnvelope)))
            throw new ClaimFinalizationException(ClaimErrorCode.InvalidOperationPayload);
    }

    private static bool IsJsonObject(ValidatedClaimSnapshotEnvelope envelope)
    {
        try
        {
            var bytes = envelope.GetCanonicalUtf8Bytes();
            var reader = new Utf8JsonReader(bytes);
            using var document = JsonDocument.ParseValue(ref reader);
            return document.RootElement.ValueKind == JsonValueKind.Object
                && reader.BytesConsumed == bytes.Length;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool Ascii(string value)
        => !string.IsNullOrWhiteSpace(value) && value.All(character => character <= 0x7f);

    private static string FormatGuid(Guid value) => value.ToString("D").ToLowerInvariant();

    private static void WriteGuidOrNull(Utf8JsonWriter writer, string propertyName, Guid? value)
    {
        if (value is null) writer.WriteNull(propertyName);
        else writer.WriteString(propertyName, FormatGuid(value.Value));
    }
}
