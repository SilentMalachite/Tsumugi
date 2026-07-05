// src/Tsumugi.Domain/Logic/RoundingPolicy.cs
using Tsumugi.Domain.Enums;

namespace Tsumugi.Domain.Logic;

/// <summary>工賃計算の端数規則を集約する純粋関数（KouchinModule v5 は HalfUp）。</summary>
public static class RoundingPolicy
{
    public static int Round(decimal amount, RoundingRule rule) => rule switch
    {
        RoundingRule.FloorYen => (int)Math.Floor(amount),
        RoundingRule.HalfUp => (int)Math.Round(amount, 0, MidpointRounding.AwayFromZero),
        RoundingRule.Ceiling => (int)Math.Ceiling(amount),
        _ => throw new ArgumentOutOfRangeException(nameof(rule), rule, "未知の端数規則です。"),
    };
}
