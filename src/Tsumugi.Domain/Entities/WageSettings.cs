using Tsumugi.Domain.Enums;
using Tsumugi.Domain.ValueObjects;

namespace Tsumugi.Domain.Entities;

/// <summary>工賃設定（期間マスタ、追記）。基準日時点の方式・端数規則・年度起点・手当規則を引く。</summary>
public sealed record WageSettings : Entity
{
    public required Guid OfficeId { get; init; }
    public required DateRange Period { get; init; }
    public required WageMethod Method { get; init; }
    public required RoundingRule Rounding { get; init; }
    public required RemainderPolicy Remainder { get; init; }
    public required int FiscalYearStartMonth { get; init; }
    public int? FixedDailyYen { get; init; }

    // v2 追加
    public int? WorkAllowancePerDayYen { get; init; }
    public required IReadOnlyList<SkillAllowanceTier> SkillAllowanceTiers { get; init; }
    public required int HourUnitMinutes { get; init; }

    private static readonly int[] AllowedHourUnitMinutes =
        [1, 2, 3, 4, 5, 6, 10, 12, 15, 20, 30, 60];

    public static WageSettings Create(
        Guid id, Guid officeId, DateRange period,
        WageMethod method, RoundingRule rounding, RemainderPolicy remainder,
        int fiscalYearStartMonth, int? fixedDailyYen,
        int? workAllowancePerDayYen,
        IReadOnlyList<SkillAllowanceTier>? skillAllowanceTiers,
        int hourUnitMinutes,
        string createdBy, DateTimeOffset createdAt)
    {
        if (fiscalYearStartMonth is < 1 or > 12)
            throw new ArgumentOutOfRangeException(nameof(fiscalYearStartMonth),
                fiscalYearStartMonth, "年度起点月は1〜12の範囲で指定してください。");
        if (method == WageMethod.Fixed && fixedDailyYen is null)
            throw new ArgumentException("Fixed 方式では FixedDailyYen を指定してください。", nameof(fixedDailyYen));
        if (fixedDailyYen is { } y && y < 0)
            throw new ArgumentOutOfRangeException(nameof(fixedDailyYen), y, "FixedDailyYen は0円以上で指定してください。");
        if (workAllowancePerDayYen is { } w && w < 0)
            throw new ArgumentOutOfRangeException(nameof(workAllowancePerDayYen), w,
                "作業手当日額は0円以上で指定してください。");

        var tiers = skillAllowanceTiers ?? Array.Empty<SkillAllowanceTier>();
        for (var i = 0; i < tiers.Count; i++)
        {
            if (tiers[i].MinHours < 0)
                throw new ArgumentOutOfRangeException(nameof(skillAllowanceTiers),
                    tiers[i].MinHours, "職能手当の閾値時間は 0 以上で指定してください。");
            if (tiers[i].Yen < 0)
                throw new ArgumentOutOfRangeException(nameof(skillAllowanceTiers),
                    tiers[i].Yen, "職能手当の金額は0円以上で指定してください。");
            if (i > 0 && tiers[i].MinHours <= tiers[i - 1].MinHours)
            {
                var reason = tiers[i].MinHours == tiers[i - 1].MinHours ? "重複しています" : "昇順ではありません";
                throw new ArgumentException($"職能手当の閾値が{reason}。", nameof(skillAllowanceTiers));
            }
        }

        if (!AllowedHourUnitMinutes.Contains(hourUnitMinutes))
            throw new ArgumentException(
                "工賃時給の最小単位は 60 の約数（1〜60 分単位）で指定してください。",
                nameof(hourUnitMinutes));

        return new WageSettings
        {
            Id = id,
            OfficeId = officeId,
            Period = period,
            Method = method,
            Rounding = rounding,
            Remainder = remainder,
            FiscalYearStartMonth = fiscalYearStartMonth,
            FixedDailyYen = fixedDailyYen,
            WorkAllowancePerDayYen = workAllowancePerDayYen,
            SkillAllowanceTiers = tiers,
            HourUnitMinutes = hourUnitMinutes,
            CreatedBy = createdBy,
            CreatedAt = createdAt,
            ConcurrencyToken = Guid.NewGuid(),
        };
    }

}
