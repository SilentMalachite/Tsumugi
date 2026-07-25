using System.Collections.ObjectModel;

namespace Tsumugi.Infrastructure.Csv.Generation;

/// <summary>
/// <c>field-mapping-r7-10.json</c> の <c>generatorRule</c> を構文解析した表現。
/// <c>head(key=value;key=value;...)</c> 形式で、値の中に <c>modelPresent(X)</c> のような
/// 入れ子の括弧を含みうる。
/// </summary>
public sealed record CsvGeneratorRule
{
    internal CsvGeneratorRule(string head, IDictionary<string, string> arguments)
    {
        Head = head;
        Arguments = new ReadOnlyDictionary<string, string>(arguments);
    }

    public string Head { get; }

    public IReadOnlyDictionary<string, string> Arguments { get; }

    /// <summary>ルールが書き込む対象 fieldId（<c>target=</c>）。</summary>
    public string Target => Require("target");

    /// <summary>出典（<c>source=</c>）。監査証跡としてのみ使い、評価には用いない。</summary>
    public string Source => Require("source");

    public string Require(string name) =>
        Arguments.TryGetValue(name, out var value)
            ? value
            : throw new CsvGeneratorRuleException(
                Arguments.GetValueOrDefault("target", string.Empty),
                $"generator rule '{Head}' is missing the required argument '{name}'");

    public string? Find(string name) => Arguments.GetValueOrDefault(name);

    /// <summary>カンマ区切りの引数を分解する（<c>fields=</c> / <c>directions=</c> 等）。</summary>
    public IReadOnlyList<string> RequireList(string name) =>
        Require(name)
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
}

/// <summary>generatorRule の構文・語彙が想定外だったときの fail-close 例外。</summary>
public sealed class CsvGeneratorRuleException : Exception
{
    public CsvGeneratorRuleException(string target, string detail)
        : base($"CSV generator rule failed: target={target}, detail={detail}")
    {
        Target = target;
        Detail = detail;
    }

    public CsvGeneratorRuleException()
        : this(string.Empty, "unspecified")
    {
    }

    public CsvGeneratorRuleException(string message)
        : base(message)
    {
        Target = string.Empty;
        Detail = message;
    }

    public CsvGeneratorRuleException(string message, Exception innerException)
        : base(message, innerException)
    {
        Target = string.Empty;
        Detail = message;
    }

    public string Target { get; } = string.Empty;
    public string Detail { get; } = string.Empty;
}
