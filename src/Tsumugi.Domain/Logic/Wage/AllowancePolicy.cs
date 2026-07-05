// src/Tsumugi.Domain/Logic/Wage/AllowancePolicy.cs
using Tsumugi.Domain.Entities;

namespace Tsumugi.Domain.Logic.Wage;

/// <summary>作業手当・職能手当の共通算定。4 方式 Strategy から共用する純粋関数。</summary>
public static class AllowancePolicy
{
    /// <summary>作業手当: 出席日数 × 日額（未設定は 0 円）。</summary>
    public static int WorkAllowanceYen(WageSettings settings, int presentDays)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return presentDays * (settings.WorkAllowancePerDayYen ?? 0);
    }

    /// <summary>職能手当: 総就労時間（時間未満切り捨て）が閾値以上の最上位段の額（該当なしは 0 円）。</summary>
    public static int SkillAllowanceYen(WageSettings settings, int totalWorkedMinutes)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var totalHours = totalWorkedMinutes / 60;
        // SkillAllowanceTiers は WageSettings.Create で MinHours 昇順が保証されている
        return settings.SkillAllowanceTiers
            .LastOrDefault(t => totalHours >= t.MinHours)?.Yen ?? 0;
    }
}
