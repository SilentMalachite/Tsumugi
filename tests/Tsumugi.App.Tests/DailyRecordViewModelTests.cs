using FluentAssertions;
using Tsumugi.App.ViewModels;
using Tsumugi.Application.Abstractions;
using Tsumugi.Application.Dtos;
using Tsumugi.Application.UseCases;
using Tsumugi.Application.UseCases.Claim;
using Tsumugi.Application.UseCases.DailyRecord;
using Tsumugi.Application.UseCases.Office;
using Tsumugi.Application.UseCases.Recipient;
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.Enums;
using Xunit;

namespace Tsumugi.App.Tests;

public sealed class DailyRecordViewModelTests
{
    private readonly FakeDailyRecordRepo _repo = new();
    private readonly InMemoryRecipientRepoForDaily _recipients = new();
    private readonly InMemoryOfficeRepo _offices = new();
    private readonly FakeIntensiveSupportEpisodeRepo _episodes = new();
    private readonly InMemoryUow _uow = new();
    private readonly FixedClock _clock = new(DateTimeOffset.UnixEpoch);

    private DailyRecordViewModel NewVm() => new(
        new RecordDailyRecordUseCase(_repo, _uow, _clock),
        new CorrectDailyRecordUseCase(_repo, _uow, _clock),
        new CancelDailyRecordUseCase(_repo, _uow, _clock),
        new QueryMonthDailyRecordsUseCase(_repo),
        new ListRecipientsUseCase(_recipients),
        new ListOfficesUseCase(_offices),
        new QueryIntensiveSupportEpisodeUseCase(_episodes),
        new SetIntensiveSupportEpisodeUseCase(_episodes, _uow, _clock));

    [Fact]
    public async Task LoadAsync_with_no_recipient_or_month_does_not_throw_and_keeps_cells_empty()
    {
        // F5 押下時に Year=Month=0 / RecipientId=Empty で DateTime.DaysInMonth(0,0) に落ちないこと。
        var vm = NewVm();
        await vm.LoadAsync();
        vm.Cells.Should().BeEmpty();
    }

    [Fact]
    public void LoadCommand_is_disabled_when_recipient_or_month_unset()
    {
        var vm = NewVm();
        vm.LoadCommand.CanExecute(null).Should().BeFalse();

        vm.SetRecipient(Guid.NewGuid());
        vm.LoadCommand.CanExecute(null).Should().BeFalse();  // 年月未指定

        vm.SetMonth(2026, 6);
        vm.LoadCommand.CanExecute(null).Should().BeTrue();
    }

    [Fact]
    public async Task InitializeAsync_populates_recipients_for_view_lifecycle()
    {
        var r = Recipient.Create(Guid.NewGuid(), "氏名", "シメイ",
            new DateOnly(1990, 1, 1), "u", DateTimeOffset.UnixEpoch, Guid.NewGuid());
        _recipients.Add(r);

        var vm = NewVm();
        await vm.InitializeAsync();

        vm.Recipients.Should().ContainSingle(x => x.Id == r.Id);
    }

    [Fact]
    public async Task LoadRecipientsAsync_populates_recipients_for_selection()
    {
        var r = Recipient.Create(Guid.NewGuid(), "氏名", "シメイ",
            new DateOnly(1990, 1, 1), "u", DateTimeOffset.UnixEpoch, Guid.NewGuid());
        _recipients.Add(r);

        var vm = NewVm();
        await vm.LoadRecipientsAsync();

        vm.Recipients.Should().ContainSingle(x => x.Id == r.Id);
    }

    [Fact]
    public void SelectedRecipient_synchronises_RecipientId_and_enables_load()
    {
        var vm = NewVm();
        vm.SetMonth(2026, 6);
        var dto = TestRecipients.Make(Guid.NewGuid(), "氏名", "シメイ");

        vm.SelectedRecipient = dto;

        vm.RecipientId.Should().Be(dto.Id);
        vm.LoadCommand.CanExecute(null).Should().BeTrue();
    }

    [Fact]
    public async Task LoadAsync_creates_cell_per_day_of_month()
    {
        var vm = NewVm();
        vm.SetRecipient(Guid.NewGuid());
        vm.SetMonth(2026, 6);
        await vm.LoadAsync();
        vm.Cells.Should().HaveCount(30);
    }

    [Fact]
    public async Task Record_then_query_shows_effective_attendance()
    {
        var vm = NewVm();
        var rid = Guid.NewGuid();
        vm.SetRecipient(rid);
        vm.SetMonth(2026, 6);
        await vm.LoadAsync();

        var cell = vm.Cells[0];  // 6/1
        await cell.RecordCommand.ExecuteAsync(Attendance.Present);
        await vm.LoadAsync();
        vm.Cells[0].EffectiveAttendance.Should().Be(Attendance.Present);
    }

    [Fact]
    public async Task SetAttendance_routes_to_record_when_no_effective()
    {
        var vm = NewVm();
        vm.SetRecipient(Guid.NewGuid());
        vm.SetMonth(2026, 6);
        await vm.LoadAsync();

        var cell = vm.Cells[0];
        cell.EffectiveId.Should().BeNull();

        await cell.SetAttendanceCommand.ExecuteAsync(Attendance.Present);

        _repo.Added.Should().HaveCount(1);
        _repo.Added[0].Kind.Should().Be(RecordKind.New);
    }

    [Fact]
    public async Task SetAttendance_routes_to_correct_when_effective_exists()
    {
        // R2-H2: 既存記録の出欠変更時、UI から訂正経路（CorrectCommand）に届かないと
        // RecordDailyRecordUseCase の同一日 New 重複拒否で例外になる。
        var vm = NewVm();
        vm.SetRecipient(Guid.NewGuid());
        vm.SetMonth(2026, 6);
        await vm.LoadAsync();

        await vm.Cells[0].SetAttendanceCommand.ExecuteAsync(Attendance.Present);
        await vm.LoadAsync();  // EffectiveId を反映

        await vm.Cells[0].SetAttendanceCommand.ExecuteAsync(Attendance.Absent);

        _repo.Added.Should().HaveCount(2);
        _repo.Added[1].Kind.Should().Be(RecordKind.Correct);
        _repo.Added[1].Attendance.Should().Be(Attendance.Absent);
        _repo.Added[1].OriginId.Should().Be(_repo.Added[0].Id);
    }

    [Fact]
    public async Task Load_selected_editor_and_correct_round_trip_all_claim_values_including_false_zero_unspecified()
    {
        var rid = Guid.NewGuid();
        _repo.Added.Add(DailyRecord.NewRecord(
            Guid.NewGuid(), rid, new DateOnly(2026, 6, 1),
            Attendance.Present, TransportKind.Round, true, "note", "u", DateTimeOffset.UnixEpoch,
            new TimeOnly(9, 0), new TimeOnly(15, 30), 0, false,
            MedicalCoordinationType.Unspecified, TrialUseSupportType.Unspecified,
            false, null, false, RecipientConfirmationStatus.Unspecified));
        var vm = NewVm();
        vm.SetRecipient(rid);
        vm.SetMonth(2026, 6);

        await vm.LoadAsync();
        vm.SelectedCell = vm.Cells[0];

        vm.EditorServiceStartTime.Should().Be(new TimeOnly(9, 0));
        vm.EditorServiceEndTime.Should().Be(new TimeOnly(15, 30));
        vm.EditorSpecialVisitSupportMinutes.Should().Be(0);
        vm.EditorOffsiteSupportApplied.Should().BeFalse();
        vm.EditorMedicalCoordinationType.Should().Be(MedicalCoordinationType.Unspecified);
        vm.EditorTrialUseSupportType.Should().Be(TrialUseSupportType.Unspecified);
        vm.EditorRegionalCollaborationApplied.Should().BeFalse();
        vm.EditorIntensiveSupportApplied.Should().BeNull();
        vm.EditorEmergencyAdmissionApplied.Should().BeFalse();
        vm.EditorRecipientConfirmation.Should().Be(RecipientConfirmationStatus.Unspecified);
        vm.EditorTransport.Should().Be(TransportKind.Round);
        vm.EditorMealProvided.Should().BeTrue();
        vm.EditorNote.Should().Be("note");

        vm.EditorAttendance = Attendance.Absent;
        await vm.SaveSelectedDailyRecordCommand.ExecuteAsync(null);

        var correction = _repo.Added[^1];
        correction.Kind.Should().Be(RecordKind.Correct);
        correction.SpecialVisitSupportMinutes.Should().Be(0);
        correction.OffsiteSupportApplied.Should().BeFalse();
        correction.RegionalCollaborationApplied.Should().BeFalse();
        correction.IntensiveSupportApplied.Should().BeNull();
        correction.EmergencyAdmissionApplied.Should().BeFalse();
        correction.MedicalCoordinationType.Should().Be(MedicalCoordinationType.Unspecified);
        correction.TrialUseSupportType.Should().Be(TrialUseSupportType.Unspecified);
        correction.RecipientConfirmation.Should().Be(RecipientConfirmationStatus.Unspecified);
        correction.Transport.Should().Be(TransportKind.Round);
        correction.MealProvided.Should().BeTrue();
        correction.Note.Should().Be("note");
    }

    [Fact]
    public async Task SaveSelectedDailyRecord_creates_new_with_all_ten_claim_values()
    {
        var vm = NewVm();
        vm.SetRecipient(Guid.NewGuid());
        vm.SetMonth(2026, 6);
        await vm.LoadAsync();
        vm.SelectedCell = vm.Cells[1];
        vm.EditorAttendance = Attendance.Present;
        vm.EditorTransport = TransportKind.Outbound;
        vm.EditorMealProvided = true;
        vm.EditorNote = "入力";
        vm.EditorServiceStartTime = new TimeOnly(8, 45);
        vm.EditorServiceEndTime = new TimeOnly(14, 0);
        vm.EditorSpecialVisitSupportMinutes = 30;
        vm.EditorOffsiteSupportApplied = true;
        vm.EditorMedicalCoordinationType = MedicalCoordinationType.TypeII;
        vm.EditorTrialUseSupportType = TrialUseSupportType.TypeI;
        vm.EditorRegionalCollaborationApplied = true;
        vm.EditorIntensiveSupportApplied = false;
        vm.EditorEmergencyAdmissionApplied = null;
        vm.EditorRecipientConfirmation = RecipientConfirmationStatus.Confirmed;

        await vm.SaveSelectedDailyRecordCommand.ExecuteAsync(null);

        var stored = _repo.Added.Single();
        stored.ServiceStartTime.Should().Be(new TimeOnly(8, 45));
        stored.ServiceEndTime.Should().Be(new TimeOnly(14, 0));
        stored.SpecialVisitSupportMinutes.Should().Be(30);
        stored.OffsiteSupportApplied.Should().BeTrue();
        stored.MedicalCoordinationType.Should().Be(MedicalCoordinationType.TypeII);
        stored.TrialUseSupportType.Should().Be(TrialUseSupportType.TypeI);
        stored.RegionalCollaborationApplied.Should().BeTrue();
        stored.IntensiveSupportApplied.Should().BeFalse();
        stored.EmergencyAdmissionApplied.Should().BeNull();
        stored.RecipientConfirmation.Should().Be(RecipientConfirmationStatus.Confirmed);
    }

    [Fact]
    public async Task SetAttendance_on_existing_record_preserves_all_claim_and_existing_values()
    {
        var rid = Guid.NewGuid();
        _repo.Added.Add(DailyRecord.NewRecord(
            Guid.NewGuid(), rid, new DateOnly(2026, 6, 1), Attendance.Present,
            TransportKind.Inbound, true, "preserve", "u", DateTimeOffset.UnixEpoch,
            new TimeOnly(10, 0), new TimeOnly(16, 0), 15, false,
            MedicalCoordinationType.TypeIII, TrialUseSupportType.TypeII,
            true, false, null, RecipientConfirmationStatus.Confirmed));
        var vm = NewVm();
        vm.SetRecipient(rid);
        vm.SetMonth(2026, 6);
        await vm.LoadAsync();

        await vm.Cells[0].SetAttendanceCommand.ExecuteAsync(Attendance.Absent);

        var correction = _repo.Added[^1];
        correction.Transport.Should().Be(TransportKind.Inbound);
        correction.MealProvided.Should().BeTrue();
        correction.Note.Should().Be("preserve");
        correction.ServiceStartTime.Should().Be(new TimeOnly(10, 0));
        correction.ServiceEndTime.Should().Be(new TimeOnly(16, 0));
        correction.SpecialVisitSupportMinutes.Should().Be(15);
        correction.OffsiteSupportApplied.Should().BeFalse();
        correction.MedicalCoordinationType.Should().Be(MedicalCoordinationType.TypeIII);
        correction.TrialUseSupportType.Should().Be(TrialUseSupportType.TypeII);
        correction.RegionalCollaborationApplied.Should().BeTrue();
        correction.IntensiveSupportApplied.Should().BeFalse();
        correction.EmergencyAdmissionApplied.Should().BeNull();
        correction.RecipientConfirmation.Should().Be(RecipientConfirmationStatus.Confirmed);
    }

    [Fact]
    public async Task IntensiveSupportEpisode_supports_new_correct_cancel_and_reentry_for_office_recipient_context()
    {
        var office = Office.Create(Guid.NewGuid(), "1234567890", "事業所",
            ServiceCategory.TypeB, RegionGrade.None, "u", DateTimeOffset.UnixEpoch, Guid.NewGuid());
        _offices.Add(office);
        var recipient = Recipient.Create(Guid.NewGuid(), "氏名", "シメイ",
            new DateOnly(1990, 1, 1), "u", DateTimeOffset.UnixEpoch, Guid.NewGuid());
        _recipients.Add(recipient);
        var vm = NewVm();
        await vm.InitializeAsync();
        vm.SelectedOffice = vm.Offices.Single();
        vm.SelectedRecipient = vm.Recipients.Single();
        await vm.LoadEpisodeAsync();
        vm.EpisodeRevisions.Should().BeEmpty();

        vm.EpisodeStartDate = new DateOnly(2026, 4, 1);
        await vm.SaveEpisodeCommand.ExecuteAsync(null);
        vm.EpisodeRevisions.Select(x => x.Kind).Should().Equal(RecordKind.New);
        vm.EpisodeCurrentHeadId.Should().NotBeNull();
        vm.EpisodeEffectiveHeadId.Should().Be(vm.EpisodeCurrentHeadId);

        vm.EpisodeStartDate = new DateOnly(2026, 5, 1);
        await vm.SaveEpisodeCommand.ExecuteAsync(null);
        vm.EpisodeRevisions.Select(x => x.Kind).Should().Equal(RecordKind.New, RecordKind.Correct);

        await vm.CancelEpisodeCommand.ExecuteAsync(null);
        vm.EpisodeRevisions.Select(x => x.Kind).Should().Equal(
            RecordKind.New, RecordKind.Correct, RecordKind.Cancel);
        vm.EpisodeEffectiveHeadId.Should().BeNull();
        _episodes.Items[^1].StartDate.Should().BeNull();

        vm.EpisodeStartDate = new DateOnly(2026, 6, 1);
        await vm.SaveEpisodeCommand.ExecuteAsync(null);
        vm.EpisodeRevisions[^1].Kind.Should().Be(RecordKind.Correct);
        vm.EpisodeEffectiveHeadId.Should().Be(vm.EpisodeCurrentHeadId);
        _episodes.Items.Should().OnlyContain(x => x.OfficeId == office.Id && x.RecipientId == recipient.Id);
    }

    [Fact]
    public async Task IntensiveSupportEpisode_stale_head_shows_fixed_reload_guidance()
    {
        var office = Office.Create(Guid.NewGuid(), "1234567890", "事業所",
            ServiceCategory.TypeB, RegionGrade.None, "u", DateTimeOffset.UnixEpoch, Guid.NewGuid());
        _offices.Add(office);
        var recipient = Recipient.Create(Guid.NewGuid(), "氏名", "シメイ",
            new DateOnly(1990, 1, 1), "u", DateTimeOffset.UnixEpoch, Guid.NewGuid());
        _recipients.Add(recipient);
        var vm = NewVm();
        await vm.InitializeAsync();
        vm.SelectedOffice = vm.Offices.Single();
        vm.SelectedRecipient = vm.Recipients.Single();
        vm.EpisodeStartDate = new DateOnly(2026, 4, 1);
        await vm.SaveEpisodeCommand.ExecuteAsync(null);
        var staleHead = vm.EpisodeCurrentHeadId!.Value;

        await new SetIntensiveSupportEpisodeUseCase(_episodes, _uow, _clock).ExecuteAsync(
            new SetIntensiveSupportEpisodeRequest(
                office.Id, recipient.Id, RecordKind.Correct, staleHead, new DateOnly(2026, 5, 1)),
            "other", default);
        vm.EpisodeStartDate = new DateOnly(2026, 6, 1);
        await vm.SaveEpisodeCommand.ExecuteAsync(null);

        vm.EpisodeErrorMessage.Should().Be(
            "重度支援対象期間は既に更新されています。最新状態を再読込してください。");
        vm.EpisodeRevisions.Should().HaveCount(2);
        vm.EpisodeStartDate.Should().Be(new DateOnly(2026, 5, 1));
    }

    [Fact]
    public async Task Cancel_makes_effective_attendance_null_no_destructive_update()
    {
        var vm = NewVm();
        var rid = Guid.NewGuid();
        vm.SetRecipient(rid);
        vm.SetMonth(2026, 6);
        await vm.LoadAsync();

        await vm.Cells[0].RecordCommand.ExecuteAsync(Attendance.Present);
        await vm.LoadAsync();
        await vm.Cells[0].CancelCommand.ExecuteAsync(null);
        await vm.LoadAsync();

        vm.Cells[0].EffectiveAttendance.Should().BeNull();
        _repo.Added.Count.Should().Be(2);  // 元レコードは残り、追記で取消行が追加
    }
}

internal sealed class InMemoryRecipientRepoForDaily : IRecipientRepository
{
    private readonly List<Recipient> _list = [];
    public void Add(Recipient r) => _list.Add(r);
    public Task AddAsync(Recipient r, CancellationToken ct) { _list.Add(r); return Task.CompletedTask; }
    public Task<Recipient?> FindByIdAsync(Guid id, CancellationToken ct) =>
        Task.FromResult(_list.FirstOrDefault(r => r.Id == id));
    public Task UpdateAsync(Recipient r, CancellationToken ct)
    {
        var idx = _list.FindIndex(x => x.Id == r.Id);
        if (idx >= 0) _list[idx] = r;
        return Task.CompletedTask;
    }
    public Task<IReadOnlyList<Recipient>> ListAsync(bool includeArchived, CancellationToken ct)
    {
        IEnumerable<Recipient> source = includeArchived ? _list : _list.Where(r => !r.IsArchived);
        return Task.FromResult<IReadOnlyList<Recipient>>(source.ToArray());
    }
}

internal sealed class FakeDailyRecordRepo : IDailyRecordRepository
{
    public List<DailyRecord> Added { get; } = new();
    public Task AddAsync(DailyRecord r, CancellationToken ct) { Added.Add(r); return Task.CompletedTask; }
    public Task<DailyRecord?> FindByIdAsync(Guid id, CancellationToken ct) =>
        Task.FromResult(Added.SingleOrDefault(r => r.Id == id));
    public Task<IReadOnlyList<DailyRecord>> ListByRecipientAndDateAsync(Guid rid, DateOnly d, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<DailyRecord>>(
            Added.Where(r => r.RecipientId == rid && r.ServiceDate == d).ToArray());
    public Task<IReadOnlyList<DailyRecord>> ListByRecipientAndMonthAsync(Guid rid, int y, int m, CancellationToken ct)
    {
        var from = new DateOnly(y, m, 1);
        var to = from.AddMonths(1).AddDays(-1);
        return Task.FromResult<IReadOnlyList<DailyRecord>>(
            Added.Where(r => r.RecipientId == rid && r.ServiceDate >= from && r.ServiceDate <= to).ToArray());
    }
}

internal sealed class FakeIntensiveSupportEpisodeRepo : IIntensiveSupportEpisodeRepository
{
    public List<IntensiveSupportEpisode> Items { get; } = [];

    public Task AddAsync(IntensiveSupportEpisode episode, CancellationToken ct)
    {
        Items.Add(episode);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<IntensiveSupportEpisode>> ListHistoryAsync(
        Guid officeId, Guid recipientId, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<IntensiveSupportEpisode>>(
            Items.Where(x => x.OfficeId == officeId && x.RecipientId == recipientId).ToArray());
}
