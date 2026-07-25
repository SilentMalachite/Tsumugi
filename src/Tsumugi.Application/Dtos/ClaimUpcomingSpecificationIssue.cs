using Tsumugi.Application.Claim;

namespace Tsumugi.Application.Dtos;

/// <summary>
/// 事前登録済みの将来の施行分で必要になる項目（ADR 0041）。確定は止めない警告。
/// </summary>
/// <param name="SpecificationVersion">その項目を要求する将来の仕様版。</param>
/// <param name="Issue">不足内容（現行版の readiness と同じ形）。</param>
public sealed record ClaimUpcomingSpecificationIssue(
    string SpecificationVersion,
    ClaimPreparationIssue Issue);
