using Tsumugi.Application.Abstractions;
using Tsumugi.Application.Dtos;
using Tsumugi.Application.Validation;
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
            var updatedInputs = new List<WageInputs>(baseInputs.Count);
            foreach (var baseInput in baseInputs)
            {
                var rates = await hourlyRateRepo.ListByOfficeRecipientAsync(officeId, baseInput.RecipientId, ct);
                if (rates.Count == 0)
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
                foreach (var work in effectiveWork.Where(w => presentDates.Contains(w.WorkDate)))
                {
                    var hourlyYen = RecipientHourlyRatePolicy.EffectiveYen(rates, baseInput.RecipientId, work.WorkDate);
                    if (hourlyYen is null) continue;
                    breakdown.Add(new DailyHourlyBasis(work.WorkDate, work.WorkedMinutes ?? 0, hourlyYen.Value));
                }

                if (breakdown.Count == 0)
                {
                    updatedInputs.Add(baseInput);
                    continue;
                }

                updatedInputs.Add(baseInput with { DailyBreakdown = breakdown });
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
