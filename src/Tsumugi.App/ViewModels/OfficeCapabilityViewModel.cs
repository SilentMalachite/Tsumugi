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
    QueryClaimBillingTokenOptionsUseCase tokenOptionsUseCase) : ViewModelBase
{
    // Discardは画面を「新規入力前の初期状態」へ戻す。DateOnlyの既定値(0001-01-01)は
    // ServiceMonthの許容年(1900〜2200)の外側であり、体制届の選択肢再読込に失敗する
    // 原因になるため、フィールド初期値と同じ有効な既定期間を単一箇所で共有する。
    private static readonly DateOnly DefaultPeriodStart = new(2026, 4, 1);

    [ObservableProperty] private OfficeDto? _selectedOffice;
    [ObservableProperty] private Guid _officeId;
    [ObservableProperty] private DateOnly _periodStart = DefaultPeriodStart;
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
            // 書き込み時にも同じ語彙チェックを課す。
            if (TreatmentImprovementOption is { } option && TreatmentImprovementOptions.Contains(option))
            {
                flags[$"mhlw.b46.capability.treatment-improvement.{option}"] = true;
            }

            // (Ⅴ)区分は、その月に選択肢が存在し、かつ選択されているときに書く。
            // 処遇改善対象の選択番号との突き合わせはここで行わない —— seedの(Ⅴ)行が
            // `capability-treatment-improvement-v`（option 6）と`-v-band-{n}`の両方を
            // 要求するため、対応関係はマスタ条件が強制する。UI側で「どの選択番号が(Ⅴ)か」を
            // 導出すると選択番号の並びに暗黙依存する（ADR 0048・0049）。
            if (TreatmentImprovementVBand is { } band
                && TreatmentImprovementVBandOptions.Contains(band))
            {
                flags[$"mhlw.b46.capability.treatment-improvement-v-band.{band}"] = true;
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
