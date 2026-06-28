using Tsumugi.Domain.Entities;
using Tsumugi.Domain.Enums;

namespace Tsumugi.Domain.Logic.Wage;

/// <summary>均等方式: PresentDays > 0 の対象者で WageFund.TotalYen を均等割（AllocationPolicy 経由）。</summary>
public sealed class EqualWageStrategy : IWageMethodStrategy
{
    public WageMethod Method => WageMethod.Equal;

    public IReadOnlyList<WageLineItem> Calculate(
        IReadOnlyList<WageInputs> inputs, WageFund? fund, WageSettings settings)
    {
        ArgumentNullException.ThrowIfNull(inputs);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(fund);

        var shares = inputs
            .Select(i => (i.RecipientId, Weight: i.PresentDays > 0 ? 1m : 0m))
            .ToArray();

        var alloc = AllocationPolicy.Allocate(
            shares, fund.TotalYen, settings.Rounding, settings.Remainder,
            officeReserveKey: settings.Remainder == RemainderPolicy.ReserveToOffice ? settings.OfficeId : null);

        return inputs
            .Select(i => new WageLineItem(
                i.RecipientId,
                alloc.First(a => a.Key == i.RecipientId).AmountYen,
                $"均等方式: 出席{i.PresentDays}日 / 対象者で均等割"))
            .ToArray();
    }
}
