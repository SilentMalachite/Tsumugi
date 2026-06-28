using Tsumugi.Domain.Entities;
using Tsumugi.Domain.Enums;

namespace Tsumugi.Domain.Logic.Wage;

/// <summary>出来高方式: 実効 WorkRecord の Σ(PieceCount × PieceUnitYen) をそのまま採用する。</summary>
public sealed class PieceWageStrategy : IWageMethodStrategy
{
    public WageMethod Method => WageMethod.Piece;

    public IReadOnlyList<WageLineItem> Calculate(
        IReadOnlyList<WageInputs> inputs, WageFund? fund, WageSettings settings)
    {
        ArgumentNullException.ThrowIfNull(inputs);
        ArgumentNullException.ThrowIfNull(settings);
        return inputs
            .Select(i => new WageLineItem(
                i.RecipientId, i.TotalPieceAmountYen,
                $"出来高方式: 合計{i.TotalPieceAmountYen:N0}円"))
            .ToArray();
    }
}
