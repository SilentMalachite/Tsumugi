using FluentAssertions;
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.Enums;
using Xunit;

namespace Tsumugi.Domain.Tests;

public sealed class DailyRecordTests
{
    private static readonly Guid Recipient = Guid.NewGuid();
    private static readonly DateOnly Day = new(2026, 6, 1);

    [Fact]
    public void NewRecord_has_no_originId()
    {
        var r = DailyRecord.NewRecord(Guid.NewGuid(), Recipient, Day,
            Attendance.Present, TransportKind.Round, mealProvided: true,
            note: null, createdBy: "u", createdAt: DateTimeOffset.UnixEpoch);
        r.Kind.Should().Be(RecordKind.New);
        r.OriginId.Should().BeNull();
    }

    [Fact]
    public void NewRecord_preserves_all_claim_inputs()
    {
        var r = DailyRecord.NewRecord(Guid.NewGuid(), Recipient, Day,
            Attendance.Present, TransportKind.Round, mealProvided: true,
            note: null, createdBy: "u", createdAt: DateTimeOffset.UnixEpoch,
            serviceStartTime: new TimeOnly(9, 0),
            serviceEndTime: new TimeOnly(15, 30),
            specialVisitSupportMinutes: 0,
            offsiteSupportApplied: false,
            medicalCoordinationType: MedicalCoordinationType.TypeVI,
            trialUseSupportType: TrialUseSupportType.TypeII,
            regionalCollaborationApplied: true,
            intensiveSupportApplied: false,
            emergencyAdmissionApplied: true,
            recipientConfirmation: RecipientConfirmationStatus.Confirmed);

        r.ServiceStartTime.Should().Be(new TimeOnly(9, 0));
        r.ServiceEndTime.Should().Be(new TimeOnly(15, 30));
        r.SpecialVisitSupportMinutes.Should().Be(0);
        r.OffsiteSupportApplied.Should().BeFalse();
        r.MedicalCoordinationType.Should().Be(MedicalCoordinationType.TypeVI);
        r.TrialUseSupportType.Should().Be(TrialUseSupportType.TypeII);
        r.RegionalCollaborationApplied.Should().BeTrue();
        r.IntensiveSupportApplied.Should().BeFalse();
        r.EmergencyAdmissionApplied.Should().BeTrue();
        r.RecipientConfirmation.Should().Be(RecipientConfirmationStatus.Confirmed);
    }

    [Fact]
    public void NewRecord_old_overload_leaves_claim_inputs_unspecified()
    {
        var r = DailyRecord.NewRecord(Guid.NewGuid(), Recipient, Day,
            Attendance.Present, TransportKind.None, mealProvided: false,
            note: null, createdBy: "u", createdAt: DateTimeOffset.UnixEpoch);

        r.ServiceStartTime.Should().BeNull();
        r.ServiceEndTime.Should().BeNull();
        r.SpecialVisitSupportMinutes.Should().BeNull();
        r.OffsiteSupportApplied.Should().BeNull();
        r.MedicalCoordinationType.Should().Be(MedicalCoordinationType.Unspecified);
        r.TrialUseSupportType.Should().Be(TrialUseSupportType.Unspecified);
        r.RegionalCollaborationApplied.Should().BeNull();
        r.IntensiveSupportApplied.Should().BeNull();
        r.EmergencyAdmissionApplied.Should().BeNull();
        r.RecipientConfirmation.Should().Be(RecipientConfirmationStatus.Unspecified);
    }

    [Fact]
    public void Correction_carries_originId()
    {
        var origin = Guid.NewGuid();
        var r = DailyRecord.Correction(Guid.NewGuid(), Recipient, Day, origin,
            Attendance.Absent, TransportKind.None, mealProvided: false,
            note: "病気のため", createdBy: "u", createdAt: DateTimeOffset.UnixEpoch);
        r.Kind.Should().Be(RecordKind.Correct);
        r.OriginId.Should().Be(origin);
    }

    [Fact]
    public void Correction_preserves_explicit_false_and_zero()
    {
        var r = DailyRecord.Correction(Guid.NewGuid(), Recipient, Day, Guid.NewGuid(),
            Attendance.Present, TransportKind.None, mealProvided: false,
            note: null, createdBy: "u", createdAt: DateTimeOffset.UnixEpoch,
            specialVisitSupportMinutes: 0,
            offsiteSupportApplied: false,
            regionalCollaborationApplied: false,
            intensiveSupportApplied: false,
            emergencyAdmissionApplied: false);

        r.SpecialVisitSupportMinutes.Should().Be(0);
        r.OffsiteSupportApplied.Should().BeFalse();
        r.RegionalCollaborationApplied.Should().BeFalse();
        r.IntensiveSupportApplied.Should().BeFalse();
        r.EmergencyAdmissionApplied.Should().BeFalse();
    }

    [Fact]
    public void NewRecord_rejects_reversed_service_times()
    {
        var act = () => DailyRecord.NewRecord(Guid.NewGuid(), Recipient, Day,
            Attendance.Present, TransportKind.None, mealProvided: false,
            note: null, createdBy: "u", createdAt: DateTimeOffset.UnixEpoch,
            serviceStartTime: new TimeOnly(16, 0),
            serviceEndTime: new TimeOnly(9, 0));

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Correction_rejects_negative_special_visit_support_minutes()
    {
        var act = () => DailyRecord.Correction(Guid.NewGuid(), Recipient, Day, Guid.NewGuid(),
            Attendance.Present, TransportKind.None, mealProvided: false,
            note: null, createdBy: "u", createdAt: DateTimeOffset.UnixEpoch,
            specialVisitSupportMinutes: -1);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void NewRecord_rejects_unknown_medical_coordination_type()
    {
        var act = () => DailyRecord.NewRecord(Guid.NewGuid(), Recipient, Day,
            Attendance.Present, TransportKind.None, mealProvided: false,
            note: null, createdBy: "u", createdAt: DateTimeOffset.UnixEpoch,
            medicalCoordinationType: (MedicalCoordinationType)999);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void NewRecord_rejects_unknown_trial_use_support_type()
    {
        var act = () => DailyRecord.NewRecord(Guid.NewGuid(), Recipient, Day,
            Attendance.Present, TransportKind.None, mealProvided: false,
            note: null, createdBy: "u", createdAt: DateTimeOffset.UnixEpoch,
            trialUseSupportType: (TrialUseSupportType)999);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void NewRecord_rejects_unknown_recipient_confirmation_status()
    {
        var act = () => DailyRecord.NewRecord(Guid.NewGuid(), Recipient, Day,
            Attendance.Present, TransportKind.None, mealProvided: false,
            note: null, createdBy: "u", createdAt: DateTimeOffset.UnixEpoch,
            recipientConfirmation: (RecipientConfirmationStatus)999);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Cancellation_carries_originId()
    {
        var origin = Guid.NewGuid();
        var r = DailyRecord.Cancellation(Guid.NewGuid(), Recipient, Day, origin,
            createdBy: "u", createdAt: DateTimeOffset.UnixEpoch);
        r.Kind.Should().Be(RecordKind.Cancel);
        r.OriginId.Should().Be(origin);
    }

    [Fact]
    public void Cancellation_has_no_claim_inputs()
    {
        var r = DailyRecord.Cancellation(Guid.NewGuid(), Recipient, Day, Guid.NewGuid(),
            createdBy: "u", createdAt: DateTimeOffset.UnixEpoch);

        r.ServiceStartTime.Should().BeNull();
        r.ServiceEndTime.Should().BeNull();
        r.SpecialVisitSupportMinutes.Should().BeNull();
        r.OffsiteSupportApplied.Should().BeNull();
        r.MedicalCoordinationType.Should().Be(MedicalCoordinationType.Unspecified);
        r.TrialUseSupportType.Should().Be(TrialUseSupportType.Unspecified);
        r.RegionalCollaborationApplied.Should().BeNull();
        r.IntensiveSupportApplied.Should().BeNull();
        r.EmergencyAdmissionApplied.Should().BeNull();
        r.RecipientConfirmation.Should().Be(RecipientConfirmationStatus.Unspecified);
    }
}
