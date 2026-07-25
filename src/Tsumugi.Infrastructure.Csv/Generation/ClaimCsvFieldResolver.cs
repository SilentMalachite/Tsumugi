using System.Globalization;
using Tsumugi.Application.Dtos.Claim.Csv;
using Tsumugi.Infrastructure.Csv.Specifications;

namespace Tsumugi.Infrastructure.Csv.Generation;

/// <summary>
/// spec JSON のマッピング（generated / existing / explicitInput / missing）だけを根拠に、
/// 1 フィールド 1 行分の値を解決する。fieldId・定数・制度実値を C# 側に持たない。
/// </summary>
/// <remarks>
/// 参照は遅延・メモ化つきで解決するため、レコード間の依存（明細→集計→請求書）を
/// 呼び出し側が並べ替える必要がない。循環参照は fail-close する。
/// </remarks>
internal sealed class ClaimCsvFieldResolver
{
    private readonly ClaimCsvDto _dto;
    private readonly CsvSpecificationCatalog _catalog;
    private readonly IReadOnlyList<ClaimCsvRowPlan> _rows;
    private readonly Dictionary<string, string> _recordIdByFieldId;
    private readonly Dictionary<string, CsvFieldSpecification> _fieldById;
    private readonly Dictionary<(string FieldId, string RowKey), ClaimCsvValue> _memo = new();
    private readonly HashSet<(string FieldId, string RowKey)> _visiting = [];

    internal ClaimCsvFieldResolver(
        ClaimCsvDto dto,
        CsvSpecificationCatalog catalog,
        IReadOnlyList<ClaimCsvRowPlan> rows)
    {
        _dto = dto;
        _catalog = catalog;
        _rows = rows;
        _recordIdByFieldId = catalog.CommonRecords.Concat(catalog.ProviderRecords)
            .SelectMany(record => record.Fields.Select(field => (field.FieldId, record.RecordId)))
            .ToDictionary(item => item.FieldId, item => item.RecordId, StringComparer.Ordinal);
        _fieldById = catalog.CommonRecords.Concat(catalog.ProviderRecords)
            .SelectMany(record => record.Fields)
            .ToDictionary(field => field.FieldId, StringComparer.Ordinal);
    }

    /// <summary>1 セル分の生値（CP932 変換前）を返す。</summary>
    /// <remarks>
    /// 自己参照条件（<c>fieldPresent(self)</c> / <c>fieldNonZero(self)</c>）は「算出値があっても
    /// 当該欄には出さない」という表示規則であり、値そのものを消す指示ではない。参照式には算出値を
    /// 渡し、セルだけを空欄にする。これを混同すると、負担額 0 円の利用者で
    /// <c>provider:J121:04:019</c> が空になり、それを参照する必須項目
    /// <c>provider:J121:04:021</c> が出力できなくなる。
    /// </remarks>
    internal string RenderCell(string fieldId, ClaimCsvRowPlan row)
    {
        var specification = _fieldById[fieldId];
        var value = Resolve(fieldId, row);
        if (IsSelfReferencing(specification.RequiredWhen, fieldId)
            && !EvaluateCondition(
                specification.RequiredWhen, new ClaimCsvResolutionScope(fieldId, _dto, row), value))
        {
            return string.Empty;
        }

        return Render(value, specification);
    }

    private static bool IsSelfReferencing(string requiredWhen, string fieldId) =>
        requiredWhen.Contains(fieldId, StringComparison.Ordinal);

    private ClaimCsvValue Resolve(string fieldId, ClaimCsvRowPlan row)
    {
        var key = (fieldId, row.RowKey);
        if (_memo.TryGetValue(key, out var cached)) return cached;
        if (!_visiting.Add(key))
        {
            throw new ClaimCsvGenerationException(
                fieldId, ClaimCsvGenerationReason.CircularFieldReference, "field reference cycle detected");
        }

        try
        {
            var value = Evaluate(fieldId, row);
            _memo[key] = value;
            return value;
        }
        finally
        {
            _visiting.Remove(key);
        }
    }

    private ClaimCsvValue Evaluate(string fieldId, ClaimCsvRowPlan row)
    {
        var specification = _fieldById[fieldId];
        var mapping = _catalog.MappingByFieldId[fieldId];
        var scope = new ClaimCsvResolutionScope(fieldId, _dto, row);

        // requiredWhen が自分自身を参照しない限り、条件を先に評価して不要な導出を避ける。
        // 「算定条件を満たさない加算」の導出規則には現行 snapshot から確定できない意味論
        // （billableOccurrences / official180DayWindow 等）を含むものがあり、
        // 条件が偽のうちからそれを評価すると誤って fail-close してしまう。
        // 自己参照条件は表示規則なので、ここでは評価せず RenderCell へ委ねる。
        if (!IsSelfReferencing(specification.RequiredWhen, fieldId)
            && !EvaluateCondition(specification.RequiredWhen, scope, ClaimCsvValue.Missing))
        {
            return ClaimCsvValue.Missing;
        }

        var raw = mapping.Status switch
        {
            "generated" => EvaluateRule(ParseRule(mapping.GeneratorRule!), scope),
            "explicitInput" => EvaluateExplicitInput(mapping.InputContract!, scope),
            "existing" => ClaimCsvModelPath.Resolve(mapping.ModelPath!, scope),
            "missing" => ClaimCsvModelPath.Resolve(
                $"{mapping.TargetModel}.{mapping.TargetProperty}", scope),
            _ => throw new ClaimCsvGenerationException(
                fieldId,
                ClaimCsvGenerationReason.UnknownMappingStatus,
                $"mapping status '{mapping.Status}' is not supported"),
        };

        // モデル由来（existing / missing）で、spec が許容コードを 1 個だけ持つ項目は「該当する」ことの
        // 表明そのものがコード値になる。許容コードを持たず、条件が同一性判定（modelIn / modelEquals）の
        // 項目は「該当回数 1」を表す数値項目（送迎加算 往/復）。
        if (mapping.Status is "existing" or "missing" && !raw.IsAbsent)
        {
            if (specification.AllowedCodes.Count == 1)
            {
                return ClaimCsvValue.FromText(specification.AllowedCodes[0]);
            }

            if (specification.AllowedCodes.Count == 0 && IsIdentityCondition(specification.RequiredWhen))
            {
                return ClaimCsvValue.FromNumber(1);
            }
        }

        return raw;
    }

    private static ClaimCsvValue EvaluateExplicitInput(string inputContract, ClaimCsvResolutionScope scope) =>
        inputContract switch
        {
            "ProcessingMonth" => ClaimCsvValue.FromMonth(
                new Domain.ValueObjects.ServiceMonth(
                    scope.Dto.ProcessingMonth.Year, scope.Dto.ProcessingMonth.Month)),
            "ServiceProvisionMonth" => ClaimCsvValue.FromMonth(scope.Dto.ServiceMonth),
            _ => throw new ClaimCsvGenerationException(
                scope.FieldId,
                ClaimCsvGenerationReason.UnresolvableRule,
                $"input contract '{inputContract}' is not supported"),
        };

    private ClaimCsvValue EvaluateRule(CsvGeneratorRule rule, ClaimCsvResolutionScope scope) => rule.Head switch
    {
        // const(value=CRLF) は行終端項目を指す。値そのものは quoteRule=crlf 側が書く。
        "const" => rule.Require("value") is "CRLF"
            ? ClaimCsvValue.Missing
            : ClaimCsvValue.FromText(rule.Require("value")),
        "constEmpty" => ClaimCsvValue.Missing,
        "copy" => EvaluateCopy(rule, scope),
        "sum" => EvaluateSum(rule, scope),
        "count" => EvaluateCount(rule, scope),
        "min" => EvaluateMin(rule, scope),
        "max" => EvaluateMax(rule, scope),
        "multiply" => Arithmetic(rule.RequireList("fields"), scope, (left, right) => left * right),
        "difference" => ClaimCsvValue.FromNumber(
            Number(rule.Require("minuend"), scope) - Number(rule.Require("subtrahend"), scope)),
        "roundDown" => ClaimCsvValue.FromNumber(EvaluateExpression(rule.Require("expression"), scope)),
        "conditional" => EvaluateCondition(rule.Require("condition"), scope, ClaimCsvValue.Missing)
            ? Resolve(rule.Require("whenTrue"), ReferenceRow(rule.Require("whenTrue"), scope))
            : Resolve(rule.Require("whenFalse"), ReferenceRow(rule.Require("whenFalse"), scope)),
        "format" => ClaimCsvModelPath.Resolve(rule.Require("selector"), scope),
        "calendarDay" => EvaluateCalendarDay(rule, scope),
        "lookup" => EvaluateLookup(rule, scope),
        // 外側フレーム専用の規則。内側レコードでは発生しない。
        "sequence" or "recordCount" or "payload" => throw new ClaimCsvGenerationException(
            scope.FieldId,
            ClaimCsvGenerationReason.UnresolvableRule,
            $"generator rule '{rule.Head}' belongs to the outer frame writer"),
        _ => throw new ClaimCsvGenerationException(
            scope.FieldId,
            ClaimCsvGenerationReason.UnresolvableRule,
            $"generator rule '{rule.Head}' has no evaluator"),
    };

    private ClaimCsvValue EvaluateCopy(CsvGeneratorRule rule, ClaimCsvResolutionScope scope)
    {
        if (rule.Find("field") is { } field)
        {
            return Resolve(field, ReferenceRow(field, scope));
        }

        return ClaimCsvModelPath.Resolve(rule.Require("selector"), scope);
    }

    /// <summary>
    /// <c>sum(field=...)</c> が受け付ける絞り込み。就労継続支援B型のみを serviceScope とするため
    /// 給付種別×サービス種類は単一グループになり、いずれも「対象行のスコープ内にある元レコード行を
    /// すべて畳み込む」と同義になる。<c>requiredCondition</c> を含む値は、対象項目自身の
    /// <c>requiredWhen</c> が同じ条件で空欄化するため、ここでは追加の絞り込みを要しない。
    /// </summary>
    private static readonly HashSet<string> SupportedSumFilters = new(StringComparer.Ordinal)
    {
        "benefitType1",
        "benefitType1-and-requiredCondition",
        "all-benefit-types",
        "requiredCondition",
    };

    /// <summary>
    /// <c>sum(field=...)</c> が受け付ける集約軸。いずれも対象行のスコープ（ファイル or 受給者）内で
    /// 単一グループになる。
    /// </summary>
    private static readonly HashSet<string> SupportedSumGroupings = new(StringComparer.Ordinal)
    {
        "benefitType,serviceType",
        "serviceType",
        "recipient",
    };

    private ClaimCsvValue EvaluateSum(CsvGeneratorRule rule, ClaimCsvResolutionScope scope)
    {
        if (rule.Find("fields") is not null)
        {
            return Arithmetic(rule.RequireList("fields"), scope, (left, right) => left + right);
        }

        // 未知の絞り込み・集約軸を黙って無視すると、請求金額が静かに誤る。fail-close する。
        if (rule.Find("filter") is { } filter && !SupportedSumFilters.Contains(filter))
        {
            throw Unresolvable(scope.FieldId, $"sum filter '{filter}' is not defined by the specification");
        }

        if (rule.Find("groupBy") is { } groupBy && !SupportedSumGroupings.Contains(groupBy))
        {
            throw Unresolvable(scope.FieldId, $"sum groupBy '{groupBy}' is not defined by the specification");
        }

        var field = rule.Require("field");
        var total = RowsInScope(field, scope).Sum(row => AsNumber(Resolve(field, row), field));
        return ClaimCsvValue.FromNumber(total);
    }

    private ClaimCsvValue EvaluateCount(CsvGeneratorRule rule, ClaimCsvResolutionScope scope)
    {
        var selector = rule.Require("selector");

        // 明細レコードそのものを数える（請求書 集計の件数）。
        if (_catalog.ProviderRecords.Any(record =>
                string.Equals(record.RecordId, selector, StringComparison.Ordinal)))
        {
            return ClaimCsvValue.FromNumber(
                _rows.Count(row =>
                    string.Equals(row.RecordId, selector, StringComparison.Ordinal)
                    && row.IsWithin(scope.Row.RowKey)));
        }

        // 算定日数は確定時に snapshot へ焼き込んだ値（BilledDays）を正本にする。
        // 現行の日次記録から再集計すると、確定時点の帳票・CSV と食い違いうる。
        if (selector.StartsWith("contract(", StringComparison.Ordinal)
            || string.Equals(selector, "DailyRecord.ServiceDate", StringComparison.Ordinal))
        {
            if (rule.Find("window") is { } window
                && !string.Equals(window, "ServiceProvisionMonth", StringComparison.Ordinal))
            {
                throw Unresolvable(scope.FieldId, $"count window '{window}' is outside the finalized snapshot");
            }

            return ClaimCsvValue.FromNumber(scope.RequireRecipient(selector).BilledDays);
        }

        // official180DayWindow（施設外支援 累計）は就労系留意事項通知（1(1)①）により
        // 「毎年4月1日に始まり翌年3月31日に終わる1年間で 180 日を限度」＝<b>年度累計</b>であり、
        // 直近180日のローリング窓ではない。確定 snapshot は当月分の日次記録しか持たないため
        // 年度累計は算出できず、個別入力が必要（docs/open-questions.md）。
        if (rule.Find("window") is { } dayWindow
            && !string.Equals(dayWindow, "ServiceProvisionMonth", StringComparison.Ordinal))
        {
            throw Unresolvable(
                scope.FieldId,
                $"count window '{dayWindow}' needs a fiscal-year cumulative that the snapshot does not carry");
        }

        // billableOccurrences（訪問支援特別加算 算定回数）は留意事項通知 2(6)⑨により、実際の
        // サービス提供回数とは別概念（計画に基づく所要時間で算定し、月2回目は再度5日間以上の
        // 利用中断を要する）。日次実績から導出できないため個別入力が必要。
        if (rule.Find("measure") is { } measure
            && !string.Equals(measure, "serviceOccurrences", StringComparison.Ordinal))
        {
            throw Unresolvable(
                scope.FieldId,
                $"count measure '{measure}' is a billable count that cannot be derived from daily records");
        }

        var matches = scope.EnumerateDailyRecordScopes()
            .Sum(dayScope => DayCountContribution(rule, selector, dayScope));
        return ClaimCsvValue.FromNumber(matches);
    }

    /// <summary>1 日分がこの count 規則へ寄与する回数。</summary>
    /// <remarks>
    /// <c>directions</c> を伴う規則（送迎加算の回数）は<b>片道換算</b>で数える。往復は往路 1・復路 1 の
    /// 2 回であり、日数ではない（ADR 0028 決定5 / <c>ClaimCalculator</c> の
    /// <c>TransportOneWayCount</c> と同じ契約）。日数で数えると往復日が過少になる。
    /// </remarks>
    private static int DayCountContribution(
        CsvGeneratorRule rule,
        string selector,
        ClaimCsvResolutionScope dayScope)
    {
        var value = ClaimCsvModelPath.Resolve(selector, dayScope);

        if (rule.Find("directions") is { } directions)
        {
            var listed = directions
                .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .Where(token => ClaimCsvModelPath.TryResolveEnumToken(selector, token, out _))
                .ToArray();
            return value is ClaimCsvValue.NumberValue number
                ? ClaimCsvModelPath.OneWayTripCount(selector, listed, number.Value)
                : 0;
        }

        return rule.Find("value") switch
        {
            "true" or "present" or null => value.IsAbsent ? 0 : 1,
            _ => throw Unresolvable(dayScope.FieldId, $"count value '{rule.Find("value")}' is not supported"),
        };
    }

    private ClaimCsvValue EvaluateMin(CsvGeneratorRule rule, ClaimCsvResolutionScope scope)
    {
        if (rule.Find("fields") is not null)
        {
            // 欠損を 0 とみなすと最小値が 0 になり、負担上限のような項目が静かに 0 円になる。
            var fields = rule.RequireList("fields");
            var values = fields
                .Select(field => Resolve(field, ReferenceRow(field, scope)))
                .ToArray();
            if (Array.Exists(values, value => value.IsAbsent))
            {
                throw Unresolvable(
                    scope.FieldId, "min requires every operand to have a value");
            }

            return ClaimCsvValue.FromNumber(values.Select((value, index) =>
                AsNumber(value, fields[index])).Min());
        }

        // selector 形式の min は spec 上「有効な継続契約における最初のサービス提供日」だけだったが、
        // 当月の日次記録から推測すると継続契約で誤値になるため、個別入力へ移した（ADR 0032）。
        // 残る min は fields 形式のみ。
        throw Unresolvable(
            scope.FieldId, "min with a selector is no longer derived; the value is entered per contract");
    }

    private static ClaimCsvValue EvaluateMax(CsvGeneratorRule rule, ClaimCsvResolutionScope scope)
    {
        // 「当月中に継続契約が終了した場合の最終サービス提供日」。契約終了は確定 snapshot に
        // 含まれないため、常に空欄（requiredWhen も自己参照の任意項目）。
        _ = rule;
        _ = scope;
        return ClaimCsvValue.Missing;
    }

    private static ClaimCsvValue EvaluateCalendarDay(CsvGeneratorRule rule, ClaimCsvResolutionScope scope)
    {
        var origin = ClaimCsvModelPath.Resolve(rule.Require("selector"), scope);
        if (origin is not ClaimCsvValue.DateValue date) return ClaimCsvValue.Missing;
        var offset = int.Parse(rule.Require("offsetDays"), CultureInfo.InvariantCulture);
        return ClaimCsvValue.FromDate(date.Value.AddDays(offset));
    }

    private static ClaimCsvValue EvaluateLookup(CsvGeneratorRule rule, ClaimCsvResolutionScope scope)
    {
        var selector = rule.Require("selector");
        if (string.Equals(selector, "firstThreeCharactersOfInnerExchangeInformationId", StringComparison.Ordinal))
        {
            throw new ClaimCsvGenerationException(
                scope.FieldId,
                ClaimCsvGenerationReason.UnresolvableRule,
                "the data kind lookup belongs to the outer frame writer");
        }

        return ClaimCsvModelPath.Resolve(selector, scope);
    }

    private ClaimCsvValue Arithmetic(
        IReadOnlyList<string> fields,
        ClaimCsvResolutionScope scope,
        Func<long, long, long> combine)
    {
        var total = Number(fields[0], scope);
        for (var index = 1; index < fields.Count; index++)
        {
            total = combine(total, Number(fields[index], scope));
        }

        return ClaimCsvValue.FromNumber(total);
    }

    /// <summary>
    /// <c>roundDown</c> の式（<c>fieldId*fieldId/1000</c> / <c>fieldId/10</c>）を左から順に評価する。
    /// 除算は整数の切り捨て。
    /// </summary>
    private long EvaluateExpression(string expression, ClaimCsvResolutionScope scope)
    {
        var tokens = expression.Split(['*', '/'], StringSplitOptions.None);
        var operators = expression.Where(character => character is '*' or '/').ToArray();
        if (tokens.Length != operators.Length + 1)
        {
            throw Unresolvable(scope.FieldId, $"expression '{expression}' is malformed");
        }

        var accumulator = Operand(tokens[0], scope);
        for (var index = 0; index < operators.Length; index++)
        {
            var right = Operand(tokens[index + 1], scope);
            accumulator = operators[index] switch
            {
                '*' => accumulator * right,
                // 金額計算のため浮動小数点を経由しない。整数の切り捨て除算で閉じる。
                '/' when right != 0 => FloorDivide(accumulator, right),
                _ => throw Unresolvable(scope.FieldId, $"expression '{expression}' divides by zero"),
            };
        }

        return accumulator;
    }

    private static long FloorDivide(long dividend, long divisor)
    {
        var quotient = dividend / divisor;
        return dividend % divisor != 0 && (dividend < 0) != (divisor < 0) ? quotient - 1 : quotient;
    }

    private long Operand(string token, ClaimCsvResolutionScope scope) =>
        long.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out var literal)
            ? literal
            : Number(token, scope);

    private long Number(string fieldId, ClaimCsvResolutionScope scope) =>
        AsNumber(Resolve(fieldId, ReferenceRow(fieldId, scope)), fieldId);

    private static long AsNumber(ClaimCsvValue value, string fieldId) => value switch
    {
        ClaimCsvValue.NumberValue number => number.Value,
        ClaimCsvValue.AbsentValue => 0,
        ClaimCsvValue.TextValue text when TryParseInvariant(text.Value, out var parsed) => parsed,
        _ => throw new ClaimCsvGenerationException(
            fieldId, ClaimCsvGenerationReason.UnsupportedDataType, "the value is not numeric"),
    };

    /// <summary>参照先フィールドが属する行を、現在行のスコープから決める。</summary>
    private ClaimCsvRowPlan ReferenceRow(string fieldId, ClaimCsvResolutionScope scope)
    {
        var recordId = _recordIdByFieldId[fieldId];
        var candidates = _rows
            .Where(row => string.Equals(row.RecordId, recordId, StringComparison.Ordinal))
            .ToArray();

        var exact = candidates.FirstOrDefault(row =>
            string.Equals(row.RowKey, scope.Row.RowKey, StringComparison.Ordinal));
        if (exact is not null) return exact;

        // 祖先スコープ（明細行 → 受給者 → ファイル）を辿る。
        var ancestor = candidates
            .Where(row => scope.Row.RowKey.StartsWith(row.RowKey, StringComparison.Ordinal))
            .MaxBy(row => row.RowKey.Length);
        if (ancestor is not null) return ancestor;

        if (candidates.Length == 1) return candidates[0];

        throw new ClaimCsvGenerationException(
            scope.FieldId,
            ClaimCsvGenerationReason.UnresolvableFieldReference,
            $"reference '{fieldId}' has no row in the scope of '{scope.Row.RowKey}'");
    }

    /// <summary>集約対象になる行（対象行のスコープ内にある元レコード行）。</summary>
    private IEnumerable<ClaimCsvRowPlan> RowsInScope(string fieldId, ClaimCsvResolutionScope scope)
    {
        var recordId = _recordIdByFieldId[fieldId];
        return _rows.Where(row =>
            string.Equals(row.RecordId, recordId, StringComparison.Ordinal)
            && row.IsWithin(scope.Row.RowKey));
    }

    private static bool IsIdentityCondition(string requiredWhen) =>
        requiredWhen.StartsWith("modelIn(", StringComparison.Ordinal)
        || requiredWhen.StartsWith("modelEquals(", StringComparison.Ordinal);

    private bool EvaluateCondition(string condition, ClaimCsvResolutionScope scope, ClaimCsvValue selfValue)
    {
        if (string.Equals(condition, "always", StringComparison.Ordinal)) return true;
        if (string.Equals(condition, "optional", StringComparison.Ordinal)) return true;
        if (string.Equals(condition, "never", StringComparison.Ordinal)) return false;

        if (TryUnwrap(condition, "all", out var all))
        {
            return SplitTopLevel(all).All(part => EvaluateCondition(part, scope, selfValue));
        }

        if (TryUnwrap(condition, "modelPresent", out var present))
        {
            return ConditionValues(present, scope).Any(value => !value.IsAbsent);
        }

        if (TryUnwrap(condition, "modelTrue", out var isTrue))
        {
            return ConditionValues(isTrue, scope).Any(value => !value.IsAbsent);
        }

        if (TryUnwrap(condition, "modelNonZero", out var nonZero))
        {
            return ConditionValues(nonZero, scope)
                .Any(value => value is ClaimCsvValue.NumberValue { Value: not 0 });
        }

        if (TryUnwrap(condition, "modelIn", out var modelIn))
        {
            var parts = SplitTopLevel(modelIn);
            return MatchesToken(parts[0], parts.Skip(1), scope);
        }

        if (TryUnwrap(condition, "modelEquals", out var modelEquals))
        {
            var parts = SplitTopLevel(modelEquals);
            return MatchesToken(parts[0], parts.Skip(1), scope);
        }

        if (TryUnwrap(condition, "fieldPresent", out var fieldPresent))
        {
            return string.Equals(fieldPresent, scope.FieldId, StringComparison.Ordinal)
                ? !selfValue.IsAbsent
                : ReferenceValues(fieldPresent, scope).Any(value => !value.IsAbsent);
        }

        if (TryUnwrap(condition, "fieldNonZero", out var fieldNonZero))
        {
            return string.Equals(fieldNonZero, scope.FieldId, StringComparison.Ordinal)
                ? selfValue is ClaimCsvValue.NumberValue { Value: not 0 }
                : ReferenceValues(fieldNonZero, scope)
                    .Any(value => value is ClaimCsvValue.NumberValue { Value: not 0 });
        }

        if (TryUnwrap(condition, "serviceProvisionMonthBefore", out var serviceBefore))
        {
            return MonthKey(scope.Dto.ServiceMonth.Year, scope.Dto.ServiceMonth.Month)
                < int.Parse(serviceBefore, CultureInfo.InvariantCulture);
        }

        if (TryUnwrap(condition, "processingMonthBefore", out var processingBefore))
        {
            return MonthKey(scope.Dto.ProcessingMonth.Year, scope.Dto.ProcessingMonth.Month)
                < int.Parse(processingBefore, CultureInfo.InvariantCulture);
        }

        throw Unresolvable(scope.FieldId, $"condition '{condition}' has no evaluator");
    }

    /// <summary>
    /// 条件式が参照する別項目の値。参照先が対象行より細かいスコープに複数行あるとき
    /// （請求書の集計行から受給者ごとの明細項目を見るとき等）は、そのすべてを返して
    /// 「スコープ内のいずれかが満たすか」で判定できるようにする。細かいスコープに無い場合は
    /// 祖先スコープの 1 行へ落とす。
    /// </summary>
    private IEnumerable<ClaimCsvValue> ReferenceValues(string fieldId, ClaimCsvResolutionScope scope)
    {
        var rows = RowsInScope(fieldId, scope).ToArray();
        return rows.Length > 0
            ? rows.Select(row => Resolve(fieldId, row))
            : [Resolve(fieldId, ReferenceRow(fieldId, scope))];
    }

    private static bool MatchesToken(
        string path,
        IEnumerable<string> tokens,
        ClaimCsvResolutionScope scope)
    {
        var expected = tokens
            .Select(token => ClaimCsvModelPath.TryResolveEnumToken(path, token, out var value)
                ? value
                : (long?)null)
            .Where(value => value.HasValue)
            .Select(value => value!.Value)
            .ToArray();

        return ConditionValues(path, scope)
            .Any(value => value is ClaimCsvValue.NumberValue number && expected.Contains(number.Value));
    }

    /// <summary>
    /// 条件式が参照するモデル値。日次記録を指す経路を、日次より粗い行（受給者単位の
    /// 実績記録票 集計など）で評価するときは「スコープ内のいずれかの日が満たすか」を見る。
    /// </summary>
    private static IEnumerable<ClaimCsvValue> ConditionValues(string path, ClaimCsvResolutionScope scope)
    {
        if (!path.StartsWith("DailyRecord.", StringComparison.Ordinal)
            || scope.Row.DailyRecordIndex is not null)
        {
            return [ClaimCsvModelPath.Resolve(path, scope)];
        }

        if (scope.Recipient is not { } recipient) return [ClaimCsvValue.Missing];

        return Enumerable.Range(0, recipient.DailyRecords.Count)
            .Select(index => ClaimCsvModelPath.Resolve(
                path, scope with { Row = scope.Row with { DailyRecordIndex = index } }));
    }

    private static int MonthKey(int year, int month) => (year * 100) + month;

    private static string Render(ClaimCsvValue value, CsvFieldSpecification specification) => value switch
    {
        ClaimCsvValue.AbsentValue => string.Empty,
        ClaimCsvValue.TextValue text => text.Value,
        ClaimCsvValue.NumberValue number => number.Value.ToString(CultureInfo.InvariantCulture),
        ClaimCsvValue.MonthValue month => $"{month.Value.Year:D4}{month.Value.Month:D2}",
        ClaimCsvValue.TimeValue time when specification.DataType is "numeric" =>
            $"{time.Value.Hour:D2}{time.Value.Minute:D2}",
        ClaimCsvValue.DateValue date => specification.DataType switch
        {
            "date" => $"{date.Value.Year:D4}{date.Value.Month:D2}{date.Value.Day:D2}",
            "yearMonth" => $"{date.Value.Year:D4}{date.Value.Month:D2}",
            "code" or "numeric" => date.Value.Day.ToString(CultureInfo.InvariantCulture),
            _ => throw new ClaimCsvGenerationException(
                specification.FieldId,
                ClaimCsvGenerationReason.UnsupportedDataType,
                $"a date cannot be written to dataType '{specification.DataType}'"),
        },
        _ => throw new ClaimCsvGenerationException(
            specification.FieldId,
            ClaimCsvGenerationReason.UnsupportedDataType,
            $"the value cannot be written to dataType '{specification.DataType}'"),
    };

    // CultureInfo: 非該当（spec の DSL 構文解析であり、数値・日付の書式変換を含まない）
    private static CsvGeneratorRule ParseRule(string generatorRule) =>
        CsvGeneratorRuleParser.Parse(generatorRule); // CultureInfo: 非該当

    private static bool TryParseInvariant(string value, out long parsed) =>
        long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed);

    private static ClaimCsvGenerationException Unresolvable(string fieldId, string detail) =>
        new(fieldId, ClaimCsvGenerationReason.UnresolvableRule, detail);

    private static bool TryUnwrap(string value, string function, out string inner)
    {
        var prefix = $"{function}(";
        if (value.StartsWith(prefix, StringComparison.Ordinal) && value.EndsWith(')'))
        {
            inner = value[prefix.Length..^1];
            return true;
        }

        inner = string.Empty;
        return false;
    }

    private static List<string> SplitTopLevel(string value)
    {
        var parts = new List<string>();
        var depth = 0;
        var start = 0;
        for (var index = 0; index < value.Length; index++)
        {
            switch (value[index])
            {
                case '(':
                    depth++;
                    break;
                case ')':
                    depth--;
                    break;
                case ';' when depth == 0:
                    parts.Add(value[start..index]);
                    start = index + 1;
                    break;
                default:
                    break;
            }
        }

        parts.Add(value[start..]);
        return parts;
    }
}
