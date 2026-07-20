using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using Tsumugi.Domain.ValueObjects;

namespace Tsumugi.Application.Claim;

/// <summary>
/// <see cref="ClaimFinalizationSnapshot"/>をcanonical UTF-8 JSON（snapshotKind = "finalization"、
/// codec v2 identity）として決定論的に生成する（プロパティ順は spec §6 のキー順で固定・インデントなし・
/// 数値/GUID/日付/時刻はculture非依存整形）。生成bytesは
/// <see cref="ClaimSnapshotValidationCodecV2.CreateEnvelope"/>で検証してenvelope化する。
/// </summary>
public static class ClaimFinalizationSnapshotWriter
{
    public static byte[] Write(ClaimFinalizationSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        using var stream = new MemoryStream();
        using (var writer = CreateWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteString("schemaVersion", ClaimSnapshotValidationCodecV2.SchemaVersionValue);
            writer.WriteString("validationCodecId", ClaimSnapshotValidationCodecV2.ValidationCodecIdValue);
            writer.WriteString("snapshotKind", "finalization");
            writer.WriteString("recipientId", FormatGuid(snapshot.RecipientId));
            writer.WriteString("serviceMonth", snapshot.ServiceMonth.ToString());
            writer.WriteString("claimMasterVersion", snapshot.ClaimMasterVersion);
            writer.WriteString("csvSpecificationVersion", snapshot.CsvSpecificationVersion);
            writer.WriteString("reportSpecificationVersion", snapshot.ReportSpecificationVersion);

            WriteOffice(writer, snapshot.Office);
            WriteRecipient(writer, snapshot.Recipient);
            WriteCertificate(writer, snapshot.Certificate);
            WriteClaimInput(writer, snapshot.ClaimInput);
            WriteDailyRecords(writer, snapshot.DailyRecords);
            WriteIntensiveSupportEpisode(writer, snapshot.IntensiveSupportEpisode);
            WriteClaimLines(writer, snapshot.ClaimLines);

            writer.WriteNumber("billedDays", snapshot.BilledDays);
            writer.WriteNumber("totalUnits", snapshot.TotalUnits);
            writer.WriteNumber("totalCostYen", snapshot.TotalCostYen);
            writer.WriteNumber("benefitYen", snapshot.BenefitYen);
            writer.WriteNumber("burdenYen", snapshot.BurdenYen);
            writer.WriteEndObject();
        }

        return stream.ToArray();
    }

    private static void WriteOffice(Utf8JsonWriter writer, ClaimFinalizationOfficeSnapshot office)
    {
        writer.WriteStartObject("office");
        writer.WriteString("officeNumber", office.OfficeNumber);
        writer.WriteString("officeName", office.OfficeName);
        writer.WriteString("regionGrade", office.RegionGrade.ToString());
        writer.WriteString("postalCode", office.PostalCode);
        writer.WriteString("address", office.Address);
        writer.WriteString("phoneNumber", office.PhoneNumber);
        writer.WriteString("representativeTitleAndName", office.RepresentativeTitleAndName);
        writer.WriteEndObject();
    }

    private static void WriteRecipient(Utf8JsonWriter writer, ClaimFinalizationRecipientSnapshot recipient)
    {
        writer.WriteStartObject("recipient");
        writer.WriteString("kanjiName", recipient.KanjiName);
        writer.WriteString("kanaName", recipient.KanaName);
        writer.WriteEndObject();
    }

    private static void WriteCertificate(
        Utf8JsonWriter writer, ClaimFinalizationCertificateSnapshot certificate)
    {
        writer.WriteStartObject("certificate");
        writer.WriteString("certificateNumber", certificate.CertificateNumber);
        writer.WriteString("municipalityNumber", certificate.MunicipalityNumber);
        WriteStringOrNull(writer, "subsidyMunicipalityNumber", certificate.SubsidyMunicipalityNumber);
        writer.WriteNumber("monthlyCostCap", certificate.MonthlyCostCap);
        WriteStringOrNull(
            writer, "upperLimitManagementProviderNumber", certificate.UpperLimitManagementProviderNumber);
        WriteStringOrNull(
            writer, "upperLimitManagementProviderName", certificate.UpperLimitManagementProviderName);
        writer.WriteEndObject();
    }

    private static void WriteClaimInput(Utf8JsonWriter writer, ClaimFinalizationClaimInputSnapshot claimInput)
    {
        writer.WriteStartObject("claimInput");
        WriteStringOrNull(writer, "upperLimitManagementResult", claimInput.UpperLimitManagementResult);
        WriteNumberOrNull(writer, "upperLimitManagedAmountYen", claimInput.UpperLimitManagedAmountYen);
        WriteNumberOrNull(writer, "municipalSubsidyAmountYen", claimInput.MunicipalSubsidyAmountYen);
        WriteMonthOrNull(writer, "exceptionalUsageStartMonth", claimInput.ExceptionalUsageStartMonth);
        WriteMonthOrNull(writer, "exceptionalUsageEndMonth", claimInput.ExceptionalUsageEndMonth);
        WriteNumberOrNull(writer, "exceptionalUsageDays", claimInput.ExceptionalUsageDays);
        WriteNumberOrNull(writer, "standardUsageDayTotal", claimInput.StandardUsageDayTotal);
        writer.WriteEndObject();
    }

    private static void WriteDailyRecords(
        Utf8JsonWriter writer, IReadOnlyList<ClaimFinalizationDailyRecordSnapshot> dailyRecords)
    {
        writer.WriteStartArray("dailyRecords");
        foreach (var record in dailyRecords)
        {
            writer.WriteStartObject();
            writer.WriteString("serviceDate", FormatDate(record.ServiceDate));
            writer.WriteString("attendance", record.Attendance.ToString());
            writer.WriteBoolean("mealProvided", record.MealProvided);
            writer.WriteString("transportKind", record.Transport.ToString());
            WriteStringOrNull(writer, "absenceResponseNote", record.AbsenceResponseNote);
            WriteTimeOrNull(writer, "serviceStartTime", record.ServiceStartTime);
            WriteTimeOrNull(writer, "serviceEndTime", record.ServiceEndTime);
            WriteNumberOrNull(writer, "specialVisitSupportMinutes", record.SpecialVisitSupportMinutes);
            writer.WriteBoolean("offsiteSupportApplied", record.OffsiteSupportApplied);
            WriteStringOrNull(writer, "medicalCoordinationType", record.MedicalCoordinationType);
            WriteStringOrNull(writer, "trialUseSupportType", record.TrialUseSupportType);
            writer.WriteBoolean("regionalCollaborationApplied", record.RegionalCollaborationApplied);
            writer.WriteBoolean("intensiveSupportApplied", record.IntensiveSupportApplied);
            writer.WriteBoolean("emergencyAdmissionApplied", record.EmergencyAdmissionApplied);
            writer.WriteBoolean("recipientConfirmation", record.RecipientConfirmation);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
    }

    private static void WriteIntensiveSupportEpisode(
        Utf8JsonWriter writer, ClaimFinalizationIntensiveSupportEpisodeSnapshot? episode)
    {
        if (episode is null)
        {
            writer.WriteNull("intensiveSupportEpisode");
            return;
        }

        writer.WriteStartObject("intensiveSupportEpisode");
        writer.WriteString("startDate", FormatDate(episode.StartDate));
        writer.WriteEndObject();
    }

    private static void WriteClaimLines(
        Utf8JsonWriter writer, IReadOnlyList<ClaimFinalizationClaimLineSnapshot> claimLines)
    {
        writer.WriteStartArray("claimLines");
        foreach (var line in claimLines)
        {
            writer.WriteStartObject();
            writer.WriteString("kind", line.Kind.ToString());
            writer.WriteString("serviceCode", line.ServiceCode);
            writer.WriteNumber("unit", line.Unit);
            writer.WriteNumber("count", line.Count);
            writer.WriteNumber("amountYen", line.AmountYen);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
    }

    private static Utf8JsonWriter CreateWriter(Stream stream)
        => new(stream, new JsonWriterOptions
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            Indented = false,
        });

    private static void WriteStringOrNull(Utf8JsonWriter writer, string propertyName, string? value)
    {
        if (value is null) writer.WriteNull(propertyName);
        else writer.WriteString(propertyName, value);
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

    private static void WriteTimeOrNull(Utf8JsonWriter writer, string propertyName, TimeOnly? value)
    {
        if (value is { } time) writer.WriteString(propertyName, FormatTime(time));
        else writer.WriteNull(propertyName);
    }

    private static string FormatDate(DateOnly value) => value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static string FormatTime(TimeOnly value) => value.ToString("HH:mm", CultureInfo.InvariantCulture);

    private static string FormatGuid(Guid value) => value.ToString("D").ToLowerInvariant();
}
