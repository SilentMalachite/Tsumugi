using Tsumugi.Domain.Entities;
using Tsumugi.Domain.Enums;

namespace Tsumugi.Domain.Logic.Wage;

/// <summary>時間割方式: WageFund.TotalYen を TotalWorkedMinutes 比で配分（AllocationPolicy 経由）。</summary>
public sealed class HourlyWageStrategy : IWageMethodStrategy
{
    public WageMethod Method => WageMethod.Hourly;

    public IReadOnlyList<WageLineItem> Calculate(
        IReadOnlyList<WageInputs> inputs, WageFund? fund, WageSettings settings)
    {
        ArgumentNullException.ThrowIfNull(inputs);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(fund);

        var shares = inputs
            .Select(i => (i.RecipientId, (decimal)i.TotalWorkedMinutes))
            .ToArray();

        var alloc = AllocationPolicy.Allocate(
            shares, fund.TotalYen, settings.Rounding, settings.Remainder,
            officeReserveKey: settings.Remainder == RemainderPolicy.ReserveToOffice ? settings.OfficeId : null);

        return inputs
            .Select(i => new WageLineItem(
                i.RecipientId,
                alloc.First(a => a.Key == i.RecipientId).AmountYen,
                $"時間割方式: {i.TotalWorkedMinutes}分 / 原資{fund.TotalYen:N0}円"))
            .ToArray();
    }
}
