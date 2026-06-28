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
        if (settings.Method is WageMethod.Hourly or WageMethod.Equal && fund is null)
            throw new InvalidOperationException(
                "Hourly / Equal 方式では当月の工賃原資（WageFund）が必須です。");

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

        var inputs = WageBasisExtractor.Build(allDaily, allWork, ym);
        var lines = WageCalculator.Calculate(strategies, settings.Method, inputs, fund, settings);

        return new WagePreviewDto(
            officeId, year, month, settings.Method,
            TotalFundYen: fund?.TotalYen ?? 0,
            TotalAllocatedYen: lines.Sum(l => l.AmountYen),
            Lines: lines.Select(l => new WagePreviewLineDto(l.RecipientId, l.AmountYen, l.BasisSummary)).ToArray());
    }
}
