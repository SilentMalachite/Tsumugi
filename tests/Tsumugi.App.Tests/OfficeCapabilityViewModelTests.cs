using FluentAssertions;
using Tsumugi.App.ViewModels;
using Tsumugi.Application.Abstractions;
using Tsumugi.Application.UseCases;
using Tsumugi.Application.UseCases.Claim;
using Tsumugi.Application.UseCases.Office;
using Tsumugi.Application.UseCases.OfficeCapability;
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.Logic.Claim.Models;
using Tsumugi.Domain.ValueObjects;
using Xunit;

namespace Tsumugi.App.Tests;

public sealed class OfficeCapabilityViewModelTests
{
    private readonly InMemoryOfficeRepo _offices = new();
    private readonly InMemoryOfficeCapabilityRepo _caps = new();
    private readonly InMemoryUow _uow = new();

    /// <summary>
    /// 既定の適用期間は現在月から導かれる（固定日を書くと、その日が属する世代の選択肢しか
    /// 出せなくなる）。テストはR6世代の月へ時計を固定する。月央の時刻にしてあるのは
    /// ローカルタイムゾーン差（-12〜+14時間）で月がずれないようにするため。
    /// </summary>
    private readonly FixedClock _clock = new(new DateTimeOffset(2025, 8, 15, 12, 0, 0, TimeSpan.Zero));

    private static readonly DateOnly DefaultPeriodStart = new(2025, 8, 1);

    private OfficeCapabilityViewModel CreateViewModel(TimeProvider? clock = null) => new(
        new RegisterOfficeCapabilityUseCase(_caps, _uow, clock ?? _clock),
        new ListOfficesUseCase(_offices),
        new QueryClaimBillingTokenOptionsUseCase(new FakeCapabilityClaimMasterProvider()),
        clock ?? _clock);

    private IReadOnlyDictionary<string, bool> SavedFlags => _caps.Last.Flags;

    [Fact]
    public async Task InitializeAsync_loads_offices_for_selection()
    {
        var o = Office.Create(Guid.NewGuid(), "1234567890", "事業所",
            Tsumugi.Domain.Enums.ServiceCategory.TypeB, Tsumugi.Domain.Enums.RegionGrade.None,
            "u", DateTimeOffset.UnixEpoch, Guid.NewGuid());
        _offices.Add(o);

        var vm = CreateViewModel();
        await vm.InitializeAsync();

        vm.Offices.Should().ContainSingle(x => x.Id == o.Id);
    }

    [Fact]
    public async Task SaveCommand_registers_capability_for_selected_office()
    {
        var oid = Guid.NewGuid();
        var vm = CreateViewModel();
        vm.SelectedOffice = new Tsumugi.Application.Dtos.OfficeDto(
            oid, "1234567890", "事業所",
            Tsumugi.Domain.Enums.ServiceCategory.TypeB,
            Tsumugi.Domain.Enums.RegionGrade.None,
            Guid.NewGuid());
        vm.PeriodStart = new DateOnly(2026, 4, 1);
        vm.PeriodEnd = new DateOnly(2027, 3, 31);
        vm.MealProvision = true;
        vm.TransportSupport = false;

        await vm.SaveCommand.ExecuteAsync(null);

        vm.SaveErrorMessage.Should().BeNull();
        vm.IsSaved.Should().BeTrue();
        _caps.Count.Should().Be(1);
        _caps.Last.OfficeId.Should().Be(oid);
        _caps.Last.Flags["mealProvision"].Should().BeTrue();
        _caps.Last.Flags["transportSupport"].Should().BeFalse();
    }

    [Fact]
    public async Task SaveCommand_without_selected_office_sets_error()
    {
        var vm = CreateViewModel();
        vm.PeriodStart = new DateOnly(2026, 4, 1);
        vm.PeriodEnd = new DateOnly(2027, 3, 31);

        await vm.SaveCommand.ExecuteAsync(null);

        vm.SaveErrorMessage.Should().NotBeNullOrEmpty();
        vm.IsSaved.Should().BeFalse();
        _caps.Count.Should().Be(0);
    }

    /// <summary>
    /// ADR 0021の公式one-hotキーを書き込む。旧暫定キー（mealProvision等）だけを
    /// 書いていると、マスタ側の条件に一致せず処遇改善加算が無音で0円になる。
    /// </summary>
    [Fact]
    public async Task SaveAsync_writes_the_official_treatment_improvement_key()
    {
        var vm = CreateViewModel();
        await vm.InitializeAsync();
        vm.OfficeId = Guid.NewGuid();
        vm.TreatmentImprovementOption = 2;

        await vm.SaveCommand.ExecuteAsync(null);

        SavedFlags.Should().ContainKey("mhlw.b46.capability.treatment-improvement.2")
            .WhoseValue.Should().BeTrue();
    }

    /// <summary>
    /// 選択されていない選択番号のキーは書かない（one-hot）。書くと複数区分が
    /// 同時に一致し、AmbiguousMatchまたは二重計上になる。
    /// </summary>
    [Fact]
    public async Task SaveAsync_writes_only_the_selected_option_as_one_hot()
    {
        var vm = CreateViewModel();
        await vm.InitializeAsync();
        vm.OfficeId = Guid.NewGuid();
        vm.TreatmentImprovementOption = 4;

        await vm.SaveCommand.ExecuteAsync(null);

        SavedFlags.Keys
            .Where(k => k.StartsWith("mhlw.b46.capability.treatment-improvement.", StringComparison.Ordinal))
            .Should().ContainSingle()
            .Which.Should().Be("mhlw.b46.capability.treatment-improvement.4");
    }

    /// <summary>
    /// (Ⅴ)区分は、その月に選択肢が存在し、かつ選択されているときに書く。
    /// 処遇改善対象の選択番号との突き合わせはViewModelで行わない —— seedの(Ⅴ)行が
    /// `capability-treatment-improvement-v`（option 6）と`-v-band-{n}`の**両方**を
    /// conditionSelectorsに要求するため、対応関係はマスタ条件が強制する。
    /// UI側で「どの選択番号が(Ⅴ)か」を導出すると選択番号の並びに暗黙依存する。
    /// </summary>
    [Fact]
    public async Task SaveAsync_writes_the_selected_category_v_band()
    {
        var vm = CreateViewModel();
        await vm.InitializeAsync();
        vm.OfficeId = Guid.NewGuid();
        vm.TreatmentImprovementOption = 6;
        vm.TreatmentImprovementVBand = 3;

        await vm.SaveCommand.ExecuteAsync(null);

        SavedFlags.Should().ContainKey("mhlw.b46.capability.treatment-improvement-v-band.3")
            .WhoseValue.Should().BeTrue();
    }

    /// <summary>
    /// (Ⅴ)区分の選択肢が存在しない月（R8世代など）では、band が選ばれていても書かない。
    /// 存在しない語彙のキーを宣言すると、存在検査（ADR 0049）が毎月警告する。
    /// </summary>
    [Fact]
    public async Task SaveAsync_does_not_write_a_band_when_the_month_has_no_band_options()
    {
        var vm = CreateViewModel();
        await vm.InitializeAsync();
        vm.OfficeId = Guid.NewGuid();
        vm.PeriodStart = new DateOnly(2026, 6, 1);   // R8世代: (Ⅴ)区分の選択肢は空
        vm.TreatmentImprovementOption = 2;
        vm.TreatmentImprovementVBand = 3;

        await vm.SaveCommand.ExecuteAsync(null);

        SavedFlags.Keys.Should().NotContain(
            k => k.StartsWith("mhlw.b46.capability.treatment-improvement-v-band.", StringComparison.Ordinal));
    }

    /// <summary>
    /// DiscardCommand (Escape) は画面を初期状態へ戻す。旧実装は PeriodStart を
    /// DateOnly の既定値 (0001-01-01) へ戻しており、OnPeriodStartChanged 経由で
    /// ServiceMonth(1, 1) の構築に失敗する（例外を握りつぶす実装であっても、
    /// 選択肢一覧が空のまま固まり対象区分のComboBoxが理由も無く使えなくなる）。
    /// 破棄後も既定の適用期間(R6)の選択肢が読み込まれた状態に戻ることを固定する。
    /// </summary>
    [Fact]
    public async Task DiscardCommand_resets_state_to_the_default_period_without_throwing()
    {
        var vm = CreateViewModel();
        await vm.InitializeAsync();
        vm.SelectedOffice = new Tsumugi.Application.Dtos.OfficeDto(
            Guid.NewGuid(), "1234567890", "事業所",
            Tsumugi.Domain.Enums.ServiceCategory.TypeB,
            Tsumugi.Domain.Enums.RegionGrade.None,
            Guid.NewGuid());
        vm.PeriodStart = new DateOnly(2026, 6, 1);
        vm.PeriodEnd = new DateOnly(2027, 3, 31);
        vm.MealProvision = true;
        vm.TransportSupport = true;
        vm.TreatmentImprovementOption = 2;

        var act = () => vm.DiscardCommand.Execute(null);

        act.Should().NotThrow();
        vm.SelectedOffice.Should().BeNull();
        vm.PeriodStart.Should().Be(DefaultPeriodStart);
        vm.PeriodEnd.Should().BeNull();
        vm.MealProvision.Should().BeFalse();
        vm.TransportSupport.Should().BeFalse();
        vm.TreatmentImprovementOption.Should().BeNull();
        vm.TreatmentImprovementVBand.Should().BeNull();
        int[] expectedDefaultPeriodOptions = [2, 4, 6];
        vm.TreatmentImprovementOptions.Should().BeEquivalentTo(expectedDefaultPeriodOptions);
        vm.SaveErrorMessage.Should().BeNull();
        vm.OverlapWarning.Should().BeNull();
        vm.IsSaved.Should().BeFalse();
    }

    /// <summary>
    /// (Ⅴ)区分bandの書き込みは、選択中の処遇改善対象の選択番号が何であっても、
    /// bandそのものが月の語彙にあれば行う。option番号が6かどうかをここで判定して
    /// bandの書き込みを条件付けると、seedの(Ⅴ)行が option 6 と band の両方を要求する
    /// 設計（対応関係はマスタ条件側が強制する）と重複判定になり、将来 option の
    /// 並びが変わった際に暗黙依存で壊れる。
    /// </summary>
    [Fact]
    public async Task SaveAsync_writes_the_band_key_regardless_of_the_selected_option_number()
    {
        var vm = CreateViewModel();
        await vm.InitializeAsync();
        vm.OfficeId = Guid.NewGuid();
        vm.TreatmentImprovementOption = 2; // (Ⅴ)を示す6ではない
        vm.TreatmentImprovementVBand = 3;

        await vm.SaveCommand.ExecuteAsync(null);

        SavedFlags.Should().ContainKey("mhlw.b46.capability.treatment-improvement-v-band.3")
            .WhoseValue.Should().BeTrue();
    }

    /// <summary>
    /// 選択番号の書き込みにも、bandと対称に月の語彙(TreatmentImprovementOptions)への
    /// 所属チェックを課す。チェックが無いと、その月の語彙に無い選択番号キーが宣言され、
    /// 存在検査（Task 5・ADR 0049）が毎月警告する。
    /// </summary>
    [Fact]
    public async Task SaveAsync_does_not_write_an_out_of_vocabulary_option_key()
    {
        var vm = CreateViewModel();
        await vm.InitializeAsync();
        vm.OfficeId = Guid.NewGuid();
        vm.TreatmentImprovementOption = 99; // フェイクマスタのどの月の語彙にも無い値

        await vm.SaveCommand.ExecuteAsync(null);

        SavedFlags.Keys.Should().NotContain(
            k => k.StartsWith("mhlw.b46.capability.treatment-improvement.", StringComparison.Ordinal));
    }

    /// <summary>
    /// 選択後にPeriodStartをoptionの無い世代へ変更すると、ReloadCapabilityOptionsが
    /// 語彙から外れた選択を自動的にnullへ戻す。これがSaveAsyncの書き込みガードの
    /// 実質的な防波堤になっているため、この再読込パス自体を検査する。
    /// </summary>
    [Fact]
    public async Task SaveAsync_does_not_write_the_option_key_after_the_period_changes_to_a_generation_lacking_it()
    {
        var vm = CreateViewModel();
        await vm.InitializeAsync();
        vm.OfficeId = Guid.NewGuid();
        vm.TreatmentImprovementOption = 6; // R6(既定期間)の語彙にはあるが、R8には無い

        vm.PeriodStart = new DateOnly(2026, 6, 1); // R8世代へ変更

        await vm.SaveCommand.ExecuteAsync(null);

        vm.TreatmentImprovementOption.Should().BeNull();
        SavedFlags.Keys.Should().NotContain(
            k => k.StartsWith("mhlw.b46.capability.treatment-improvement.", StringComparison.Ordinal));
    }

    /// <summary>
    /// PeriodStartはDiscard以外に、画面のテキスト入力（DateOnlyConverter経由のTwoWay
    /// バインド）からも任意の年を受け取り得る。ServiceMonthの許容年（1900〜2200）外の
    /// 値を直接設定しても、ReloadCapabilityOptionsが例外を外へ漏らさず選択肢を
    /// 空にするだけであることを固定する（Discardの既定値修正だけでは、この経路の
    /// 再発は防げない。手入力で古い/未来の年を打ち切るまでの1文字違いで到達する）。
    /// </summary>
    [Fact]
    public void ReloadCapabilityOptions_does_not_throw_for_a_period_start_year_outside_the_service_month_range()
    {
        var vm = CreateViewModel();
        vm.TreatmentImprovementOption = 6;

        var act = () => vm.PeriodStart = new DateOnly(1899, 1, 1);

        act.Should().NotThrow();
        vm.TreatmentImprovementOptions.Should().BeEmpty();
        vm.TreatmentImprovementOption.Should().BeNull();
    }

    /// <summary>
    /// I1: 当月のマスタ行が(Ⅴ)区分を併せて要求している選択番号を、区分の選択なしに保存すると、
    /// その選択番号の行は1件も一致せず加算が**無音で0円**になる（ADR 0048・0049 が塞ごうと
    /// している無音の過少請求そのものを、本ブランチの入力画面が再生産する）。
    /// 不完全な宣言は永続化せず、保存エラーとして差し戻す。
    /// </summary>
    [Fact]
    public async Task SaveAsync_rejects_an_option_that_requires_a_v_band_when_no_band_is_selected()
    {
        var vm = CreateViewModel();
        await vm.InitializeAsync();
        vm.OfficeId = Guid.NewGuid();
        vm.TreatmentImprovementOption = 6; // 合成マスタの(Ⅴ)行は band も要求する
        vm.TreatmentImprovementVBand = null;

        await vm.SaveCommand.ExecuteAsync(null);

        vm.SaveErrorMessage.Should().NotBeNullOrEmpty();
        vm.IsSaved.Should().BeFalse();
        _caps.Count.Should().Be(0, "不完全な宣言は1件も永続化しない");
    }

    /// <summary>
    /// I1: 正しい組合せ（対象区分＋(Ⅴ)区分）は従来どおり両方のキーを書いて保存できる。
    /// 差し戻しが常時発火して保存経路を殺していないことを固定する。
    /// </summary>
    [Fact]
    public async Task SaveAsync_accepts_an_option_that_requires_a_v_band_when_the_band_is_selected()
    {
        var vm = CreateViewModel();
        await vm.InitializeAsync();
        vm.OfficeId = Guid.NewGuid();
        vm.TreatmentImprovementOption = 6;
        vm.TreatmentImprovementVBand = 3;

        await vm.SaveCommand.ExecuteAsync(null);

        vm.SaveErrorMessage.Should().BeNull();
        vm.IsSaved.Should().BeTrue();
        SavedFlags.Should().ContainKey("mhlw.b46.capability.treatment-improvement.6")
            .WhoseValue.Should().BeTrue();
        SavedFlags.Should().ContainKey("mhlw.b46.capability.treatment-improvement-v-band.3")
            .WhoseValue.Should().BeTrue();
    }

    /// <summary>
    /// I1: bandを要求しない選択番号は、band 未選択でも従来どおり保存できる。
    /// 「どの選択番号がbandを要求するか」はマスタ行から導出しており、UI側の決め打ちではない。
    /// </summary>
    [Fact]
    public async Task SaveAsync_accepts_an_option_that_does_not_require_a_v_band_without_a_band()
    {
        var vm = CreateViewModel();
        await vm.InitializeAsync();
        vm.OfficeId = Guid.NewGuid();
        vm.TreatmentImprovementOption = 2;

        await vm.SaveCommand.ExecuteAsync(null);

        vm.SaveErrorMessage.Should().BeNull();
        vm.IsSaved.Should().BeTrue();
        SavedFlags.Should().ContainKey("mhlw.b46.capability.treatment-improvement.2");
    }

    /// <summary>
    /// I2: 既定の適用期間は現在月の初日。固定日を書くと、その日が属する世代の選択肢しか
    /// 出せなくなり（旧実装は 2026-04 固定で、2026-06 施行の選択番号 7・8 が一切選べなかった）、
    /// 現在月の体制届を登録する運用で無音の過少宣言になる。
    /// </summary>
    [Fact]
    public async Task The_default_period_start_follows_the_current_month_and_exposes_that_generation()
    {
        var clock = new FixedClock(new DateTimeOffset(2026, 7, 15, 12, 0, 0, TimeSpan.Zero));

        var vm = CreateViewModel(clock);
        await vm.InitializeAsync();

        vm.PeriodStart.Should().Be(new DateOnly(2026, 7, 1));
        vm.TreatmentImprovementOptions.Should().Contain([7, 8],
            "現在月がR8世代なら2026-06施行の選択番号が選べなければならない");
    }

    /// <summary>
    /// I2: Discard（Escape）も現在月へ戻す。固定日へ戻すと、破棄のたびに過去世代の
    /// 語彙へ落ちる。
    /// </summary>
    [Fact]
    public void DiscardCommand_returns_to_the_current_month_not_a_fixed_date()
    {
        var clock = new FixedClock(new DateTimeOffset(2026, 7, 15, 12, 0, 0, TimeSpan.Zero));
        var vm = CreateViewModel(clock);
        vm.PeriodStart = new DateOnly(2024, 6, 1);

        vm.DiscardCommand.Execute(null);

        vm.PeriodStart.Should().Be(new DateOnly(2026, 7, 1));
    }

    /// <summary>
    /// 対象区分が未選択（null）のときは `treatment-improvement.*` を1件も書かない。
    /// one-hotの下限側の不変条件であり、このViewModelは `src/` 内で当該キーを書く唯一の
    /// 場所（同じメソッドで、語彙外optionを無条件に書く実欠陥が過去に見つかっている）。
    /// </summary>
    [Fact]
    public async Task SaveAsync_writes_no_option_key_when_no_option_is_selected()
    {
        var vm = CreateViewModel();
        await vm.InitializeAsync();
        vm.OfficeId = Guid.NewGuid();
        vm.TreatmentImprovementOption = null;

        await vm.SaveCommand.ExecuteAsync(null);

        vm.IsSaved.Should().BeTrue();
        SavedFlags.Keys.Should().NotContain(
            k => k.StartsWith("mhlw.b46.capability.treatment-improvement.", StringComparison.Ordinal));
    }

    /// <summary>
    /// テスト専用のIClaimMasterProvider fake。R6世代（2026-05以前）は処遇改善加算の
    /// 選択番号(2,4,6)と(Ⅴ)区分band(3)を持ち、(Ⅴ)行は option 6 と band 3 の**両方**を
    /// 条件に要求する（実seedの二重ゲート。ADR 0048 決定4）。R8世代（2026-06以降）は
    /// (Ⅴ)を持たず、代わりに 2026-06 施行の選択番号(7,8)を持つ（実運用のADR 0046と同型）。
    /// </summary>
    private sealed class FakeCapabilityClaimMasterProvider : IClaimMasterProvider
    {
        private static readonly ServiceMonth R8Start = new(2026, 6);

        public ClaimMasterRelease ResolveVersion(ServiceMonth serviceMonth) =>
            throw new NotSupportedException();

        public ClaimCalculationMasterBundle ResolveCalculationMasters(ServiceMonth serviceMonth)
        {
            var conditions = new List<ClaimConditionDefinition>
            {
                CapabilityCondition("mhlw.b46.capability.treatment-improvement.2"),
                CapabilityCondition("mhlw.b46.capability.treatment-improvement.4"),
            };
            var serviceCodes = new List<ServiceCodeMasterRow>
            {
                ServiceCode("sc-ti-2", "mhlw.b46.capability.treatment-improvement.2"),
                ServiceCode("sc-ti-4", "mhlw.b46.capability.treatment-improvement.4"),
            };

            if (serviceMonth < R8Start)
            {
                conditions.Add(CapabilityCondition("mhlw.b46.capability.treatment-improvement.6"));
                conditions.Add(CapabilityCondition("mhlw.b46.capability.treatment-improvement-v-band.3"));
                serviceCodes.Add(ServiceCode(
                    "sc-ti-6-band-3",
                    "mhlw.b46.capability.treatment-improvement.6",
                    "mhlw.b46.capability.treatment-improvement-v-band.3"));
            }
            else
            {
                conditions.Add(CapabilityCondition("mhlw.b46.capability.treatment-improvement.7"));
                conditions.Add(CapabilityCondition("mhlw.b46.capability.treatment-improvement.8"));
                serviceCodes.Add(ServiceCode("sc-ti-7", "mhlw.b46.capability.treatment-improvement.7"));
                serviceCodes.Add(ServiceCode("sc-ti-8", "mhlw.b46.capability.treatment-improvement.8"));
            }

            return new ClaimCalculationMasterBundle(
                BasicRewards: [],
                UnitAdjustments: [],
                RegionUnitPrices: [],
                BurdenCaps: [],
                TransitionRules: [],
                ServiceCodes: serviceCodes,
                ConditionDefinitions: conditions);
        }

        public IReadOnlySet<string> AllOfficeCapabilityConditionValues() =>
            new HashSet<string>(StringComparer.Ordinal);

        private static ClaimConditionDefinition CapabilityCondition(string value) =>
            new("cond-" + value, new ServiceMonth(2024, 6), null, ClaimConditionKind.OfficeCapability,
                ClaimConditionOperator.Equals, new ClaimConditionTokenOperand(value), [SourceRef()]);

        /// <summary>条件の組合せだけが検証対象のため、単位規則・componentは最小の合成値でよい。</summary>
        private static ServiceCodeMasterRow ServiceCode(string key, params string[] capabilityValues) =>
            new(key,
                "460000",
                "合成加算行",
                "b-type",
                [],
                [.. capabilityValues.Select(value => "cond-" + value)],
                new BaseComponentPassThroughRule("base-x", "step-x", null, BillingUnit.PerDay),
                [],
                new ServiceMonth(2024, 6),
                null,
                [SourceRef()]);

        private static ClaimSourceRef SourceRef() => new(
            "doc-1",
            "0000000000000000000000000000000000000000000000000000000000000000",
            "loc",
            ClaimSourceEvidenceRole.Authoritative,
            [ClaimSourceSupport.Conditions]);
    }
}

internal sealed class InMemoryOfficeCapabilityRepo : IOfficeCapabilityRepository
{
    private readonly List<OfficeCapability> _list = [];
    public int Count => _list.Count;
    public OfficeCapability Last => _list[^1];
    public Task AddAsync(OfficeCapability c, CancellationToken ct) { _list.Add(c); return Task.CompletedTask; }
    public Task<IReadOnlyList<OfficeCapability>> ListByOfficeAsync(Guid officeId, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<OfficeCapability>>(_list.Where(c => c.OfficeId == officeId).ToArray());
    public Task<OfficeCapability?> FindEffectiveAsync(Guid officeId, DateOnly asOf, CancellationToken ct) =>
        Task.FromResult(_list.FirstOrDefault(c => c.OfficeId == officeId && c.Period.Contains(asOf)));
}
