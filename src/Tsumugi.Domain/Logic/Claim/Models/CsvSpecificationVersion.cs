namespace Tsumugi.Domain.Logic.Claim.Models;

/// <summary>国保連CSVのfield・record仕様を選択する永続版。</summary>
public readonly record struct CsvSpecificationVersion
{
    public const int MaxLength = 64;

    public string Value { get; }

    public CsvSpecificationVersion(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        if (value.Length > MaxLength)
            throw new ArgumentOutOfRangeException(nameof(value), value.Length, $"版は{MaxLength}文字以下で指定してください。");

        Value = value;
    }

    public override string ToString() => Value;
}
