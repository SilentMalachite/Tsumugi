using FluentAssertions;
using Tsumugi.Application.Abstractions;
using Tsumugi.Application.Claim;
using Tsumugi.Domain.Enums;
using Xunit;

namespace Tsumugi.Application.Tests.Claim;

/// <summary>
/// 日次記録の縮約は 1 か所（確定前の DB 由来と確定後の snapshot 由来で同じ関数）。
/// 規則が分かれると「確定できたのに、同じ請求の再評価では項目不足」になる。
/// </summary>
public sealed class ClaimDailyRecordReductionTests
{
    // NOTE(teeth): 初日が時刻未入力で後日に入力がある場合、「暦日昇順で最初に値が入力された日」を
    // 代表にする（先頭日の null を代表にすると、入力済みなのに不足扱いになる）。
    [Fact]
    public void The_first_entered_time_represents_the_month_even_when_the_first_day_is_blank()
    {
        var aggregate = ClaimDailyRecordReduction.Reduce(
        [
            Row(1) with { ServiceStartTime = null, ServiceEndTime = null },
            Row(2) with
            {
                ServiceStartTime = new TimeOnly(9, 30),
                ServiceEndTime = new TimeOnly(15, 45),
            },
        ]);

        aggregate.ServiceStartTime.Should().Be(new TimeOnly(9, 30));
        aggregate.ServiceEndTime.Should().Be(new TimeOnly(15, 45));
    }

    // NOTE(teeth): 一部の日だけ確認済みなら「最初の非 Unspecified」を代表にする（全日確認を要求すると、
    // 確定を通した請求が再評価で不足になる）。
    [Fact]
    public void A_partially_confirmed_month_reduces_to_confirmed()
    {
        var aggregate = ClaimDailyRecordReduction.Reduce(
        [
            Row(1) with { RecipientConfirmation = RecipientConfirmationStatus.Unspecified },
            Row(2) with { RecipientConfirmation = RecipientConfirmationStatus.Confirmed },
        ]);

        aggregate.RecipientConfirmation.Should().Be(RecipientConfirmationStatus.Confirmed);
    }

    [Fact]
    public void The_first_specified_classification_represents_the_month()
    {
        var aggregate = ClaimDailyRecordReduction.Reduce(
        [
            Row(1),
            Row(2) with
            {
                MedicalCoordinationType = MedicalCoordinationType.TypeI,
                TrialUseSupportType = TrialUseSupportType.TypeI,
            },
        ]);

        aggregate.MedicalCoordinationType.Should().Be(MedicalCoordinationType.TypeI);
        aggregate.TrialUseSupportType.Should().Be(TrialUseSupportType.TypeI);
    }

    // NOTE(teeth): 本体報酬を算定しない日は母集団に入れない（欠席時対応日の値を代表にしない）。
    [Fact]
    public void Days_without_the_base_reward_are_excluded()
    {
        var aggregate = ClaimDailyRecordReduction.Reduce(
        [
            Row(1) with
            {
                Attendance = Attendance.AbsenceSupport,
                ServiceStartTime = new TimeOnly(8, 0),
                SpecialVisitSupportMinutes = 60,
            },
            Row(2) with { ServiceStartTime = new TimeOnly(10, 0), SpecialVisitSupportMinutes = 30 },
        ]);

        aggregate.ServiceStartTime.Should().Be(new TimeOnly(10, 0));
        aggregate.SpecialVisitSupportMinutesTotal.Should().Be(30);
    }

    // NOTE(teeth): 算定時間数はどの日にも入力が無ければ null。0 を返すと「入力済みの 0」と区別できず
    // 要求条件が fail-open する。
    [Fact]
    public void Unentered_billed_hours_stay_null_while_an_entered_zero_is_kept()
    {
        ClaimDailyRecordReduction.Reduce([Row(1)])
            .SpecialVisitSupportBilledHoursTotal.Should().BeNull();
        ClaimDailyRecordReduction.Reduce([Row(1) with { SpecialVisitSupportBilledHours = 0 }])
            .SpecialVisitSupportBilledHoursTotal.Should().Be(0);
    }

    [Fact]
    public void A_month_without_any_reward_day_reduces_to_empty()
    {
        ClaimDailyRecordReduction.Reduce([Row(1) with { Attendance = Attendance.Absent }])
            .Should().Be(ClaimDailyRecordAggregate.Empty);
    }

    private static ClaimReadinessDailyRow Row(int day) => new(
        new DateOnly(2026, 7, day),
        Attendance.Present,
        ServiceStartTime: new TimeOnly(9, 0),
        ServiceEndTime: new TimeOnly(15, 0),
        SpecialVisitSupportMinutes: null,
        OffsiteSupportApplied: false,
        MedicalCoordinationType: MedicalCoordinationType.Unspecified,
        TrialUseSupportType: TrialUseSupportType.Unspecified,
        RegionalCollaborationApplied: false,
        IntensiveSupportApplied: false,
        EmergencyAdmissionApplied: false,
        RecipientConfirmation: RecipientConfirmationStatus.Unspecified,
        SpecialVisitSupportBilledHours: null);
}
