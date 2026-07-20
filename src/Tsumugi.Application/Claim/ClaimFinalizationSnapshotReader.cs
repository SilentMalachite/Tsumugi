using System.Globalization;
using System.Text.Json;
using Tsumugi.Application.Dtos.Claim.Reports;
using Tsumugi.Domain.Enums;
using Tsumugi.Domain.ValueObjects;

namespace Tsumugi.Application.Claim;

/// <summary>
/// <see cref="ClaimFinalizationSnapshotWriter"/>が生成したcanonical UTF-8 JSONを
/// <see cref="ClaimFinalizationSnapshot"/>へ復元する。identity（schemaVersion/validationCodecId）と
/// snapshotKind = "finalization" を検証する（envelope自体の署名検証はcodec v2の責務であり、
/// このReaderはenvelope検証済みのcanonical bytesを受け取る前提）。
/// </summary>
public static class ClaimFinalizationSnapshotReader
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        AllowDuplicateProperties = false,
    };

    public static ClaimFinalizationSnapshot Parse(ReadOnlySpan<byte> canonicalUtf8)
    {
        if (canonicalUtf8.IsEmpty)
            throw new InvalidOperationException("canonical bytes が空です。");

        JsonElement root;
        try
        {
            root = JsonSerializer.Deserialize<JsonElement>(canonicalUtf8, SerializerOptions);
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException("canonical bytes の JSON parse に失敗しました。", exception);
        }

        if (root.ValueKind != JsonValueKind.Object)
            throw new InvalidOperationException("canonical bytes は JSON object でなければなりません。");

        var schemaVersion = RequireString(root, "schemaVersion");
        if (schemaVersion != ClaimSnapshotValidationCodecV2.SchemaVersionValue)
            throw new InvalidOperationException(
                $"schemaVersion は '{ClaimSnapshotValidationCodecV2.SchemaVersionValue}' でなければなりません。");

        var validationCodecId = RequireString(root, "validationCodecId");
        if (validationCodecId != ClaimSnapshotValidationCodecV2.ValidationCodecIdValue)
            throw new InvalidOperationException(
                $"validationCodecId は '{ClaimSnapshotValidationCodecV2.ValidationCodecIdValue}' でなければなりません。");

        var snapshotKind = RequireString(root, "snapshotKind");
        if (snapshotKind != "finalization")
            throw new InvalidOperationException(
                "snapshotKind は 'finalization' でなければなりません（calculation snapshotはこのReaderでは扱えません）。");

        return new ClaimFinalizationSnapshot(
            RecipientId: Guid.Parse(RequireString(root, "recipientId"), CultureInfo.InvariantCulture),
            ServiceMonth: ParseServiceMonth(RequireString(root, "serviceMonth")),
            ClaimMasterVersion: RequireString(root, "claimMasterVersion"),
            CsvSpecificationVersion: RequireString(root, "csvSpecificationVersion"),
            ReportSpecificationVersion: RequireString(root, "reportSpecificationVersion"),
            Office: ParseOffice(RequireObject(root, "office")),
            Recipient: ParseRecipient(RequireObject(root, "recipient")),
            Certificate: ParseCertificate(RequireObject(root, "certificate")),
            ClaimInput: ParseClaimInput(RequireObject(root, "claimInput")),
            DailyRecords: ParseDailyRecords(RequireArray(root, "dailyRecords")),
            IntensiveSupportEpisode: ParseIntensiveSupportEpisode(RequireProperty(root, "intensiveSupportEpisode")),
            ClaimLines: ParseClaimLines(RequireArray(root, "claimLines")),
            BilledDays: RequireInt(root, "billedDays"),
            TotalUnits: RequireInt(root, "totalUnits"),
            TotalCostYen: RequireInt(root, "totalCostYen"),
            BenefitYen: RequireInt(root, "benefitYen"),
            BurdenYen: RequireInt(root, "burdenYen"));
    }

    private static ClaimFinalizationOfficeSnapshot ParseOffice(JsonElement office) => new(
        OfficeNumber: RequireString(office, "officeNumber"),
        OfficeName: RequireString(office, "officeName"),
        RegionGrade: Enum.Parse<RegionGrade>(RequireString(office, "regionGrade")),
        PostalCode: RequireString(office, "postalCode"),
        Address: RequireString(office, "address"),
        PhoneNumber: RequireString(office, "phoneNumber"),
        RepresentativeTitleAndName: RequireString(office, "representativeTitleAndName"));

    private static ClaimFinalizationRecipientSnapshot ParseRecipient(JsonElement recipient) => new(
        KanjiName: RequireString(recipient, "kanjiName"),
        KanaName: RequireString(recipient, "kanaName"));

    private static ClaimFinalizationCertificateSnapshot ParseCertificate(JsonElement certificate) => new(
        CertificateNumber: RequireString(certificate, "certificateNumber"),
        MunicipalityNumber: RequireString(certificate, "municipalityNumber"),
        SubsidyMunicipalityNumber: OptionalString(certificate, "subsidyMunicipalityNumber"),
        MonthlyCostCap: RequireInt(certificate, "monthlyCostCap"),
        UpperLimitManagementProviderNumber: OptionalString(certificate, "upperLimitManagementProviderNumber"),
        UpperLimitManagementProviderName: OptionalString(certificate, "upperLimitManagementProviderName"));

    private static ClaimFinalizationClaimInputSnapshot ParseClaimInput(JsonElement claimInput) => new(
        UpperLimitManagementResult: OptionalString(claimInput, "upperLimitManagementResult"),
        UpperLimitManagedAmountYen: OptionalInt(claimInput, "upperLimitManagedAmountYen"),
        MunicipalSubsidyAmountYen: OptionalInt(claimInput, "municipalSubsidyAmountYen"),
        ExceptionalUsageStartMonth: OptionalServiceMonth(claimInput, "exceptionalUsageStartMonth"),
        ExceptionalUsageEndMonth: OptionalServiceMonth(claimInput, "exceptionalUsageEndMonth"),
        ExceptionalUsageDays: OptionalInt(claimInput, "exceptionalUsageDays"),
        StandardUsageDayTotal: OptionalInt(claimInput, "standardUsageDayTotal"));

    private static IReadOnlyList<ClaimFinalizationDailyRecordSnapshot> ParseDailyRecords(JsonElement dailyRecords)
        => [.. dailyRecords.EnumerateArray().Select(ParseDailyRecord)];

    private static ClaimFinalizationDailyRecordSnapshot ParseDailyRecord(JsonElement record) => new(
        ServiceDate: ParseDate(RequireString(record, "serviceDate")),
        Attendance: Enum.Parse<Attendance>(RequireString(record, "attendance")),
        MealProvided: RequireBool(record, "mealProvided"),
        Transport: Enum.Parse<TransportKind>(RequireString(record, "transportKind")),
        AbsenceResponseNote: OptionalString(record, "absenceResponseNote"),
        ServiceStartTime: OptionalTime(record, "serviceStartTime"),
        ServiceEndTime: OptionalTime(record, "serviceEndTime"),
        SpecialVisitSupportMinutes: OptionalInt(record, "specialVisitSupportMinutes"),
        OffsiteSupportApplied: RequireBool(record, "offsiteSupportApplied"),
        MedicalCoordinationType: OptionalString(record, "medicalCoordinationType"),
        TrialUseSupportType: OptionalString(record, "trialUseSupportType"),
        RegionalCollaborationApplied: RequireBool(record, "regionalCollaborationApplied"),
        IntensiveSupportApplied: RequireBool(record, "intensiveSupportApplied"),
        EmergencyAdmissionApplied: RequireBool(record, "emergencyAdmissionApplied"),
        RecipientConfirmation: RequireBool(record, "recipientConfirmation"));

    private static ClaimFinalizationIntensiveSupportEpisodeSnapshot? ParseIntensiveSupportEpisode(
        JsonElement episode)
        => episode.ValueKind == JsonValueKind.Null
            ? null
            : new ClaimFinalizationIntensiveSupportEpisodeSnapshot(ParseDate(RequireString(episode, "startDate")));

    private static IReadOnlyList<ClaimFinalizationClaimLineSnapshot> ParseClaimLines(JsonElement claimLines)
        => [.. claimLines.EnumerateArray().Select(ParseClaimLine)];

    private static ClaimFinalizationClaimLineSnapshot ParseClaimLine(JsonElement line) => new(
        Kind: Enum.Parse<ClaimDetailLineKind>(RequireString(line, "kind")),
        ServiceCode: RequireString(line, "serviceCode"),
        Unit: RequireInt(line, "unit"),
        Count: RequireInt(line, "count"),
        AmountYen: RequireInt(line, "amountYen"));

    private static ServiceMonth ParseServiceMonth(string value)
    {
        var parts = value.Split('-');
        if (parts.Length != 2
            || !int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var year)
            || !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var month))
            throw new InvalidOperationException($"serviceMonth の形式が不正です: '{value}'");
        return new ServiceMonth(year, month);
    }

    private static ServiceMonth? OptionalServiceMonth(JsonElement obj, string propertyName)
    {
        var property = RequireProperty(obj, propertyName);
        return property.ValueKind == JsonValueKind.Null ? null : ParseServiceMonth(property.GetString()!);
    }

    private static DateOnly ParseDate(string value)
        => DateOnly.ParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static TimeOnly ParseTime(string value)
        => TimeOnly.ParseExact(value, "HH:mm", CultureInfo.InvariantCulture);

    private static TimeOnly? OptionalTime(JsonElement obj, string propertyName)
    {
        var property = RequireProperty(obj, propertyName);
        return property.ValueKind == JsonValueKind.Null ? null : ParseTime(property.GetString()!);
    }

    private static JsonElement RequireProperty(JsonElement obj, string propertyName)
        => obj.TryGetProperty(propertyName, out var property)
            ? property
            : throw new InvalidOperationException($"必須フィールド '{propertyName}' がありません。");

    private static JsonElement RequireObject(JsonElement obj, string propertyName)
    {
        var property = RequireProperty(obj, propertyName);
        if (property.ValueKind != JsonValueKind.Object)
            throw new InvalidOperationException($"フィールド '{propertyName}' は JSON object でなければなりません。");
        return property;
    }

    private static JsonElement RequireArray(JsonElement obj, string propertyName)
    {
        var property = RequireProperty(obj, propertyName);
        if (property.ValueKind != JsonValueKind.Array)
            throw new InvalidOperationException($"フィールド '{propertyName}' は JSON array でなければなりません。");
        return property;
    }

    private static string RequireString(JsonElement obj, string propertyName)
    {
        var property = RequireProperty(obj, propertyName);
        return property.ValueKind == JsonValueKind.String
            ? property.GetString()!
            : throw new InvalidOperationException($"フィールド '{propertyName}' は文字列でなければなりません。");
    }

    private static string? OptionalString(JsonElement obj, string propertyName)
    {
        var property = RequireProperty(obj, propertyName);
        if (property.ValueKind == JsonValueKind.Null) return null;
        return property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : throw new InvalidOperationException($"フィールド '{propertyName}' は文字列またはnullでなければなりません。");
    }

    private static int RequireInt(JsonElement obj, string propertyName)
    {
        var property = RequireProperty(obj, propertyName);
        return property.ValueKind == JsonValueKind.Number && property.TryGetInt32(out var value)
            ? value
            : throw new InvalidOperationException($"フィールド '{propertyName}' は整数でなければなりません。");
    }

    private static int? OptionalInt(JsonElement obj, string propertyName)
    {
        var property = RequireProperty(obj, propertyName);
        if (property.ValueKind == JsonValueKind.Null) return null;
        return property.ValueKind == JsonValueKind.Number && property.TryGetInt32(out var value)
            ? value
            : throw new InvalidOperationException($"フィールド '{propertyName}' は整数またはnullでなければなりません。");
    }

    private static bool RequireBool(JsonElement obj, string propertyName)
    {
        var property = RequireProperty(obj, propertyName);
        return property.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => throw new InvalidOperationException($"フィールド '{propertyName}' は真偽値でなければなりません。"),
        };
    }
}
