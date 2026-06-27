using FluentAssertions;
using Tsumugi.Application.Abstractions;
using Tsumugi.Application.UseCases.OfficeCapability;
using Tsumugi.Application.Validation;
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.ValueObjects;
using Xunit;

namespace Tsumugi.Application.Tests;

public sealed class RegisterOfficeCapabilityUseCaseTests
{
    [Fact]
    public async Task Adds_capability_when_no_overlap()
    {
        var repo = new FakeOfficeCapabilityRepository();
        var uow = new FakeUnitOfWork();
        var sut = new RegisterOfficeCapabilityUseCase(repo, uow,
            new FixedTimeProvider(DateTimeOffset.UnixEpoch));

        var oid = Guid.NewGuid();
        var flags = new Dictionary<string, bool> { ["intensiveSupport"] = true };
        var (dto, warnings) = await sut.ExecuteAsync(
            officeId: oid,
            period: new DateRange(new DateOnly(2026, 4, 1), new DateOnly(2027, 3, 31)),
            flags: flags,
            actor: "u", ct: default);

        warnings.Should().BeEmpty();
        repo.Added.Should().ContainSingle();
        dto.OfficeId.Should().Be(oid);
        dto.Flags["intensiveSupport"].Should().BeTrue();
        uow.SaveCount.Should().Be(1);
    }

    [Fact]
    public async Task Surfaces_warning_when_period_overlaps_existing()
    {
        var repo = new FakeOfficeCapabilityRepository();
        var oid = Guid.NewGuid();
        repo.Added.Add(OfficeCapability.Create(Guid.NewGuid(), oid,
            new DateRange(new DateOnly(2026, 1, 1), new DateOnly(2026, 6, 30)),
            new Dictionary<string, bool>(),
            "u", DateTimeOffset.UnixEpoch, Guid.NewGuid()));

        var sut = new RegisterOfficeCapabilityUseCase(repo, new FakeUnitOfWork(),
            new FixedTimeProvider(DateTimeOffset.UnixEpoch));

        var (_, warnings) = await sut.ExecuteAsync(
            oid,
            new DateRange(new DateOnly(2026, 6, 1), new DateOnly(2027, 3, 31)),  // overlaps
            new Dictionary<string, bool>(), "u", default);

        warnings.Should().NotBeEmpty();
        warnings.Should().ContainMatch("*重複*");
    }

    [Fact]
    public async Task Rejects_period_start_out_of_valid_range()
    {
        var sut = new RegisterOfficeCapabilityUseCase(
            new FakeOfficeCapabilityRepository(), new FakeUnitOfWork(),
            new FixedTimeProvider(DateTimeOffset.UnixEpoch));

        // DateRange ctor allows 1800 date (end >= start), but DateValidator.EnsureRange rejects < 1900
        Func<Task> act = () => sut.ExecuteAsync(
            Guid.NewGuid(),
            new DateRange(new DateOnly(1800, 1, 1), new DateOnly(1800, 12, 31)),
            new Dictionary<string, bool>(), "u", default);

        await act.Should().ThrowAsync<DateValidationException>();
    }

    [Fact]
    public async Task Rejects_empty_office_id()
    {
        var sut = new RegisterOfficeCapabilityUseCase(
            new FakeOfficeCapabilityRepository(), new FakeUnitOfWork(),
            new FixedTimeProvider(DateTimeOffset.UnixEpoch));

        Func<Task> act = () => sut.ExecuteAsync(
            officeId: Guid.Empty,
            period: new DateRange(new DateOnly(2026, 4, 1), new DateOnly(2027, 3, 31)),
            flags: new Dictionary<string, bool>(),
            actor: "u", ct: default);

        await act.Should().ThrowAsync<ArgumentException>()
            .Where(e => e.ParamName == "officeId");
    }
}

internal sealed class FakeOfficeCapabilityRepository : IOfficeCapabilityRepository
{
    public List<OfficeCapability> Added { get; } = new();
    public Task AddAsync(OfficeCapability c, CancellationToken ct) { Added.Add(c); return Task.CompletedTask; }
    public Task<IReadOnlyList<OfficeCapability>> ListByOfficeAsync(Guid officeId, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<OfficeCapability>>(Added.Where(c => c.OfficeId == officeId).ToArray());
    public Task<OfficeCapability?> FindEffectiveAsync(Guid officeId, DateOnly asOf, CancellationToken ct) =>
        Task.FromResult(Added.FirstOrDefault(c => c.OfficeId == officeId && c.Period.Contains(asOf)));
}
