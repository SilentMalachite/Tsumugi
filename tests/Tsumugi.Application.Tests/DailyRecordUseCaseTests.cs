using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Tsumugi.Application.Abstractions;
using Tsumugi.Application.UseCases.DailyRecord;
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.Enums;
using Xunit;

namespace Tsumugi.Application.Tests;

public sealed class DailyRecordUseCaseTests
{
    private readonly FakeDailyRecordRepository _repo = new();
    private readonly FakeUnitOfWork _uow = new();
    private readonly FixedTimeProvider _clock = new(DateTimeOffset.UnixEpoch);

    [Fact]
    public async Task Record_appends_new()
    {
        var sut = new RecordDailyRecordUseCase(_repo, _uow, _clock);
        var rid = Guid.NewGuid();
        var dto = await sut.ExecuteAsync(rid, new DateOnly(2026, 6, 1),
            Attendance.Present, TransportKind.Round, true, "通常", "u", default);
        _repo.Added.Should().ContainSingle();
        dto.Kind.Should().Be(RecordKind.New);
    }

    [Fact]
    public async Task Record_rejects_empty_recipient_id()
    {
        var sut = new RecordDailyRecordUseCase(_repo, _uow, _clock);
        Func<Task> act = () => sut.ExecuteAsync(
            Guid.Empty, new DateOnly(2026, 6, 1),
            Attendance.Present, TransportKind.None, false, null, "u", default);
        await act.Should().ThrowAsync<ArgumentException>()
            .Where(e => e.ParamName == "recipientId");
    }

    [Fact]
    public async Task Record_throws_when_new_already_exists()
    {
        var rid = Guid.NewGuid();
        var existing = DailyRecord.NewRecord(Guid.NewGuid(), rid, new DateOnly(2026, 6, 1),
            Attendance.Present, TransportKind.None, false, null, "u", DateTimeOffset.UnixEpoch);
        _repo.Added.Add(existing);

        var sut = new RecordDailyRecordUseCase(_repo, _uow, _clock);
        var act = () => sut.ExecuteAsync(rid, new DateOnly(2026, 6, 1),
            Attendance.Present, TransportKind.None, false, null, "u", default);
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Correct_appends_correction_with_origin()
    {
        var rid = Guid.NewGuid();
        var origin = DailyRecord.NewRecord(Guid.NewGuid(), rid, new DateOnly(2026, 6, 1),
            Attendance.Present, TransportKind.None, false, null, "u", DateTimeOffset.UnixEpoch);
        _repo.Added.Add(origin);

        var sut = new CorrectDailyRecordUseCase(_repo, _uow, _clock);
        var dto = await sut.ExecuteAsync(origin.Id, Attendance.Absent, TransportKind.None, false, "病欠", "u", default);

        dto.Kind.Should().Be(RecordKind.Correct);
        dto.OriginId.Should().Be(origin.Id);
        _repo.Added.Count.Should().Be(2);
    }

    [Fact]
    public async Task Correct_throws_when_origin_not_found()
    {
        var sut = new CorrectDailyRecordUseCase(_repo, _uow, _clock);
        var act = () => sut.ExecuteAsync(Guid.NewGuid(), Attendance.Present, TransportKind.None, false, null, "u", default);
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Cancel_appends_cancellation()
    {
        var rid = Guid.NewGuid();
        var origin = DailyRecord.NewRecord(Guid.NewGuid(), rid, new DateOnly(2026, 6, 1),
            Attendance.Present, TransportKind.None, false, null, "u", DateTimeOffset.UnixEpoch);
        _repo.Added.Add(origin);

        var sut = new CancelDailyRecordUseCase(_repo, _uow, _clock);
        var dto = await sut.ExecuteAsync(origin.Id, "u", default);
        dto.Kind.Should().Be(RecordKind.Cancel);
    }

    [Fact]
    public async Task Cancel_throws_when_origin_not_found()
    {
        var sut = new CancelDailyRecordUseCase(_repo, _uow, _clock);
        var act = () => sut.ExecuteAsync(Guid.NewGuid(), "u", default);
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Correct_throws_when_origin_is_cancelled()
    {
        var rid = Guid.NewGuid();
        var origin = DailyRecord.NewRecord(Guid.NewGuid(), rid, new DateOnly(2026, 6, 3),
            Attendance.Present, TransportKind.None, false, null, "u", DateTimeOffset.UnixEpoch);
        var cancel = DailyRecord.Cancellation(Guid.NewGuid(), rid, new DateOnly(2026, 6, 3), origin.Id,
            "u", DateTimeOffset.UnixEpoch.AddSeconds(1));
        _repo.Added.AddRange(new[] { origin, cancel });

        var sut = new CorrectDailyRecordUseCase(_repo, _uow, _clock);
        var act = () => sut.ExecuteAsync(cancel.Id, Attendance.Present, TransportKind.None, false, null, "u", default);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("取消済みレコードは訂正できません。");
    }

    [Fact]
    public async Task Cancel_throws_when_origin_is_already_cancelled()
    {
        var rid = Guid.NewGuid();
        var origin = DailyRecord.NewRecord(Guid.NewGuid(), rid, new DateOnly(2026, 6, 4),
            Attendance.Present, TransportKind.None, false, null, "u", DateTimeOffset.UnixEpoch);
        var cancel = DailyRecord.Cancellation(Guid.NewGuid(), rid, new DateOnly(2026, 6, 4), origin.Id,
            "u", DateTimeOffset.UnixEpoch.AddSeconds(1));
        _repo.Added.AddRange(new[] { origin, cancel });

        var sut = new CancelDailyRecordUseCase(_repo, _uow, _clock);
        var act = () => sut.ExecuteAsync(cancel.Id, "u", default);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("取消済みレコードを再度取り消すことはできません。");
    }

    [Fact]
    public async Task Correct_throws_when_origin_is_not_current_effective_due_to_sibling_cancel()
    {
        // R3-H1: 取消 (x) が n の sibling として既に入った状態で、n.Id を origin として訂正 (c) を追加すると、
        // DailyRecordPolicy.Effective は同一 origin の最新子を採るため、訂正が取消を上書きしてしまう。
        // Application 層で「現行実効レコードでないものへの訂正」を拒否することで防御する。
        var rid = Guid.NewGuid();
        var day = new DateOnly(2026, 6, 5);
        var n = DailyRecord.NewRecord(Guid.NewGuid(), rid, day,
            Attendance.Present, TransportKind.None, false, null, "u", DateTimeOffset.UnixEpoch);
        var cancel = DailyRecord.Cancellation(Guid.NewGuid(), rid, day, n.Id,
            "u", DateTimeOffset.UnixEpoch.AddSeconds(1));
        _repo.Added.AddRange(new[] { n, cancel });

        var sut = new CorrectDailyRecordUseCase(_repo, _uow, _clock);
        var act = () => sut.ExecuteAsync(n.Id,  // ← stale: n は cancel で既に実効から外れている
            Attendance.Absent, TransportKind.None, false, null, "u", default);

        await act.Should().ThrowAsync<InvalidOperationException>();
        _repo.Added.Count.Should().Be(2);  // 訂正は追記されていない
    }

    [Fact]
    public async Task Cancel_throws_when_origin_is_not_current_effective_due_to_prior_correction()
    {
        // 同じく取消側: n の後に correct c1 が入って実効が c1 になっている状態で、n.Id を origin として取消を出すのは禁止。
        var rid = Guid.NewGuid();
        var day = new DateOnly(2026, 6, 6);
        var n = DailyRecord.NewRecord(Guid.NewGuid(), rid, day,
            Attendance.Present, TransportKind.None, false, null, "u", DateTimeOffset.UnixEpoch);
        var c1 = DailyRecord.Correction(Guid.NewGuid(), rid, day, n.Id,
            Attendance.Absent, TransportKind.None, false, null, "u", DateTimeOffset.UnixEpoch.AddSeconds(1));
        _repo.Added.AddRange(new[] { n, c1 });

        var sut = new CancelDailyRecordUseCase(_repo, _uow, _clock);
        var act = () => sut.ExecuteAsync(n.Id, "u", default);  // ← stale: n は c1 で実効から外れている

        await act.Should().ThrowAsync<InvalidOperationException>();
        _repo.Added.Count.Should().Be(2);
    }

    [Fact]
    public async Task Correct_succeeds_when_origin_is_current_effective()
    {
        // 正常経路: 何も無い日に n を入れて、n.Id を origin として訂正 c を入れる。
        var rid = Guid.NewGuid();
        var day = new DateOnly(2026, 6, 7);
        var n = DailyRecord.NewRecord(Guid.NewGuid(), rid, day,
            Attendance.Present, TransportKind.None, false, null, "u", DateTimeOffset.UnixEpoch);
        _repo.Added.Add(n);

        var sut = new CorrectDailyRecordUseCase(_repo, _uow, _clock);
        await sut.ExecuteAsync(n.Id, Attendance.Absent, TransportKind.None, false, null, "u", default);

        _repo.Added.Count.Should().Be(2);
    }

    [Fact]
    public async Task QueryMonth_returns_effective_records()
    {
        var rid = Guid.NewGuid();
        var n = DailyRecord.NewRecord(Guid.NewGuid(), rid, new DateOnly(2026, 6, 1),
            Attendance.Present, TransportKind.None, false, null, "u", DateTimeOffset.UnixEpoch);
        var c = DailyRecord.Correction(Guid.NewGuid(), rid, new DateOnly(2026, 6, 1), n.Id,
            Attendance.Absent, TransportKind.None, false, "訂正", "u",
            DateTimeOffset.UnixEpoch.AddSeconds(1));
        _repo.Added.AddRange(new[] { n, c });

        var sut = new QueryMonthDailyRecordsUseCase(_repo);
        var result = await sut.ExecuteAsync(rid, 2026, 6, default);

        result.Should().ContainKey(new DateOnly(2026, 6, 1));
        result[new DateOnly(2026, 6, 1)].Kind.Should().Be(RecordKind.Correct);
    }

    [Fact]
    public async Task QueryMonth_excludes_cancelled_dates()
    {
        var rid = Guid.NewGuid();
        var n = DailyRecord.NewRecord(Guid.NewGuid(), rid, new DateOnly(2026, 6, 2),
            Attendance.Present, TransportKind.None, false, null, "u", DateTimeOffset.UnixEpoch);
        var cancel = DailyRecord.Cancellation(Guid.NewGuid(), rid, new DateOnly(2026, 6, 2), n.Id,
            "u", DateTimeOffset.UnixEpoch.AddSeconds(1));
        _repo.Added.AddRange(new[] { n, cancel });

        var sut = new QueryMonthDailyRecordsUseCase(_repo);
        var result = await sut.ExecuteAsync(rid, 2026, 6, default);

        result.Should().NotContainKey(new DateOnly(2026, 6, 2));
    }

    // ── Fakes ──────────────────────────────────────────────────────────────

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public int SaveCalls { get; private set; }
        public Task<int> SaveChangesAsync(CancellationToken ct) { SaveCalls++; return Task.FromResult(1); }
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class FakeDailyRecordRepository : IDailyRecordRepository
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
}
