using System.Text.Encodings.Web;
using System.Text.Json;
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.Logic.Claim;
using Tsumugi.Domain.Logic.Claim.Models;
using Tsumugi.Domain.ValueObjects;

namespace Tsumugi.Application.Claim;

/// <summary>
/// 受給者単位の入力/算定snapshotをcanonical UTF-8 JSONとして決定論的に生成する
/// （プロパティ順は本コードの記述順で固定・インデントなし・数値/GUID/月はculture非依存整形）。
/// 生成bytesは<see cref="ClaimSnapshotValidationCodecV1.CreateEnvelope"/>で検証してenvelope化する。
/// 同一入力→同一bytes→同一hashがPreviewHash契約の土台になる。
/// </summary>
public static class ClaimRecipientSnapshotWriter
{
    public static byte[] WriteInputSnapshot(
        ServiceMonth serviceMonth,
        ClaimCalculationRequest request,
        RecipientClaimSource source,
        ClaimInput? claimInput)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(source);

        using var stream = new MemoryStream();
        using (var writer = CreateWriter(stream))
        {
            writer.WriteStartObject();
            WriteEnvelopeIdentity(writer, "input");
            writer.WriteString("recipientId", FormatGuid(source.RecipientId));
            writer.WriteString("serviceMonth", serviceMonth.ToString());
            writer.WriteNumber("billedDays", source.BilledDays);
            writer.WriteNumber("benefitRatePercent", source.BenefitRatePercent);
            writer.WriteNumber("certificateMonthlyCapYen", source.CertificateMonthlyCapYen);

            writer.WriteStartObject("conditions");
            writer.WriteString("rewardSystem", request.Conditions.RewardSystem);
            writer.WriteString("paymentBand", request.Conditions.PaymentBand);
            writer.WriteNumber("capacityHeadcount", request.Conditions.CapacityHeadcount);
            writer.WriteString("staffingKey", request.Conditions.StaffingKey);
            writer.WriteNumber(
                "averageWageBandOptionKind", (int)request.Conditions.AverageWageBandOption.Kind);
            writer.WriteNumber(
                "averageWageBandOptionCode",
                request.Conditions.AverageWageBandOption.OfficialOptionCode);
            writer.WriteNumber("r8ReformStatus", (int)request.Conditions.R8ReformStatus);
            writer.WriteString("regionKey", request.RegionKey);
            writer.WriteString("serviceKind", request.ServiceKind);
            writer.WriteEndObject();

            if (claimInput is null)
            {
                writer.WriteNull("claimInput");
            }
            else
            {
                writer.WriteStartObject("claimInput");
                WriteNumberOrNull(
                    writer, "upperLimitManagementResult", (int?)claimInput.UpperLimitManagementResult);
                WriteNumberOrNull(
                    writer, "upperLimitManagedAmountYen", claimInput.UpperLimitManagedAmountYen);
                WriteNumberOrNull(
                    writer, "municipalSubsidyAmountYen", claimInput.MunicipalSubsidyAmountYen);
                WriteMonthOrNull(
                    writer, "exceptionalUsageStartMonth", claimInput.ExceptionalUsageStartMonth);
                WriteMonthOrNull(
                    writer, "exceptionalUsageEndMonth", claimInput.ExceptionalUsageEndMonth);
                WriteNumberOrNull(writer, "exceptionalUsageDays", claimInput.ExceptionalUsageDays);
                WriteNumberOrNull(writer, "standardUsageDayTotal", claimInput.StandardUsageDayTotal);
                writer.WriteEndObject();
            }

            writer.WriteEndObject();
        }

        return stream.ToArray();
    }

    public static byte[] WriteCalculationSnapshot(
        ServiceMonth serviceMonth,
        string claimMasterVersion,
        RecipientClaimResult result)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(claimMasterVersion);
        ArgumentNullException.ThrowIfNull(result);

        using var stream = new MemoryStream();
        using (var writer = CreateWriter(stream))
        {
            writer.WriteStartObject();
            WriteEnvelopeIdentity(writer, "calculation");
            writer.WriteString("recipientId", FormatGuid(result.RecipientId));
            writer.WriteString("serviceMonth", serviceMonth.ToString());
            writer.WriteString("claimMasterVersion", claimMasterVersion);
            writer.WriteString("serviceCode", result.ServiceCode);
            writer.WriteNumber("billedDays", result.BilledDays);
            writer.WriteNumber("totalUnits", result.TotalUnits);
            writer.WriteNumber("totalCostYen", result.TotalCostYen);
            writer.WriteNumber("benefitYen", result.BenefitYen);
            writer.WriteNumber("burdenYen", result.BurdenYen);
            writer.WriteEndObject();
        }

        return stream.ToArray();
    }

    private static Utf8JsonWriter CreateWriter(Stream stream)
        => new(stream, new JsonWriterOptions
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            Indented = false,
        });

    private static void WriteEnvelopeIdentity(Utf8JsonWriter writer, string snapshotKind)
    {
        writer.WriteString("schemaVersion", ClaimSnapshotValidationCodecV1.SchemaVersionValue);
        writer.WriteString("validationCodecId", ClaimSnapshotValidationCodecV1.ValidationCodecIdValue);
        writer.WriteString("snapshotKind", snapshotKind);
    }

    private static void WriteNumberOrNull(Utf8JsonWriter writer, string propertyName, int? value)
    {
        if (value is { } number) writer.WriteNumber(propertyName, number);
        else writer.WriteNull(propertyName);
    }

    private static void WriteMonthOrNull(Utf8JsonWriter writer, string propertyName, ServiceMonth? value)
    {
        if (value is { } month) writer.WriteString(propertyName, month.ToString());
        else writer.WriteNull(propertyName);
    }

    private static string FormatGuid(Guid value) => value.ToString("D").ToLowerInvariant();
}
