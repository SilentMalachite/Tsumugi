using Tsumugi.Application.Abstractions;
using Tsumugi.Application.Dtos;
using Tsumugi.Application.Validation;
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.Enums;
using Tsumugi.Domain.Logic;
using Tsumugi.Domain.Logic.Wage;
using Tsumugi.Domain.ValueObjects;

namespace Tsumugi.Application.UseCases.Wage;

public sealed class CalculateWagesUseCase(
    IDailyRecordRepository dailyRepo,
    IWorkRecordRepository workRepo,
    IWageFundRepository fundRepo,
    IWageSettingsRepository settingsRepo,
    IContractRepository contractRepo,
    IRecipientRepository recipientRepo,
    IRecipientHourlyRateRepository hourlyRateRepo,
    IWageAdjustmentRepository adjustmentRepo,
    IReadOnlyList<IWageMethodStrategy> strategies)
{
    public async Task<WagePreviewDto> ExecuteAsync(
        Guid officeId, int year, int month, CancellationToken ct)
    {
        if (officeId == Guid.Empty)
            throw new ArgumentException("事業所IDが指定されていません。", nameof(officeId));
        DateValidator.EnsureYearMonth(year, month);

        var ym = new YearMonth(year, month);

        var settings = WageSettingsPolicy.Effective(
            await settingsRepo.ListByOfficeAsync(officeId, ct),
            ym.FirstDay());
        if (settings is null)
            throw new InvalidOperationException(
                $"事業所 {officeId} の {year}-{month:D2} 時点の工賃設定が見つかりません。");

        var fund = WageFundPolicy.Effective(
            await fundRepo.ListByOfficeAndMonthAsync(officeId, year, month, ct));

        // 単一事業所運用前提: 当月 1 日時点で有効な契約を持つ利用者を対象とする。
        // 複数事業所対応は ContractedProvider / Contract 整理時に再実装する（open-questions 既出）。
        var allRecipients = await recipientRepo.ListAsync(includeArchived: false, ct);
        var anchor = ym.FirstDay();
        var targetRecipients = new List<Guid>();
        foreach (var r in allRecipients)
        {
            var contract = await contractRepo.FindEffectiveAsync(r.Id, anchor, ct);
            if (contract is not null) targetRecipients.Add(r.Id);
        }

        var allDaily = new List<Tsumugi.Domain.Entities.DailyRecord>();
        var allWork = new List<Tsumugi.Domain.Entities.WorkRecord>();
        foreach (var rid in targetRecipients)
        {
            allDaily.AddRange(await dailyRepo.ListByRecipientAndMonthAsync(rid, year, month, ct));
            allWork.AddRange(await workRepo.ListByRecipientAndMonthAsync(rid, year, month, ct));
        }

        var baseInputs = WageBasisExtractor.Build(allDaily, allWork, ym);

        // KouchinModule: Hourly 方式の場合、利用者ごとの時給マスタから DailyBreakdown を組み立てる
        IReadOnlyList<WageInputs> inputs = baseInputs;
        if (settings.Method == WageMethod.Hourly && targetRecipients.Count > 0)
        {
            // N+1 回避: 事業所分を一括取得して利用者ごとに引き当てる
            var allRates = await hourlyRateRepo.ListByOfficeAsync(officeId, ct);
            var ratesByRecipient = allRates
                .GroupBy(r => r.RecipientId)
                .ToDictionary(g => g.Key, g => (IReadOnlyList<RecipientHourlyRate>)g.ToArray());

            var updatedInputs = new List<WageInputs>(baseInputs.Count);
            foreach (var baseInput in baseInputs)
            {
                if (!ratesByRecipient.TryGetValue(baseInput.RecipientId, out var rates))
                {
                    updatedInputs.Add(baseInput);
                    continue;
                }

                // 出席日かつ WorkRecord がある日を DailyHourlyBasis に変換
                // allWork から当該利用者の当月実効 WorkRecord を日付ごとに取得
                var recipientWork = allWork
                    .Where(w => w.RecipientId == baseInput.RecipientId)
                    .ToArray();

                // WorkRecordPolicy.EffectiveByDate で訂正・取消を反映
                var effectiveWork = WorkRecordPolicy.EffectiveByDate(recipientWork).Values;

                // 出席日のみ対象（WageBasisExtractor と同一ロジック）
                var recipientDaily = allDaily
                    .Where(d => d.RecipientId == baseInput.RecipientId)
                    .ToArray();
                var effectiveDaily = DailyRecordPolicy.EffectiveByDate(recipientDaily).Values;
                var presentDates = effectiveDaily
                    .Where(d => d.Attendance == Attendance.Present)
                    .Select(d => d.ServiceDate)
                    .ToHashSet();

                var breakdown = new List<DailyHourlyBasis>();
                // 就労時間 0（欠席時対応等で分数のみ 0 のケース）は賃金に寄与しないため、
                // 時給期間の欠落チェック対象から除外する（HourlyWageStrategy の混在バッチ判定と同じ基準）
                foreach (var work in effectiveWork.Where(w =>
                    presentDates.Contains(w.WorkDate) && (w.WorkedMinutes ?? 0) > 0))
                {
                    var hourlyYen = RecipientHourlyRatePolicy.EffectiveYen(rates, baseInput.RecipientId, work.WorkDate);
                    if (hourlyYen is null)
                        throw new InvalidOperationException(
                            $"利用者 {baseInput.RecipientId} の {work.WorkDate:yyyy-MM-dd} に実効時給が見つかりません。" +
                            "時給期間に欠落がないか確認してください。就労日を黙って計算から除外することはできません。");
                    breakdown.Add(new DailyHourlyBasis(work.WorkDate, work.WorkedMinutes ?? 0, hourlyYen.Value));
                }

                if (breakdown.Count == 0)
                {
                    updatedInputs.Add(baseInput);
                    continue;
                }

                updatedInputs.Add(baseInput with { DailyBreakdown = breakdown });
            }

            // 混在バッチの防御: 一部の利用者だけ時給マスタがある状態で計算すると、
            // 未設定者の時給分が黙って 0 円になるため、設定漏れとして明示的に失敗させる
            if (updatedInputs.Any(i => i.DailyBreakdown is not null))
            {
                var uncovered = updatedInputs
                    .Where(i => i.DailyBreakdown is null && i.TotalWorkedMinutes > 0)
                    .Select(i => i.RecipientId)
                    .ToArray();
                if (uncovered.Length > 0)
                    throw new InvalidOperationException(
                        $"時給マスタが未設定のまま就労実績がある利用者がいます（利用者ID: {string.Join(", ", uncovered)}）。" +
                        "全対象者の時給を設定してください。");
            }
            inputs = updatedInputs;
        }

        // Hourly: DailyBreakdown がある場合は fund 不要、ない場合は fund 必須
        var anyBreakdown = inputs.Any(i => i.DailyBreakdown is not null);
        if (!anyBreakdown && settings.Method is WageMethod.Hourly or WageMethod.Equal && fund is null)
            throw new InvalidOperationException(
                "Hourly / Equal 方式では当月の工賃原資（WageFund）が必須です。");

        var lines = WageCalculator.Calculate(strategies, settings.Method, inputs, fund, settings);

        // WageAdjustment の実効合計を各利用者の工賃に加算
        var adjustments = await adjustmentRepo.ListByOfficeMonthAsync(officeId, ym, ct);
        var lineItems = lines.Select(l =>
        {
            var adj = WageAdjustmentPolicy.SumEffective(adjustments, l.RecipientId, ym);
            if (adj == 0) return l;
            // 現在 WageAdjustment.AmountYen は非負のため adj < 0 は到達不可だが、
            // 将来の deduction 系列 (WageAdjustmentType 拡張) のための保守的分岐。
            return new WageLineItem(l.RecipientId, l.AmountYen + adj,
                adj switch
                {
                    > 0 => $"{l.BasisSummary} + 特別手当 {adj:N0} 円",
                    < 0 => $"{l.BasisSummary} + 控除 {adj:N0} 円",
                    _ => l.BasisSummary,
                });
        }).ToArray();

        return new WagePreviewDto(
            officeId, year, month, settings.Method,
            TotalFundYen: fund?.TotalYen ?? 0,
            TotalAllocatedYen: lineItems.Sum(l => l.AmountYen),
            Lines: lineItems.Select(l => new WagePreviewLineDto(l.RecipientId, l.AmountYen, l.BasisSummary)).ToArray());
    }
}
