using FluentAssertions;
using Tsumugi.App.Startup;
using Xunit;

namespace Tsumugi.App.Tests;

public sealed class FirstRunWizardLifecycleTests
{
    [Fact]
    public void NotifyRegistered_requests_the_window_switch_once_even_when_called_again()
    {
        // RequestCancellation にはあった再入ガードが NotifyRegistered には無く、
        // Window 差し替えが二重に走りうる状態だった。
        var switches = 0;
        var sut = new FirstRunWizardLifecycle(
            onRegistrationCompleted: () => switches++,
            onCancellationRequested: () => { });

        sut.NotifyRegistered();
        sut.NotifyRegistered();

        switches.Should().Be(1);
    }

    [Fact]
    public void NotifyRegistered_marks_completion_before_invoking_the_switch()
    {
        // 差し替え callback の中で Window が Close される。その時点で
        // ShouldAllowClose が true でないと Closing がキャンセルされ、閉じられない。
        var allowedDuringSwitch = false;
        FirstRunWizardLifecycle? sut = null;
        sut = new FirstRunWizardLifecycle(
            onRegistrationCompleted: () => allowedDuringSwitch = sut!.ShouldAllowClose,
            onCancellationRequested: () => { });

        sut.NotifyRegistered();

        allowedDuringSwitch.Should().BeTrue();
    }

    [Fact]
    public void RequestCancellation_requests_shutdown_once_even_when_called_again()
    {
        var shutdowns = 0;
        var sut = new FirstRunWizardLifecycle(
            onRegistrationCompleted: () => { },
            onCancellationRequested: () => shutdowns++);

        sut.RequestCancellation();
        sut.RequestCancellation();

        shutdowns.Should().Be(1);
    }

    [Fact]
    public void Close_is_not_allowed_before_registration_completes()
    {
        var sut = new FirstRunWizardLifecycle(() => { }, () => { });

        sut.ShouldAllowClose.Should().BeFalse();
    }

    [Fact]
    public void Close_is_allowed_after_registration_completes()
    {
        var sut = new FirstRunWizardLifecycle(() => { }, () => { });

        sut.NotifyRegistered();

        sut.ShouldAllowClose.Should().BeTrue();
    }

    [Fact]
    public void A_failed_window_switch_does_not_leave_cancellation_blocked()
    {
        // 差し替えが失敗してウィザードが残ったとき、キャンセルが唯一の脱出経路。
        var shutdowns = 0;
        var sut = new FirstRunWizardLifecycle(
            onRegistrationCompleted: () => throw new InvalidOperationException("switch failed"),
            onCancellationRequested: () => shutdowns++);

        var act = () => sut.NotifyRegistered();
        act.Should().Throw<InvalidOperationException>();

        sut.RequestCancellation();
        shutdowns.Should().Be(1);
    }
}
