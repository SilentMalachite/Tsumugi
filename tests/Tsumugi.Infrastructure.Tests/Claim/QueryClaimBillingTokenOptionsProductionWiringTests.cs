using FluentAssertions;
using Tsumugi.Application.UseCases.Claim;
using Tsumugi.Domain.ValueObjects;
using Tsumugi.Infrastructure.ClaimMasters;

namespace Tsumugi.Infrastructure.Tests.Claim;

/// <summary>
/// Phase 3-6 Task 3 review: <see cref="QueryClaimBillingTokenOptionsUseCase"/>の体制届選択肢は
/// 実<see cref="JsonClaimMasterProvider.LoadEmbedded"/>のR6-06 seed（Task 1・2投入分）を対象月別に
/// 検証しないと意味が無い。<c>Tsumugi.Application.Tests</c>はApplication層のみを参照し
/// Infrastructureへは決して手を伸ばさない（<c>ArchitectureTests</c>・依存方向厳守）ため、
/// 実seedを使う検証は本ファイル（既にApplication/Infrastructure双方を参照する
/// <see cref="ClaimPreviewProductionWiringTests"/>・<see cref="ClaimCsvExportProductionWiringTests"/>と
/// 同じ置き場所）に置く。合成データ（<c>FakeMasterProvider</c>）による2ファミリー分離の単体テストは
/// <c>Tsumugi.Application.Tests.UseCases.Claim.QueryClaimBillingTokenOptionsCapabilityTests</c>に残す。
/// </summary>
public sealed class QueryClaimBillingTokenOptionsProductionWiringTests
{
    private static readonly QueryClaimBillingTokenOptionsUseCase UseCase =
        new(JsonClaimMasterProvider.LoadEmbedded());

    /// <summary>
    /// 体制届の選択番号はseedの条件定義にのみ存在し、UI/Applicationへハードコードしない
    /// （CLAUDE.md ハード制約3）。R6世代は(Ⅰ)〜(Ⅴ)＝option 2〜6。
    /// </summary>
    [Fact]
    public void R6_generation_exposes_options_two_through_six()
    {
        var dto = UseCase.Execute(new ServiceMonth(2024, 6));

        dto.TreatmentImprovementOptions.Should().Equal(2, 3, 4, 5, 6);
    }

    /// <summary>
    /// (Ⅴ)は2025-03限りで失効するため、2025-04以降のoption 6は選択肢から消える。
    /// </summary>
    [Fact]
    public void Category_v_disappears_after_march_2025()
    {
        UseCase.Execute(new ServiceMonth(2025, 3))
            .TreatmentImprovementOptions.Should().Contain(6);
        UseCase.Execute(new ServiceMonth(2025, 4))
            .TreatmentImprovementOptions.Should().NotContain(6);
    }

    /// <summary>
    /// R8世代は(Ⅰ)イ=2・(Ⅱ)イ=3・(Ⅲ)=4・(Ⅳ)=5・(Ⅰ)ロ=7・(Ⅱ)ロ=8。
    /// B型に(Ⅴ)は存在しないためoption 6は出ない（ADR 0048）。
    /// </summary>
    [Fact]
    public void R8_generation_exposes_the_six_reformed_options_without_category_v()
    {
        var dto = UseCase.Execute(new ServiceMonth(2026, 6));

        dto.TreatmentImprovementOptions.Should().Equal(2, 3, 4, 5, 7, 8);
    }

    /// <summary>
    /// (Ⅴ)区分の14択はR6の(Ⅴ)有効期間にのみ現れる。
    /// </summary>
    [Fact]
    public void The_category_v_band_options_exist_only_while_category_v_is_effective()
    {
        UseCase.Execute(new ServiceMonth(2024, 6))
            .TreatmentImprovementVBandOptions
            .Should().Equal(1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14);

        UseCase.Execute(new ServiceMonth(2026, 6))
            .TreatmentImprovementVBandOptions.Should().BeEmpty();
    }

    /// <summary>
    /// <c>mhlw.b46.capability.treatment-improvement.</c>への接頭辞一致は
    /// <c>mhlw.b46.capability.treatment-improvement-v-band.</c>を拾ってはならない
    /// （接頭辞が"."で終端するため、直後がハイフンの後者は前者のプレフィックスに一致しない）。
    /// R6世代の実seedで、通常区分の選択番号域（2〜6）と(Ⅴ)区分の選択番号域（1〜14。7〜14は
    /// 通常区分に存在しない値）を使い、2ファミリーが互いに混入しないことを直接検査する。
    /// </summary>
    [Fact]
    public void Treatment_improvement_and_v_band_families_do_not_bleed_into_each_other()
    {
        var dto = UseCase.Execute(new ServiceMonth(2024, 6));

        // v-band専用の選択番号（7〜14）が通常区分側へ混入していない。
        dto.TreatmentImprovementOptions.Should().NotContain([7, 8, 9, 10, 11, 12, 13, 14]);

        // 通常区分の(Ⅴ)自体を示す選択番号6は、(Ⅴ)区分側（サブ区分1〜14）とは別の語彙であり
        // v-band側の選択肢には現れない（v-bandはサブ区分1〜14そのものを列挙する）。
        dto.TreatmentImprovementVBandOptions.Should().Equal(1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14);
    }

    /// <summary>
    /// I1: 「(Ⅴ)区分を併せて要求する選択番号」は実seedの service-code 行から導出する
    /// （どの選択番号が(Ⅴ)かをコードに書かない）。R6-06世代では option 6 の行だけが
    /// <c>treatment-improvement-v-band.*</c> 条件を同じ行で要求する（ADR 0048 決定4の二重ゲート）。
    /// </summary>
    [Fact]
    public void Only_category_v_requires_a_band_in_the_r6_generation()
    {
        UseCase.Execute(new ServiceMonth(2024, 6))
            .TreatmentImprovementOptionsRequiringVBand.Should().Equal(6);
    }

    /// <summary>
    /// I1: (Ⅴ)が失効した月・B型に(Ⅴ)が無いR8世代では、bandを要求する選択番号は存在しない。
    /// 保存ガードが常時発火して体制届の登録を殺していないことをseed側から固定する。
    /// </summary>
    [Theory]
    [InlineData(2025, 4)]
    [InlineData(2026, 6)]
    public void No_option_requires_a_band_once_category_v_is_gone(int year, int month)
    {
        UseCase.Execute(new ServiceMonth(year, month))
            .TreatmentImprovementOptionsRequiringVBand.Should().BeEmpty();
    }
}
