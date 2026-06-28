using Tsumugi.Domain.Entities;
using Tsumugi.Domain.Enums;

namespace Tsumugi.Domain.Logic.Wage;

public sealed record WageInputs(
    Guid RecipientId,
    int PresentDays,
    int TotalWorkedMinutes,
    int TotalPieceAmountYen,
    int TotalPoints);

public sealed record WageLineItem(Guid RecipientId, int AmountYen, string BasisSummary);

public interface IWageMethodStrategy
{
    WageMethod Method { get; }
    IReadOnlyList<WageLineItem> Calculate(
        IReadOnlyList<WageInputs> inputs,
        WageFund? fund,
        WageSettings settings);
}
