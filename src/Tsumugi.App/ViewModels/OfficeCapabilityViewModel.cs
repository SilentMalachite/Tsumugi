using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Tsumugi.Application.Dtos;
using Tsumugi.Application.UseCases.Claim;
using Tsumugi.Application.UseCases.Office;
using Tsumugi.Application.UseCases.OfficeCapability;
using Tsumugi.Domain.ValueObjects;

namespace Tsumugi.App.ViewModels;

/// <summary>
/// 事業所体制（食事提供 / 送迎支援 / 福祉・介護職員等処遇改善加算 等）の登録画面 ViewModel。
/// 食事提供体制・送迎支援体制のフラグキーは docs/open-questions.md 通り Phase 1 では暫定
/// （mealProvision / transportSupport）。処遇改善加算区分・(Ⅴ)区分は ADR 0021 の公式
/// one-hotキー（mhlw.b46.capability.treatment-improvement.{n} / -v-band.{n}）で書く。
/// </summary>
public sealed partial class OfficeCapabilityViewModel(
    RegisterOfficeCapabilityUseCase registerUseCase,
    ListOfficesUseCase listOfficesUseCase,
    QueryClaimBillingTokenOptionsUseCase tokenOptionsUseCase,
    TimeProvider clock) : ViewModelBase
{
    private const string VBandRequiredMessage =
        "選択した対象区分は(Ⅴ)区分の選択を併せて必要とします。(Ⅴ)区分を選択してください。";

    /// <summary>
    /// Discardは画面を「新規入力前の初期状態」へ戻す。DateOnlyの既定値(0001-01-01)は
    /// ServiceMonthの許容年(1900〜2200)の外側であり、体制届の選択肢再読込に失敗する
    /// 原因になるため、フィールド初期値と同じ有効な既定期間を単一箇所で共有する。
    /// 固定日を書くと、その日が属する世代の選択肢しか出せなくなり（旧実装の 2026-04 固定では
    /// 2026-06 施行の選択番号が一切選べない）、現在月の体制届を登録する運用で無音の
    /// 過少宣言になる。兄弟の <c>ClaimPreparationViewModel</c> と同じく現在日から導く。
    /// </summary>
    private DateOnly DefaultPeriodStart => FirstDayOfMonth(clock);

    private static DateOnly FirstDayOfMonth(TimeProvider timeProvider)
    {
        var today = DateOnly.FromDateTime(timeProvider.GetLocalNow().DateTime);
        return new DateOnly(today.Year, today.Month, 1);
    }

    /// <summary>
    /// 当月のマスタが「(Ⅴ)区分を併せて要求する」と宣言している処遇改善の選択番号。
    /// 語彙はマスタから来る（ADR 0048・0049。UI側に選択番号を書かない）。
    /// </summary>
    private readonly HashSet<int> _optionsRequiringVBand = [];

    [ObservableProperty] private OfficeDto? _selectedOffice;
    [ObservableProperty] private Guid _officeId;
    [ObservableProperty] private DateOnly _periodStart = FirstDayOfMonth(clock);
    [ObservableProperty] private DateOnly? _periodEnd;
    [ObservableProperty] private bool _mealProvision;
    [ObservableProperty] private bool _transportSupport;
    [ObservableProperty] private int? _treatmentImprovementOption;
    [ObservableProperty] private int? _treatmentImprovementVBand;
    [ObservableProperty] private string? _saveErrorMessage;
    [ObservableProperty] private string? _overlapWarning;
    [ObservableProperty] private bool _isSaved;

    public ObservableCollection<OfficeDto> Offices { get; } = new();
    public ObservableCollection<int> TreatmentImprovementOptions { get; } = new();
    public ObservableCollection<int> TreatmentImprovementVBandOptions { get; } = new();

    partial void OnSelectedOfficeChanged(OfficeDto? value)
        => OfficeId = value?.Id ?? Guid.Empty;

    /// <summary>View の Loaded から呼ばれる初期化フック。事業所一覧と体制届の選択肢を読み込む。</summary>
    public async Task InitializeAsync(CancellationToken ct = default)
    {
        await LoadOfficesAsync(ct);
        ReloadCapabilityOptions();
    }

    public async Task LoadOfficesAsync(CancellationToken ct = default)
    {
        var list = await listOfficesUseCase.ExecuteAsync(ct);
        Offices.Clear();
        foreach (var o in list) Offices.Add(o);
    }

    /// <summary>
    /// 体制届の選択肢は適用期間の開始月に有効なマスタから引く。世代（R6/R8）で
    /// 選択番号の集合が変わるため、UIへ語彙を持たせない（ADR 0021・0048）。
    /// </summary>
    public void ReloadCapabilityOptions()
    {
        ServiceMonth month;
        try
        {
            month = new ServiceMonth(PeriodStart.Year, PeriodStart.Month);
        }
        catch (ArgumentOutOfRangeException)
        {
            // PeriodStartはDiscard（既定値へ戻す。今はDefaultPeriodStartで有効な年に固定済み）
            // 以外に、画面のテキスト入力（DateOnlyConverter経由のTwoWayバインド）からも
            // ServiceMonthの許容年（1900〜2200）外の値を直接受け取り得る。例外を外へ
            // 漏らさず、選択肢を空にして再選択を促す。
            TreatmentImprovementOptions.Clear();
            TreatmentImprovementVBandOptions.Clear();
            _optionsRequiringVBand.Clear();
            TreatmentImprovementOption = null;
            TreatmentImprovementVBand = null;
            return;
        }

        var options = tokenOptionsUseCase.Execute(month);

        // Clear()に続くAdd()はItemsSourceへReset通知を出す。画面に束縛された
        // SelectingItemsControlはResetでSelectedItemをnullへ戻し、TwoWayバインドにより
        // それがこのVMへ書き戻る（再構築の最中に選択値を失う）。再構築前の値を退避し、
        // 再構築後に明示的に代入し直すことで、新しい語彙にまだ含まれる選択を実際に
        // 保持させる（ヘッドレスなVMテストではこの書き戻りが起きないため、この経路の
        // 効果はVMテストだけでは証明できず、画面での手動確認に委ねる）。
        var previousOption = TreatmentImprovementOption;
        var previousBand = TreatmentImprovementVBand;

        TreatmentImprovementOptions.Clear();
        foreach (var code in options.TreatmentImprovementOptions) TreatmentImprovementOptions.Add(code);

        TreatmentImprovementVBandOptions.Clear();
        foreach (var code in options.TreatmentImprovementVBandOptions) TreatmentImprovementVBandOptions.Add(code);

        _optionsRequiringVBand.Clear();
        foreach (var code in options.TreatmentImprovementOptionsRequiringVBand) _optionsRequiringVBand.Add(code);

        TreatmentImprovementOption =
            previousOption is { } selected && TreatmentImprovementOptions.Contains(selected) ? selected : null;
        TreatmentImprovementVBand =
            previousBand is { } band && TreatmentImprovementVBandOptions.Contains(band) ? band : null;
    }

    partial void OnPeriodStartChanged(DateOnly value) => ReloadCapabilityOptions();

    [RelayCommand]
    private async Task SaveAsync()
    {
        try
        {
            var flags = new Dictionary<string, bool>
            {
                ["mealProvision"] = MealProvision,
                ["transportSupport"] = TransportSupport,
            };

            // ADR 0021: 公式体制届キーはone-hot。選択された選択番号のキーだけをtrueで置く。
            // 未選択なら1件も置かない（「なし」＝option 1 は加算行を持たないため宣言しない）。
            // bandと対称に、月の語彙(TreatmentImprovementOptions)に無い値は書かない
            // ——通常はReloadCapabilityOptionsの失効選択リセットが先に選択をnullへ戻すが、
            // それを経由しない値の混入（プログラム的な直接代入等）に対する防波堤として、
            // 書き込み時にも同じ語彙チェックを課す。この語彙チェック済みの選択番号
            // （無ければnull）は、option書き込み・band書き込みガードの両方が参照するため
            // 一度だけ確定させる。
            var validOption = TreatmentImprovementOption is { } candidate
                && TreatmentImprovementOptions.Contains(candidate)
                ? candidate
                : (int?)null;

            if (validOption is { } option)
            {
                // 当月のマスタ行が(Ⅴ)区分を併せて要求している選択番号を、区分の選択なしに
                // 宣言すると、その選択番号の行は1件も一致せず加算が**無音で0円**になる
                // （ADR 0048・0049 が塞ごうとしている無音の過少請求そのもの）。
                // 不完全な宣言を永続化する前に、保存エラーとして差し戻す。
                if (_optionsRequiringVBand.Contains(option) && !HasSelectableVBand())
                {
                    SaveErrorMessage = VBandRequiredMessage;
                    IsSaved = false;
                    return;
                }

                flags[$"mhlw.b46.capability.treatment-improvement.{option}"] = true;
            }

            // (Ⅴ)区分は、選択中の選択番号が当月のマスタ行でband併宣言を要求している場合に
            // 限って書く（上のガードの逆向き）。band を要求しない選択番号のまま band だけを
            // 選択・保存すると、宣言集合ではどの(Ⅴ)行にも一致しないorphanなキーになり、
            // 体制届の充足可能性検査（`OfficeCapabilityCoveragePolicy.FindUnsatisfiableDeclaredKeys`）
            // が毎月警告する（レビュー指摘: (Ⅴ)から他区分へ切り替えた後もband選択がそのまま
            // 残る画面挙動のため、実運用で到達しうる）。band 併選択を要求しない選択番号
            // （またはoption未選択）では、band を選んでいても書かない。どの選択番号が band を
            // 要求するかは常にマスタ行から導出しており、UI側に「どの選択番号が(Ⅴ)か」という
            // 語彙は持たせない（ADR 0048・0049）。
            if (validOption is { } bandGatingOption
                && _optionsRequiringVBand.Contains(bandGatingOption)
                && HasSelectableVBand())
            {
                flags[$"mhlw.b46.capability.treatment-improvement-v-band.{TreatmentImprovementVBand}"] = true;
            }

            var (_, warnings) = await registerUseCase.ExecuteAsync(
                OfficeId, new DateRange(PeriodStart, PeriodEnd), flags,
                actor: Environment.UserName, default);
            SaveErrorMessage = null;
            OverlapWarning = warnings.Count > 0 ? string.Join(" ", warnings) : null;
            IsSaved = true;
        }
        catch (ArgumentException ex)
        {
            SaveErrorMessage = ex.Message;
            IsSaved = false;
        }
    }

    /// <summary>(Ⅴ)区分が選択済みかつ当月の語彙に含まれる（＝書き込んで意味がある）か。</summary>
    private bool HasSelectableVBand() =>
        TreatmentImprovementVBand is { } band && TreatmentImprovementVBandOptions.Contains(band);

    [RelayCommand]
    private void Discard()
    {
        SelectedOffice = null;
        PeriodStart = DefaultPeriodStart;
        PeriodEnd = null;
        MealProvision = false;
        TransportSupport = false;
        TreatmentImprovementOption = null;
        TreatmentImprovementVBand = null;
        SaveErrorMessage = null;
        OverlapWarning = null;
        IsSaved = false;
    }
}
