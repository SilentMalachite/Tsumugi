using Tsumugi.Domain.Entities;
using Tsumugi.Domain.Enums;
using Tsumugi.Domain.ValueObjects;

namespace Tsumugi.Domain.Logic.Wage;

public sealed record WageInputs(
    Guid RecipientId,
    int PresentDays,
    int TotalWorkedMinutes,
    int TotalPieceAmountYen,
    int TotalPoints)
{
    public IReadOnlyList<DailyHourlyBasis>? DailyBreakdown { get; init; }
}

public sealed record WageLineItem(Guid RecipientId, int AmountYen, string BasisSummary);

public interface IWageMethodStrategy
{
    WageMethod Method { get; }
    IReadOnlyList<WageLineItem> Calculate(
        IReadOnlyList<WageInputs> inputs,
        WageFund? fund,
        WageSettings settings);
}
