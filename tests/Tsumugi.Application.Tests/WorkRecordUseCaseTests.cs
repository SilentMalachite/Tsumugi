using FluentAssertions;
using Tsumugi.Application.Abstractions;
using Tsumugi.Application.UseCases.WorkRecord;
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.Enums;
using Xunit;

namespace Tsumugi.Application.Tests;

public sealed class WorkRecordUseCaseTests
{
    private static readonly DateTimeOffset Clock0 = new(2026, 7, 1, 9, 0, 0, TimeSpan.Zero);

    private sealed class InMemoryRepo : IWorkRecordRepository
    {
        public List<WorkRecord> Items { get; } = new();
        public Task AddAsync(WorkRecord r, CancellationToken ct)
        {
            Items.Add(r);
            return Task.CompletedTask;
        }
        public Task<WorkRecord?> FindByIdAsync(Guid id, CancellationToken ct)
            => Task.FromResult<WorkRecord?>(Items.FirstOrDefault(r => r.Id == id));
        public Task<IReadOnlyList<WorkRecord>> ListByRecipientAndMonthAsync(
            Guid recipientId, int year, int month, CancellationToken ct)
        {
            var first = new DateOnly(year, month, 1);
            var last = new DateOnly(year, month, DateTime.DaysInMonth(year, month));
            IReadOnlyList<WorkRecord> r = Items
                .Where(x => x.RecipientId == recipientId && x.WorkDate >= first && x.WorkDate <= last)
                .ToList();
            return Task.FromResult(r);
        }
    }

    [Fact]
    public async Task Record_new_work_creates_entity()
    {
        var repo = new InMemoryRepo();
        var u = new RecordWorkUseCase(repo, new FakeUnitOfWork(), new FixedTimeProvider(Clock0));
        var dto = await u.ExecuteAsync(
            Guid.NewGuid(), new DateOnly(2026, 7, 1),
            workedMinutes: 240, pieceCount: null, pieceUnitYen: null, points: null,
            note: null, actor: "tester", CancellationToken.None);
        dto.Kind.Should().Be(RecordKind.New);
        dto.WorkedMinutes.Should().Be(240);
        repo.Items.Should().ContainSingle();
    }

    [Fact]
    public async Task Duplicate_new_record_for_same_date_is_rejected()
    {
        var repo = new InMemoryRepo();
        var rid = Guid.NewGuid();
        var date = new DateOnly(2026, 7, 1);
        var u = new RecordWorkUseCase(repo, new FakeUnitOfWork(), new FixedTimeProvider(Clock0));
        await u.ExecuteAsync(rid, date, 240, null, null, null, null, "t", default);

        var act = async () => await u.ExecuteAsync(rid, date, 200, null, null, null, null, "t", default);
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*新規記録が既に存在*");
    }

    [Fact]
    public async Task Correct_replaces_effective_with_origin_check()
    {
        var repo = new InMemoryRepo();
        var rid = Guid.NewGuid();
        var clock = new FixedTimeProvider(Clock0);
        var record = new RecordWorkUseCase(repo, new FakeUnitOfWork(), clock);
        var correct = new CorrectWorkUseCase(repo, new FakeUnitOfWork(), new FixedTimeProvider(Clock0.AddMinutes(1)));

        var first = await record.ExecuteAsync(rid, new DateOnly(2026, 7, 1), 240, null, null, null, null, "t", default);
        var fixedDto = await correct.ExecuteAsync(first.Id, 200, null, null, null, "訂正", "t", default);

        fixedDto.Kind.Should().Be(RecordKind.Correct);
        fixedDto.OriginId.Should().Be(first.Id);
        fixedDto.WorkedMinutes.Should().Be(200);
        repo.Items.Should().HaveCount(2);
    }

    [Fact]
    public async Task Correct_against_stale_origin_is_rejected()
    {
        var repo = new InMemoryRepo();
        var rid = Guid.NewGuid();
        var clock = new FixedTimeProvider(Clock0);
        var record = new RecordWorkUseCase(repo, new FakeUnitOfWork(), clock);
        var correct = new CorrectWorkUseCase(repo, new FakeUnitOfWork(), new FixedTimeProvider(Clock0.AddMinutes(1)));

        var first = await record.ExecuteAsync(rid, new DateOnly(2026, 7, 1), 240, null, null, null, null, "t", default);
        await correct.ExecuteAsync(first.Id, 200, null, null, null, null, "t", default);

        var act = async () => await correct.ExecuteAsync(first.Id, 150, null, null, null, null, "t", default);
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*現行の実効状態ではありません*");
    }

    [Fact]
    public async Task Cancel_creates_cancel_record_and_makes_effective_null()
    {
        var repo = new InMemoryRepo();
        var rid = Guid.NewGuid();
        var clock = new FixedTimeProvider(Clock0);
        var record = new RecordWorkUseCase(repo, new FakeUnitOfWork(), clock);
        var cancel = new CancelWorkUseCase(repo, new FakeUnitOfWork(), new FixedTimeProvider(Clock0.AddMinutes(1)));

        var first = await record.ExecuteAsync(rid, new DateOnly(2026, 7, 1), 240, null, null, null, null, "t", default);
        var cancelled = await cancel.ExecuteAsync(first.Id, "t", default);

        cancelled.Kind.Should().Be(RecordKind.Cancel);
        cancelled.OriginId.Should().Be(first.Id);

        var query = new QueryMonthWorkUseCase(repo);
        var byDate = await query.ExecuteAsync(rid, 2026, 7, default);
        byDate.Should().BeEmpty();
    }

    [Fact]
    public async Task Cancel_twice_is_rejected()
    {
        var repo = new InMemoryRepo();
        var rid = Guid.NewGuid();
        var clock = new FixedTimeProvider(Clock0);
        var record = new RecordWorkUseCase(repo, new FakeUnitOfWork(), clock);
        var cancel = new CancelWorkUseCase(repo, new FakeUnitOfWork(), new FixedTimeProvider(Clock0.AddMinutes(1)));

        var first = await record.ExecuteAsync(rid, new DateOnly(2026, 7, 1), 240, null, null, null, null, "t", default);
        var cancelled = await cancel.ExecuteAsync(first.Id, "t", default);

        var act = async () => await cancel.ExecuteAsync(cancelled.Id, "t", default);
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*取消済み*");
    }

    [Fact]
    public async Task Query_month_returns_only_effective_per_day()
    {
        var repo = new InMemoryRepo();
        var rid = Guid.NewGuid();
        var record = new RecordWorkUseCase(repo, new FakeUnitOfWork(), new FixedTimeProvider(Clock0));

        await record.ExecuteAsync(rid, new DateOnly(2026, 7, 1), 240, null, null, null, null, "t", default);
        await record.ExecuteAsync(rid, new DateOnly(2026, 7, 2), 360, null, null, null, null, "t", default);

        var query = new QueryMonthWorkUseCase(repo);
        var byDate = await query.ExecuteAsync(rid, 2026, 7, default);
        byDate.Should().HaveCount(2);
        byDate[new DateOnly(2026, 7, 1)].WorkedMinutes.Should().Be(240);
        byDate[new DateOnly(2026, 7, 2)].WorkedMinutes.Should().Be(360);
    }
}
