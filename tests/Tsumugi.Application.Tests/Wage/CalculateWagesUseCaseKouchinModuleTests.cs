// tests/Tsumugi.Application.Tests/Wage/CalculateWagesUseCaseKouchinModuleTests.cs
// KouchinModule 突合ケースの Application 統合テスト（AC: Task 7 Step 1）
using FluentAssertions;
using Tsumugi.Application.UseCases.Wage;
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.Enums;
using Tsumugi.Domain.Logic.Wage;
using Tsumugi.Domain.ValueObjects;
using Xunit;

namespace Tsumugi.Application.Tests.Wage;

public sealed class CalculateWagesUseCaseKouchinModuleTests
{
    // --- 固定値 ---
    private static readonly DateTimeOffset T0 = new(2026, 4, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly Guid OfficeId = Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly Guid RecipientId = Guid.Parse("20000000-0000-0000-0000-000000000002");
    private static readonly YearMonth Ym = new(2026, 5);

    // 対象月: 2026-05-01 〜 2026-05-31 のうち 15 日間を出勤日とする
    private static readonly DateOnly[] AttendanceDates = Enumerable
        .Range(1, 15)
        .Select(d => new DateOnly(2026, 5, d))
        .ToArray();

    private static readonly IReadOnlyList<IWageMethodStrategy> AllStrategies = new IWageMethodStrategy[]
    {
        new PieceWageStrategy(), new HourlyWageStrategy(),
        new FixedWageStrategy(), new EqualWageStrategy(),
    };

    // --- シード構築ヘルパー ---

    /// <summary>
    /// WageSettings: Hourly / HalfUp / LargestRemainder / 作業手当 500 / 職能 55h→2000, 70h→4000 / 単位 15 分
    /// </summary>
    private static WageSettings BuildSettings() => WageSettings.Create(
        Guid.NewGuid(), OfficeId,
        new DateRange(new DateOnly(2026, 4, 1), null),
        WageMethod.Hourly, RoundingRule.HalfUp, RemainderPolicy.LargestRemainder,
        fiscalYearStartMonth: 4, fixedDailyYen: null,
        workAllowancePerDayYen: 500,
        skillAllowanceTiers: new[] { new SkillAllowanceTier(55, 2000), new SkillAllowanceTier(70, 4000) },
        hourUnitMinutes: 15,
        createdBy: "tester", createdAt: T0);

    /// <summary>利用者: 2026-04-01〜 有効な契約あり</summary>
    private static (Recipient recipient, Contract contract) BuildRecipient()
    {
        var r = Recipient.Create(RecipientId, "テスト太郎", "テストタロウ",
            new DateOnly(1990, 1, 1), "tester", T0, Guid.NewGuid());
        var c = Contract.Create(Guid.NewGuid(), RecipientId,
            new DateRange(new DateOnly(2026, 4, 1), null),
            contractedSupplyDays: 22, createdBy: "tester", createdAt: T0, concurrencyToken: Guid.NewGuid());
        return (r, c);
    }

    /// <summary>
    /// RecipientHourlyRate: 350 円/h (2026-04-01 〜 2027-03-31)
    /// </summary>
    private static RecipientHourlyRate BuildHourlyRate() => RecipientHourlyRate.NewRecord(
        Guid.NewGuid(), OfficeId, RecipientId,
        new DateRange(new DateOnly(2026, 4, 1), new DateOnly(2027, 3, 31)),
        hourlyYen: 350,
        createdBy: "tester", createdAt: T0);

    /// <summary>15 日 × 出勤 + 各日 105 分の WorkRecord を生成</summary>
    private static (DailyRecord[] dailyRecords, WorkRecord[] workRecords) BuildRecords()
    {
        var daily = AttendanceDates
            .Select(d => DailyRecord.NewRecord(Guid.NewGuid(), RecipientId, d,
                Attendance.Present, TransportKind.None, false, null, "tester", T0))
            .ToArray();
        var work = AttendanceDates
            .Select(d => WorkRecord.NewRecord(Guid.NewGuid(), RecipientId, d,
                workedMinutes: 105, pieceCount: null, pieceUnitYen: null, points: null,
                note: null, createdBy: "tester", createdAt: T0))
            .ToArray();
        return (daily, work);
    }

    private static CalculateWagesUseCase BuildSut(
        WageAdjustment[] adjustments)
    {
        var settings = BuildSettings();
        var (recipient, contract) = BuildRecipient();
        var hourlyRate = BuildHourlyRate();
        var (daily, work) = BuildRecords();

        return new CalculateWagesUseCase(
            new FakeDailyRecordRepoSeeded(daily),
            new FakeWorkRecordRepoSeeded(work),
            new FakeWageFundRepoSeeded(Array.Empty<WageFund>()),
            new FakeWageSettingsRepoSeeded(new[] { settings }),
            new FakeContractRepoSeeded(new[] { contract }),
            new FakeRecipientRepoSeeded(new[] { recipient }),
            new FakeRecipientHourlyRateRepoSeeded(new[] { hourlyRate }),
            new FakeWageAdjustmentRepoSeeded(adjustments),
            AllStrategies);
    }

    // ============================================================
    // AC: ベースライン（特別手��なし）
    // 計算根拠:
    //   時給分 = ROUND(1575/60 × 350, HalfUp) = ROUND(9187.5) = 9188 円
    //   作業手当 = 15 × 500 = 7500 円
    //   職能手当 = 26h < 55h → 0 円
    //   合計 = 9188 + 7500 + 0 = 16688 円
    // ============================================================
    [Fact]
    public async Task Calculate_matches_kouchin_baseline_case()
    {
        var sut = BuildSut(Array.Empty<WageAdjustment>());

        var preview = await sut.ExecuteAsync(OfficeId, year: 2026, month: 5, ct: default);

        var line = preview.Lines.Should().ContainSingle().Subject;
        line.RecipientId.Should().Be(RecipientId);
        line.AmountYen.Should().Be(16_688,
            "時給 9188 + 作業手当 7500 + 職能手当 0 = 16688 円");
        line.BasisSummary.Should().Contain("9,188", "時給パートを含む");
        line.BasisSummary.Should().Contain("7,500", "作業手当を含む");
    }

    // ============================================================
    // AC: 特別手当 1,000 円加算
    //   合計 = 16688 + 1000 = 17688 円
    // ============================================================
    [Fact]
    public async Task Calculate_adds_special_allowance_via_wage_adjustment()
    {
        var specialAllowance = WageAdjustment.NewRecord(
            Guid.NewGuid(), OfficeId, RecipientId, Ym,
            WageAdjustmentType.SpecialAllowance, amountYen: 1_000, note: "特別手当",
            createdBy: "tester", createdAt: T0);

        var sut = BuildSut(new[] { specialAllowance });

        var preview = await sut.ExecuteAsync(OfficeId, year: 2026, month: 5, ct: default);

        var line = preview.Lines.Should().ContainSingle().Subject;
        line.AmountYen.Should().Be(17_688,
            "ベースライン 16688 + 特別手当 1000 = 17688 円");
        line.BasisSummary.Should().Contain("1,000", "特別手当の内訳が BasisSummary に含まれる");
    }
}
