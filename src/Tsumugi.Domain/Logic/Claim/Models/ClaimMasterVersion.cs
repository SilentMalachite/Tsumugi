namespace Tsumugi.Domain.Logic.Claim.Models;

/// <summary>算定マスタと出典束を一意に選択する永続版。</summary>
public readonly record struct ClaimMasterVersion
{
    private readonly string? _value;

    public const int MaxLength = 64;

    public string Value
    {
        get
        {
            EnsureValid();
            return _value!;
        }
    }

    public ClaimMasterVersion(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        if (value.Length != value.Trim().Length)
            throw new ArgumentException("版の前後に空白を含められません。", nameof(value));
        if (value.Length > MaxLength)
            throw new ArgumentOutOfRangeException(nameof(value), value.Length, $"版は{MaxLength}文字以下で指定してください。");

        _value = value;
    }

    public bool Equals(ClaimMasterVersion other)
    {
        EnsureValid();
        other.EnsureValid();
        return string.Equals(_value, other._value, StringComparison.Ordinal);
    }

    public override int GetHashCode()
    {
        EnsureValid();
        return StringComparer.Ordinal.GetHashCode(_value!);
    }

    public override string ToString()
    {
        EnsureValid();
        return _value!;
    }

    private void EnsureValid()
    {
        if (string.IsNullOrWhiteSpace(_value)
            || _value.Length > MaxLength
            || _value.Length != _value.Trim().Length)
        {
            throw new InvalidOperationException("ClaimMasterVersionが初期化されていません。");
        }
    }
}
