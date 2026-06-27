namespace Tsumugi.Domain.Enums;

/// <summary>地域区分（1〜7級地・その他）。報酬告示由来。フェーズ3で単価と突合する。</summary>
public enum RegionGrade
{
    None = 0,
    Grade1 = 1,
    Grade2 = 2,
    Grade3 = 3,
    Grade4 = 4,
    Grade5 = 5,
    Grade6 = 6,
    Grade7 = 7,
    Other = 99,
}
