namespace Tsumugi.Domain.Entities;

/// <summary>
/// 汎用 pass-through 入力の 1 値（ADR 0042）。親 <see cref="ClaimInput"/> の revision に属し、
/// 独立した履歴を持たない（訂正は親の新 revision で集合を作り直す）。
/// </summary>
/// <remarks>
/// 値は文字列 1 列で持つ。型・桁数・ラベルは CSV 仕様側の宣言が正本で、入力時に
/// <c>IClaimGenericFieldCatalog</c> 由来の宣言で検証する（Domain に制度実値を持ち込まない）。
/// </remarks>
public sealed record ClaimInputGenericValue
{
    /// <summary>この行の識別子（EF の主キー）。</summary>
    public required Guid Id { get; init; }

    /// <summary>親の請求入力 revision。</summary>
    public required Guid ClaimInputId { get; init; }

    /// <summary>値の名前（readiness path は <c>ClaimGenericInput.{Name}</c>）。</summary>
    public required string Name { get; init; }

    /// <summary>値（文字列表現）。</summary>
    public required string Value { get; init; }

    public static ClaimInputGenericValue Create(Guid id, Guid claimInputId, string name, string value)
    {
        if (id == Guid.Empty) throw new ArgumentException("IDが空です。", nameof(id));
        if (claimInputId == Guid.Empty) throw new ArgumentException("IDが空です。", nameof(claimInputId));
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        return new ClaimInputGenericValue
        {
            Id = id,
            ClaimInputId = claimInputId,
            Name = name,
            Value = value,
        };
    }
}
