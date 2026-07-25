using System.Globalization;
using Tsumugi.Application.Dtos.Claim.Csv;
using Tsumugi.Domain.Enums;

namespace Tsumugi.Infrastructure.Csv.Generation;

/// <summary>
/// <c>field-mapping-r7-10.json</c> の <c>modelPath</c> / <c>targetModel.targetProperty</c> /
/// generatorRule の <c>selector</c> が指すモデル経路を、<see cref="ClaimCsvDto"/> 上で解決する。
/// </summary>
/// <remarks>
/// finalization snapshot v2 に含まれない経路（<c>ContractedProvider.*</c> /
/// <c>DailyRecord.Note</c> / <c>ClaimCalculation.InitialAdditionStartDate</c> /
/// <c>ClaimCalculation.SpecialMeasuresYen</c> / <c>ClaimServiceLine.SummaryNote</c>）は
/// <see cref="ClaimCsvValue.Missing"/> を返す。これらはいずれも requiredWhen が
/// <c>modelPresent</c> 系または歴史的条件のため、空欄が正しい出力になる。
/// 常時必須の経路が欠けた場合は呼び出し側（encoder）が MissingRequired で fail-close する。
/// </remarks>
internal static class ClaimCsvModelPath
{
    /// <summary>
    /// <c>modelPath</c> が単位変換を伴うときの区切り（<c>Entity.Property:unit</c>）。公式が要求する単位は
    /// CSV 仕様側の事実なので、C# のプロパティ名へ埋め込まず spec JSON の modelPath で宣言する
    /// （CLAUDE.md §ハード制約3）。変換元は必ず実在するドメインプロパティを指すため、
    /// 「existing はドメインの実プロパティを指す」という不変条件は保たれる。
    /// </summary>
    internal const string UnitSuffixSeparator = ":";

    /// <summary>汎用 pass-through 入力（ADR 0042）の path 接頭辞。</summary>
    internal const string GenericInputPrefix = "ClaimGenericInput.";

    /// <summary>1/100 時間単位（事業所編の「整数部2桁・小数部2桁」書式に対応する尺度）。</summary>
    internal const string HundredthsOfHourUnit = "hundredthsOfHour";

    /// <summary>
    /// 半角カナ（公式の「英数」属性＝1 バイト文字の項目へ全角カナ入力を写すための宣言）。
    /// </summary>
    internal const string HalfWidthKanaUnit = "halfWidthKana";

    /// <summary>単位接尾辞の閉じた語彙。ここに無い接尾辞を spec が宣言したら解決できない。</summary>
    internal static IReadOnlySet<string> KnownUnitSuffixes { get; } =
        new HashSet<string>(StringComparer.Ordinal) { HundredthsOfHourUnit, HalfWidthKanaUnit };

    /// <summary>
    /// 支給決定者氏名カナ（事業所編 基本情報 項目8）を書く経路。公式属性が「英数」＝半角 1 バイト
    /// なので、全角カナで入力された氏名は半角へ写してから出す（<see cref="HalfWidthKana"/>）。
    /// </summary>
    internal const string RecipientKanaNameHalfWidthPath =
        "Recipient.KanaName" + UnitSuffixSeparator + HalfWidthKanaUnit;

    /// <summary>
    /// 訪問支援特別加算の「サービス提供時間数」（事業所編 日ごと明細情報 項目27）を書く経路。
    /// 実績は分で持つが、公式書式は 1/100 時間なので単位接尾辞つきで宣言する。
    /// </summary>
    internal const string SpecialVisitSupportServiceHoursPath =
        "DailyRecord.SpecialVisitSupportMinutes" + UnitSuffixSeparator + HundredthsOfHourUnit;

    /// <summary>modelIn / modelEquals のトークンを数値へ解く際に使う列挙型の対応表。</summary>
    private static readonly Dictionary<string, Type> EnumTypeByPath = new(StringComparer.Ordinal)
    {
        ["DailyRecord.Transport"] = typeof(TransportKind),
        ["DailyRecord.Attendance"] = typeof(Attendance),
        ["DailyRecord.MedicalCoordinationType"] = typeof(MedicalCoordinationType),
        ["DailyRecord.TrialUseSupportType"] = typeof(TrialUseSupportType),
    };

    /// <summary>snapshot v2 に含まれないことが判明している経路（空欄が正しい出力になる）。</summary>
    internal static IReadOnlySet<string> PathsAbsentFromSnapshot { get; } =
        new HashSet<string>(StringComparer.Ordinal)
        {
            "DailyRecord.Note",
            "ClaimCalculation.InitialAdditionStartDate",
            "ClaimCalculation.SpecialMeasuresYen",
            "ClaimServiceLine.SummaryNote",
        };

    internal static bool TryResolveEnumToken(string path, string token, out long value)
    {
        value = 0;
        if (!EnumTypeByPath.TryGetValue(path, out var enumType)) return false;
        // 列挙型メンバー名の解析。CultureInfo: 非該当（書式変換を伴わない）
        if (!Enum.TryParse(enumType, token, ignoreCase: false, out var parsed) || parsed is null) // CultureInfo: 非該当
        {
            return false;
        }

        value = Convert.ToInt64(parsed, CultureInfo.InvariantCulture);
        return true;
    }

    /// <summary>受給者単位の値だが、ファイル全体で単一であることが請求の前提になる経路の接頭辞。</summary>
    private static readonly string[] RecipientUniformPrefixes =
        ["Certificate.", "Recipient.", "ClaimInput.", "IntensiveSupportEpisode."];

    /// <summary>
    /// 送迎の片道回数。往復（<see cref="TransportKind.Round"/>）は往路・復路の 2 回として数える
    /// （ADR 0028 決定5 / <c>ClaimCalculator.TransportOneWayCount</c> と同じ契約）。
    /// 数え上げの対象は <paramref name="listedTokens"/> に挙がった向きに限る。
    /// </summary>
    internal static int OneWayTripCount(string path, IReadOnlyList<string> listedTokens, long value)
    {
        if (!string.Equals(path, "DailyRecord.Transport", StringComparison.Ordinal))
        {
            return listedTokens.Any(token =>
                TryResolveEnumToken(path, token, out var expected) && expected == value) ? 1 : 0;
        }

        var kind = (TransportKind)value;
        if (!listedTokens.Any(token =>
                Enum.TryParse<TransportKind>(token, ignoreCase: false, out var listed) && listed == kind))
        {
            return 0;
        }

        return kind switch
        {
            TransportKind.Round => 2,
            TransportKind.Outbound or TransportKind.Inbound => 1,
            _ => 0,
        };
    }

    internal static ClaimCsvValue Resolve(string path, ClaimCsvResolutionScope scope)
    {
        if (PathsAbsentFromSnapshot.Contains(path)) return ClaimCsvValue.Missing;

        // 請求書（provider:J111:*）はファイル単位のレコードだが、市町村番号のように
        // 受給者側に持つ値を書く項目がある。国保連請求は市町村単位の束であることが前提のため、
        // 全受給者で値が一致することを要求し、割れていたら fail-close する。
        if (scope.Recipient is null
            && RecipientUniformPrefixes.Any(prefix => path.StartsWith(prefix, StringComparison.Ordinal)))
        {
            return ResolveUniformAcrossRecipients(path, scope);
        }

        return path switch
        {
            "Office.OfficeNumber" => ClaimCsvValue.FromText(scope.Dto.Office.OfficeNumber),

            "Recipient.KanaName" => ClaimCsvValue.FromText(scope.RequireRecipient(path).RecipientKanaName),
            RecipientKanaNameHalfWidthPath =>
                Narrowed(scope, scope.RequireRecipient(path).RecipientKanaName),

            "Certificate.CertificateNumber" =>
                ClaimCsvValue.FromText(scope.RequireRecipient(path).CertificateNumber),
            "Certificate.MunicipalityNumber" =>
                ClaimCsvValue.FromText(scope.RequireRecipient(path).MunicipalityNumber),
            "Certificate.SubsidyMunicipalityNumber" =>
                ClaimCsvValue.FromText(scope.RequireRecipient(path).SubsidyMunicipalityNumber),
            "Certificate.MonthlyCostCap" =>
                ClaimCsvValue.FromNumber(scope.RequireRecipient(path).MonthlyCostCapYen),
            "Certificate.UpperLimitManagementProviderNumber" =>
                ClaimCsvValue.FromText(scope.RequireRecipient(path).UpperLimitManagementProviderNumber),

            "ClaimInput.UpperLimitManagementResult" => ClaimCsvValue.FromOptionalNumber(
                scope.RequireRecipient(path).UpperLimitManagementResultCode),
            "ClaimInput.UpperLimitManagedAmountYen" => ClaimCsvValue.FromOptionalNumber(
                scope.RequireRecipient(path).UpperLimitManagedAmountYen),
            "ClaimInput.MunicipalSubsidyAmountYen" => ClaimCsvValue.FromOptionalNumber(
                scope.RequireRecipient(path).MunicipalSubsidyAmountYen),
            "ClaimInput.ExceptionalUsageStartMonth" => ClaimCsvValue.FromOptional(
                scope.RequireRecipient(path).ExceptionalUsageStartMonth, ClaimCsvValue.FromMonth),
            "ClaimInput.ExceptionalUsageEndMonth" => ClaimCsvValue.FromOptional(
                scope.RequireRecipient(path).ExceptionalUsageEndMonth, ClaimCsvValue.FromMonth),
            "ClaimInput.ExceptionalUsageDays" => ClaimCsvValue.FromOptionalNumber(
                scope.RequireRecipient(path).ExceptionalUsageDays),
            "ClaimInput.StandardUsageDayTotal" => ClaimCsvValue.FromOptionalNumber(
                scope.RequireRecipient(path).StandardUsageDayTotal),

            // グループB個別入力（Phase 3-3 / ADR 0033）。訪問支援特別加算の算定回数は留意事項通知
            // 2(6)⑨ により実際のサービス提供回数と別概念で、施設外支援の累計は年度累計のため、
            // どちらも当月分しか持たない確定 snapshot からは導出できない。
            "ClaimInput.SpecialVisitSupportBilledCount" => ClaimCsvValue.FromOptionalNumber(
                scope.RequireRecipient(path).SpecialVisitSupportBilledCount),
            "ClaimInput.OffsiteSupportCumulativeDays" => ClaimCsvValue.FromOptionalNumber(
                scope.RequireRecipient(path).OffsiteSupportCumulativeDays),

            "IntensiveSupportEpisode.StartDate" => ClaimCsvValue.FromOptional(
                scope.RequireRecipient(path).IntensiveSupportEpisodeStartDate, ClaimCsvValue.FromDate),

            // 契約情報。確定 snapshot が契約を持たない場合は Missing になり、必須項目は
            // encoder の MissingRequired で fail-close する（黙って空欄で出さない）。
            "ContractedProvider.ContractedSupplyDays" => ClaimCsvValue.FromOptionalNumber(
                scope.RequireRecipient(path).Contract?.ContractedSupplyDays),
            "ContractedProvider.ContractDate" => ClaimCsvValue.FromOptional(
                scope.RequireRecipient(path).Contract?.ContractDate, ClaimCsvValue.FromDate),
            "ContractedProvider.TerminationDate" => ClaimCsvValue.FromOptional(
                scope.RequireRecipient(path).Contract?.TerminationDate, ClaimCsvValue.FromDate),
            "ContractedProvider.CertificateEntryNumber" => ClaimCsvValue.FromOptionalNumber(
                scope.RequireRecipient(path).Contract?.CertificateEntryNumber),
            "ContractedProvider.FirstServiceDate" => ClaimCsvValue.FromOptional(
                scope.RequireRecipient(path).Contract?.FirstServiceDate, ClaimCsvValue.FromDate),

            "DailyRecord.ServiceDate" => ClaimCsvValue.FromDate(scope.RequireDay(path).ServiceDate),
            "DailyRecord.Attendance" => ClaimCsvValue.FromNumber(scope.RequireDay(path).AttendanceCode),
            "DailyRecord.Transport" => ClaimCsvValue.FromNumber(scope.RequireDay(path).TransportCode),
            "DailyRecord.MealProvided" => Flag(scope.RequireDay(path).MealProvided),
            "DailyRecord.ServiceStartTime" => ClaimCsvValue.FromOptional(
                scope.RequireDay(path).ServiceStartTime, ClaimCsvValue.FromTime),
            "DailyRecord.ServiceEndTime" => ClaimCsvValue.FromOptional(
                scope.RequireDay(path).ServiceEndTime, ClaimCsvValue.FromTime),
            // 汎用 pass-through 入力（ADR 0042）。宣言だけで増える項目なので、path 名ごとの
            // ハードコードを増やさず接頭辞で受ける。値は文字列で、数値属性の項目でも encoder が
            // 文字種と桁数を再検証する。
            _ when path.StartsWith(GenericInputPrefix, StringComparison.Ordinal) =>
                GenericInput(scope, path),

            "DailyRecord.SpecialVisitSupportMinutes" => ClaimCsvValue.FromOptionalNumber(
                scope.RequireDay(path).SpecialVisitSupportMinutes),
            SpecialVisitSupportServiceHoursPath =>
                HundredthsOfHour(scope, scope.RequireDay(path).SpecialVisitSupportMinutes),
            // 算定時間数（時間・整数）。サービス提供時間（分）とは別項目で、そこからは導出できない。
            "DailyRecord.SpecialVisitSupportBilledHours" => ClaimCsvValue.FromOptionalNumber(
                scope.RequireDay(path).SpecialVisitSupportBilledHours),
            "DailyRecord.OffsiteSupportApplied" => Flag(scope.RequireDay(path).OffsiteSupportApplied),
            "DailyRecord.MedicalCoordinationType" => ClaimCsvValue.FromOptionalNumber(
                scope.RequireDay(path).MedicalCoordinationCode),
            "DailyRecord.TrialUseSupportType" => ClaimCsvValue.FromOptionalNumber(
                scope.RequireDay(path).TrialUseSupportCode),
            "DailyRecord.RegionalCollaborationApplied" =>
                Flag(scope.RequireDay(path).RegionalCollaborationApplied),
            "DailyRecord.IntensiveSupportApplied" => Flag(scope.RequireDay(path).IntensiveSupportApplied),
            "DailyRecord.EmergencyAdmissionApplied" => Flag(scope.RequireDay(path).EmergencyAdmissionApplied),

            "ClaimServiceLine.ServiceCode" => ClaimCsvValue.FromText(scope.RequireLine(path).ServiceCode),
            "ClaimServiceLine.UnitCount" => ClaimCsvValue.FromNumber(scope.RequireLine(path).Unit),
            "ClaimServiceLine.Count" => ClaimCsvValue.FromNumber(scope.RequireLine(path).Count),

            "RegionalClassificationMaster.byOfficeAndServiceProvisionMonth" =>
                RegionClassificationCode(scope),
            "UnitPriceMaster.byRegionServiceTypeAndServiceProvisionMonth" =>
                ClaimCsvValue.FromNumber(scope.Dto.Office.UnitPriceMilliYen),

            _ => throw new ClaimCsvGenerationException(
                scope.FieldId,
                ClaimCsvGenerationReason.UnresolvableModelPath,
                $"model path '{path}' is not known to the CSV generator"),
        };
    }

    private static ClaimCsvValue ResolveUniformAcrossRecipients(
        string path,
        ClaimCsvResolutionScope scope)
    {
        if (scope.Dto.Recipients.Count == 0)
        {
            throw new ClaimCsvGenerationException(
                scope.FieldId,
                ClaimCsvGenerationReason.MissingRow,
                $"model path '{path}' requires at least one recipient");
        }

        var values = scope.Dto.Recipients
            .Select((_, index) => Resolve(path, scope with
            {
                Row = scope.Row with { RecipientIndex = index },
            }))
            .Distinct()
            .ToArray();

        return values.Length == 1
            ? values[0]
            : throw new ClaimCsvGenerationException(
                scope.FieldId,
                ClaimCsvGenerationReason.UnresolvableModelPath,
                $"model path '{path}' differs across recipients; a claim file must cover a single value");
    }

    /// <summary>
    /// 地域区分コード。共通編のコード一覧（一級地=11 … 七級地=17 / その他=23）から解決する。
    /// 表に載らない区分（未設定等）は fail-close し、推測したコードを出さない。
    /// </summary>
    private static ClaimCsvValue RegionClassificationCode(ClaimCsvResolutionScope scope) =>
        Specifications.RegionClassificationCodeCatalog.Instance
            .TryResolve(scope.Dto.Office.RegionGrade, out var code)
            ? ClaimCsvValue.FromText(code)
            : throw new ClaimCsvGenerationException(
                scope.FieldId,
                ClaimCsvGenerationReason.UnresolvableModelPath,
                "the region grade has no official region classification code");

    /// <summary>
    /// 全角カナ・全角英数字を半角へ写す（公式属性「英数」の項目）。写せない文字（ひらがな・漢字・
    /// 康熙部首など）は丸めずに fail-close する。例外には fieldId と理由だけを載せ、値は載せない。
    /// </summary>
    private static ClaimCsvValue Narrowed(ClaimCsvResolutionScope scope, string? value)
    {
        if (string.IsNullOrEmpty(value)) return ClaimCsvValue.Missing;

        return HalfWidthKana.TryNarrow(value, out var narrowed)
            ? ClaimCsvValue.FromText(narrowed)
            : throw new ClaimCsvGenerationException(
                scope.FieldId,
                ClaimCsvGenerationReason.UnresolvableModelPath,
                "the value contains a character that has no half-width form, but the official "
                + "attribute of this item is 英数 (single-byte characters only)");
    }

    /// <summary>真偽フラグは「該当する/しない」だけを表し、値そのものは持たない。</summary>
    private static ClaimCsvValue Flag(bool value) =>
        value ? ClaimCsvValue.FromNumber(1) : ClaimCsvValue.Missing;

    /// <summary>1 時間の分数。時間と分の関係は制度に依らない普遍の換算。</summary>
    private const int MinutesPerHour = 60;

    /// <summary>1 時間を 1/100 時間で表した値（小数部 2 桁の尺度）。</summary>
    private const int HundredthsPerHour = 100;

    /// <summary>
    /// 分で持つ実績を 1/100 時間へ<b>厳密に</b>変換する。事業所編「就労継続支援Ｂ型日ごと明細情報」
    /// 項目27 は「実際にサービス提供した時間数（時間）を整数部 2 桁・小数部 2 桁で設定」
    /// （例: 1.5 時間 → 0150）と定めるため、出力の尺度は分ではなく 1/100 時間になる。
    /// </summary>
    /// <remarks>
    /// 1/100 時間で表せない分値（3 の倍数でない分。例: 50 分 = 83.33… ）は、公式資料が丸め方向も
    /// 丸め桁も定めていないため、黙って丸めずに fail-close する。切り上げ・切り捨て・四捨五入の
    /// どれを採っても加算の算定時間が動くため、推測で埋めない（docs/open-questions.md で追跡）。
    /// </remarks>
    /// <summary>
    /// 宣言された汎用 pass-through 入力の値。未入力は Missing（要求条件が真なら encoder が
    /// <c>MissingRequired</c> で fail-close する）。
    /// </summary>
    private static ClaimCsvValue GenericInput(ClaimCsvResolutionScope scope, string path)
    {
        var name = path[GenericInputPrefix.Length..];
        var recipient = scope.RequireRecipient(path);
        return recipient.GenericInputs is { } values
            && values.TryGetValue(name, out var value)
            && !string.IsNullOrWhiteSpace(value)
                ? ClaimCsvValue.FromText(value)
                : ClaimCsvValue.Missing;
    }

    private static ClaimCsvValue HundredthsOfHour(ClaimCsvResolutionScope scope, int? minutes)
    {
        if (minutes is not { } value) return ClaimCsvValue.Missing;

        var scaled = (long)value * HundredthsPerHour;
        return scaled % MinutesPerHour == 0
            ? ClaimCsvValue.FromNumber(scaled / MinutesPerHour)
            : throw new ClaimCsvGenerationException(
                scope.FieldId,
                ClaimCsvGenerationReason.UnresolvableRule,
                "the recorded minutes have no exact hundredths-of-an-hour value and the official "
                + "rounding rule for this item is not fixed");
    }
}
