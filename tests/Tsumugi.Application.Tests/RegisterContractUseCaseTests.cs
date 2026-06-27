using FluentAssertions;
using Tsumugi.Application.Abstractions;
using Tsumugi.Application.UseCases.Contract;
using Tsumugi.Application.Validation;
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.ValueObjects;
using Xunit;

namespace Tsumugi.Application.Tests;

public sealed class RegisterContractUseCaseTests
{
    [Fact]
    public async Task Adds_contract_when_no_overlap()
    {
        var repo = new FakeContractRepository();
        var uow = new FakeUnitOfWork();
        var sut = new RegisterContractUseCase(repo, uow,
            new FixedTimeProvider(DateTimeOffset.UnixEpoch));

        var rid = Guid.NewGuid();
        var (dto, warnings) = await sut.ExecuteAsync(
            recipientId: rid,
            period: new DateRange(new DateOnly(2026, 4, 1), new DateOnly(2027, 3, 31)),
            contractedSupplyDays: 23,
            actor: "u", ct: default);

        warnings.Should().BeEmpty();
        repo.Added.Should().ContainSingle();
        dto.RecipientId.Should().Be(rid);
        dto.ContractedSupplyDays.Should().Be(23);
        uow.SaveCount.Should().Be(1);
    }

    [Fact]
    public async Task Surfaces_warning_when_period_overlaps_existing()
    {
        var repo = new FakeContractRepository();
        var rid = Guid.NewGuid();
        repo.Added.Add(Contract.Create(Guid.NewGuid(), rid,
            new DateRange(new DateOnly(2026, 1, 1), new DateOnly(2026, 6, 30)),
            20, "u", DateTimeOffset.UnixEpoch, Guid.NewGuid()));

        var sut = new RegisterContractUseCase(repo, new FakeUnitOfWork(),
            new FixedTimeProvider(DateTimeOffset.UnixEpoch));

        var (_, warnings) = await sut.ExecuteAsync(
            rid,
            new DateRange(new DateOnly(2026, 6, 1), new DateOnly(2027, 3, 31)),  // overlaps
            23, "u", default);

        warnings.Should().NotBeEmpty();
        warnings.Should().ContainMatch("*重複*");
    }

    [Fact]
    public async Task Rejects_empty_recipient_id()
    {
        var repo = new FakeContractRepository();
        var sut = new RegisterContractUseCase(repo, new FakeUnitOfWork(),
            new FixedTimeProvider(DateTimeOffset.UnixEpoch));

        Func<Task> act = () => sut.ExecuteAsync(
            recipientId: Guid.Empty,  // 利用者未選択は受け付けない
            period: new DateRange(new DateOnly(2026, 4, 1), new DateOnly(2027, 3, 31)),
            contractedSupplyDays: 23,
            actor: "u", ct: default);

        await act.Should().ThrowAsync<ArgumentException>()
            .Where(e => e.ParamName == "recipientId");
        repo.Added.Should().BeEmpty();
    }

    [Fact]
    public async Task Rejects_period_start_out_of_valid_range()
    {
        var sut = new RegisterContractUseCase(
            new FakeContractRepository(), new FakeUnitOfWork(),
            new FixedTimeProvider(DateTimeOffset.UnixEpoch));

        // DateRange ctor allows 1800 date (end >= start), but DateValidator.EnsureRange rejects < 1900
        Func<Task> act = () => sut.ExecuteAsync(
            Guid.NewGuid(),
            new DateRange(new DateOnly(1800, 1, 1), new DateOnly(1800, 12, 31)),
            23, "u", default);

        await act.Should().ThrowAsync<DateValidationException>();
    }
}

internal sealed class FakeContractRepository : IContractRepository
{
    public List<Contract> Added { get; } = new();
    public Task AddAsync(Contract c, CancellationToken ct) { Added.Add(c); return Task.CompletedTask; }
    public Task<IReadOnlyList<Contract>> ListByRecipientAsync(Guid recipientId, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<Contract>>(Added.Where(c => c.RecipientId == recipientId).ToArray());
    public Task<Contract?> FindEffectiveAsync(Guid recipientId, DateOnly asOf, CancellationToken ct) =>
        Task.FromResult(Added.FirstOrDefault(c => c.RecipientId == recipientId && c.Period.Contains(asOf)));
}
