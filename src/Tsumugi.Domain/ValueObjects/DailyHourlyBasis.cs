namespace Tsumugi.Domain.ValueObjects;

/// <summary>日単位の時給・時間の基礎値。RecipientHourlyRate の実効値と WorkRecord から Application 層で組み立てる。</summary>
public sealed record DailyHourlyBasis(DateOnly Date, int Minutes, int HourlyYen);
