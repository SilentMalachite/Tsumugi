namespace Tsumugi.Domain.ValueObjects;

/// <summary>職能手当の 1 段（就労時間の閾値と支給額）。妥当性は WageSettings.Create が集合単位で判定する。</summary>
public sealed record SkillAllowanceTier(int MinHours, int Yen);
