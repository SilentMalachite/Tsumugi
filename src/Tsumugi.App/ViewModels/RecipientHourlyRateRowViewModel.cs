using Tsumugi.Application.Dtos;

namespace Tsumugi.App.ViewModels;

/// <summary>利用者別時給期間の一行表示用 ViewModel。</summary>
public sealed class RecipientHourlyRateRowViewModel(RecipientHourlyRateDto dto)
{
    public int HourlyYen => dto.HourlyYen;

    public string PeriodDisplay => dto.Period.End.HasValue
        ? $"{dto.Period.Start:yyyy-MM-dd} 〜 {dto.Period.End:yyyy-MM-dd}"
        : $"{dto.Period.Start:yyyy-MM-dd} 〜 （継続中）";
}
