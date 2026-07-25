namespace Tsumugi.Infrastructure.Csv.Generation;

/// <summary>
/// <c>generatorRule</c> 文字列を <see cref="CsvGeneratorRule"/> へ構文解析する。
/// 語彙（head）は spec JSON に現れる 17 種に限定し、未知の head は fail-close する。
/// </summary>
public static class CsvGeneratorRuleParser
{
    /// <summary>spec JSON に現れる generatorRule の head 語彙。ここに無い head は解析エラーにする。</summary>
    public static IReadOnlySet<string> KnownHeads { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        "const",
        "constEmpty",
        "copy",
        "sum",
        "count",
        "min",
        "max",
        "multiply",
        "difference",
        "roundDown",
        "conditional",
        "format",
        "calendarDay",
        "lookup",
        "sequence",
        "recordCount",
        "payload",
    };

    public static CsvGeneratorRule Parse(string generatorRule)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(generatorRule);

        var openIndex = generatorRule.IndexOf('(', StringComparison.Ordinal);
        if (openIndex <= 0 || !generatorRule.EndsWith(')'))
        {
            throw new CsvGeneratorRuleException(
                string.Empty, "generator rule must have the shape 'head(key=value;...)'");
        }

        var head = generatorRule[..openIndex];
        if (!KnownHeads.Contains(head))
        {
            throw new CsvGeneratorRuleException(string.Empty, $"unknown generator rule head '{head}'");
        }

        var arguments = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var part in SplitTopLevel(generatorRule[(openIndex + 1)..^1]))
        {
            var separatorIndex = part.IndexOf('=', StringComparison.Ordinal);
            if (separatorIndex <= 0)
            {
                throw new CsvGeneratorRuleException(
                    arguments.GetValueOrDefault("target", string.Empty),
                    $"generator rule argument '{part}' is not a key=value pair");
            }

            var key = part[..separatorIndex];
            if (!arguments.TryAdd(key, part[(separatorIndex + 1)..]))
            {
                throw new CsvGeneratorRuleException(
                    arguments.GetValueOrDefault("target", string.Empty),
                    $"generator rule argument '{key}' is duplicated");
            }
        }

        var rule = new CsvGeneratorRule(head, arguments);
        _ = rule.Target; // target は全ルール必須。欠けていればここで fail-close する。
        return rule;
    }

    /// <summary>括弧の入れ子を尊重してセミコロンで分割する。</summary>
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
                    if (depth < 0)
                    {
                        throw new CsvGeneratorRuleException(string.Empty, "unbalanced parentheses");
                    }

                    break;
                case ';' when depth == 0:
                    parts.Add(value[start..index]);
                    start = index + 1;
                    break;
                default:
                    break;
            }
        }

        if (depth != 0)
        {
            throw new CsvGeneratorRuleException(string.Empty, "unbalanced parentheses");
        }

        parts.Add(value[start..]);
        return parts;
    }
}
