using FluentAssertions;
using Tsumugi.Application.Abstractions;
using Tsumugi.Application.UseCases.Wage;
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.Enums;
using Tsumugi.Domain.Logic.Wage;
using Tsumugi.Domain.ValueObjects;
using Xunit;

namespace Tsumugi.Application.Tests;

public sealed class CloseWagesUseCaseTests
{
    private static readonly DateTimeOffset T0 = new(2026, 8, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly Guid Office = Guid.NewGuid();

    private static readonly IReadOnlyList<IWageMethodStrategy> AllStrategies = new IWageMethodStrategy[]
    {
        new PieceWageStrategy(), new HourlyWageStrategy(),
        new FixedWageStrategy(), new EqualWageStrategy(),
    };

    private static Recipient Rec(Guid id) => Recipient.Create(
        id, "氏名", "シメイ", new DateOnly(1990, 1, 1), "t", T0, Guid.NewGuid());

    private static Contract ContractFor(Guid rid, DateRange period) => Contract.Create(
        Guid.NewGuid(), rid, period, 22, "t", T0, Guid.NewGuid());

    private static WageSettings Settings() => WageSettings.Create(
        Guid.NewGuid(), Office, new DateRange(new DateOnly(2026, 4, 1), null),
        WageMethod.Hourly, RoundingRule.FloorYen, RemainderPolicy.LargestRemainder, 4, null, "t", T0);

    private static WageFund Fund(int yen) =>
        WageFund.NewRecord(Guid.NewGuid(), Office, new YearMonth(2026, 7), yen, null, "t", T0);

    private static Tsumugi.Domain.Entities.DailyRecord Present(Guid rid, DateOnly date) =>
        Tsumugi.Domain.Entities.DailyRecord.NewRecord(
            Guid.NewGuid(), rid, date, Attendance.Present, TransportKind.None, false, null, "t", T0);

    private static Tsumugi.Domain.Entities.WorkRecord Work(Guid rid, DateOnly date, int minutes) =>
        Tsumugi.Domain.Entities.WorkRecord.NewRecord(
            Guid.NewGuid(), rid, date, minutes, null, null, null, null, "t", T0);

    private sealed class InMemoryStatementRepo : IWageStatementRepository
    {
        public List<WageStatement> Items { get; } = new();
        public Task AddAsync(WageStatement s, CancellationToken ct) { Items.Add(s); return Task.CompletedTask; }
        public Task<IReadOnlyList<WageStatement>> ListByOfficeAndMonthAsync(
            Guid officeId, int year, int month, CancellationToken ct)
        {
            var ym = new YearMonth(year, month);
            IReadOnlyList<WageStatement> r = Items
                .Where(s => s.OfficeId == officeId && s.Month == ym)
                .ToList();
            return Task.FromResult(r);
        }
    }

    private static CloseWagesUseCase BuildClose(
        IEnumerable<Recipient> recipients,
        IEnumerable<Contract> contracts,
        IEnumerable<Tsumugi.Domain.Entities.DailyRecord> daily,
        IEnumerable<Tsumugi.Domain.Entities.WorkRecord> work,
        IEnumerable<WageFund> funds,
        IEnumerable<WageSettings> settings,
        InMemoryStatementRepo stmtRepo,
        DateTimeOffset now)
    {
        var calculate = new CalculateWagesUseCase(
            new FakeDailyRecordRepoSeeded(daily),
            new FakeWorkRecordRepoSeeded(work),
            new FakeWageFundRepoSeeded(funds),
            new FakeWageSettingsRepoSeeded(settings),
            new FakeContractRepoSeeded(contracts),
            new FakeRecipientRepoSeeded(recipients),
            AllStrategies);
        return new CloseWagesUseCase(
            calculate, stmtRepo, new FakeUnitOfWork(),
            new FixedTimeProvider(now), new NoopAuditTrail());
    }

    [Fact]
    public async Task First_close_creates_new_statements_per_recipient()
    {
        var r1 = Rec(Guid.NewGuid());
        var r2 = Rec(Guid.NewGuid());
        var period = new DateRange(new DateOnly(2026, 4, 1), null);
        var stmtRepo = new InMemoryStatementRepo();

        var close = BuildClose(
            new[] { r1, r2 },
            new[] { ContractFor(r1.Id, period), ContractFor(r2.Id, period) },
            new[] { Present(r1.Id, new DateOnly(2026, 7, 1)), Present(r2.Id, new DateOnly(2026, 7, 1)) },
            new[] { Work(r1.Id, new DateOnly(2026, 7, 1), 600), Work(r2.Id, new DateOnly(2026, 7, 1), 400) },
            new[] { Fund(100_000) },
            new[] { Settings() },
            stmtRepo,
            T0);

        var results = await close.ExecuteAsync(Office, 2026, 7, "alice", default);

        results.Should().HaveCount(2);
        results.Should().AllSatisfy(s => s.Kind.Should().Be(RecordKind.New));
        results.Sum(s => s.AmountYen).Should().Be(100_000);
        stmtRepo.Items.Should().HaveCount(2);
    }

    [Fact]
    public async Task Re_close_adds_correction_records_referring_original_origin()
    {
        var r1 = Rec(Guid.NewGuid());
        var period = new DateRange(new DateOnly(2026, 4, 1), null);
        var stmtRepo = new InMemoryStatementRepo();

        var dailyList = new[] { Present(r1.Id, new DateOnly(2026, 7, 1)) };
        var workList = new[] { Work(r1.Id, new DateOnly(2026, 7, 1), 600) };

        var close = BuildClose(
            new[] { r1 },
            new[] { ContractFor(r1.Id, period) },
            dailyList, workList,
            new[] { Fund(100_000) },
            new[] { Settings() },
            stmtRepo,
            T0);

        var first = await close.ExecuteAsync(Office, 2026, 7, "alice", default);
        var second = await close.ExecuteAsync(Office, 2026, 7, "alice", default);

        first.Should().ContainSingle().Which.Kind.Should().Be(RecordKind.New);
        second.Should().ContainSingle().Which.Kind.Should().Be(RecordKind.Correct);
        second[0].OriginId.Should().Be(first[0].Id);
        stmtRepo.Items.Should().HaveCount(2);
    }

    [Fact]
    public async Task Rejects_blank_actor()
    {
        var stmtRepo = new InMemoryStatementRepo();
        var close = BuildClose(
            Array.Empty<Recipient>(), Array.Empty<Contract>(),
            Array.Empty<Tsumugi.Domain.Entities.DailyRecord>(),
            Array.Empty<Tsumugi.Domain.Entities.WorkRecord>(),
            Array.Empty<WageFund>(),
            new[] { Settings() },
            stmtRepo, T0);

        var act = async () => await close.ExecuteAsync(Office, 2026, 7, "  ", default);
        await act.Should().ThrowAsync<ArgumentException>().Where(e => e.ParamName == "actor");
    }

    [Fact]
    public async Task Query_returns_effective_per_recipient_only()
    {
        var r1 = Rec(Guid.NewGuid());
        var period = new DateRange(new DateOnly(2026, 4, 1), null);
        var stmtRepo = new InMemoryStatementRepo();

        var close = BuildClose(
            new[] { r1 },
            new[] { ContractFor(r1.Id, period) },
            new[] { Present(r1.Id, new DateOnly(2026, 7, 1)) },
            new[] { Work(r1.Id, new DateOnly(2026, 7, 1), 600) },
            new[] { Fund(100_000) },
            new[] { Settings() },
            stmtRepo, T0);

        await close.ExecuteAsync(Office, 2026, 7, "alice", default);
        await close.ExecuteAsync(Office, 2026, 7, "alice", default);

        var query = new QueryWageStatementUseCase(stmtRepo);
        var list = await query.ExecuteAsync(Office, 2026, 7, default);

        list.Should().ContainSingle();
        list[0].Kind.Should().Be(RecordKind.Correct);
        list[0].RecipientId.Should().Be(r1.Id);
    }
}
