using Tsumugi.Application.Claim;

namespace Tsumugi.Application.Dtos;

/// <summary>次の施行分（事前登録済みの将来版）で、項目の要求がどう変わるか。</summary>
public enum ClaimUpcomingSpecificationChange
{
    /// <summary>現行版では求められないが、次の施行分では必要になる。</summary>
    BecomesRequired = 1,

    /// <summary>
    /// 現行版では不足として止まっているが、次の施行分では求められなくなる。
    /// <b>今月の確定は止め続ける</b>（現行版の適用期間内に提出するなら必要な項目なので、
    /// 自動で緩めると提出先で弾かれる）。どちらで提出するかは運用者が決める。
    /// </summary>
    BecomesOptional = 2,
}

/// <summary>
/// 事前登録済みの将来の施行分での要求の変化（ADR 0041）。確定は止めない情報。
/// </summary>
/// <param name="SpecificationVersion">対象の将来の仕様版。</param>
/// <param name="Change">要求がどう変わるか。</param>
/// <param name="Issue">
/// 対象の項目。<see cref="ClaimUpcomingSpecificationChange.BecomesRequired"/> なら
/// 「次の施行分で不足になる項目」、<see cref="ClaimUpcomingSpecificationChange.BecomesOptional"/> なら
/// 「現行版では不足しているが次の施行分では不要になる項目」。
/// </param>
public sealed record ClaimUpcomingSpecificationIssue(
    string SpecificationVersion,
    ClaimUpcomingSpecificationChange Change,
    ClaimPreparationIssue Issue);
