using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Tsumugi.Application.Abstractions;
using Tsumugi.Application.UseCases;
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.Enums;
using Xunit;

namespace Tsumugi.Application.Tests;

public sealed class RegisterOfficeUseCaseTests
{
    private sealed class FakeOfficeRepository : IOfficeRepository
    {
        public Office? Added { get; private set; }
        public Office? Existing { get; init; }
        public Task AddAsync(Office office, CancellationToken ct) { Added = office; return Task.CompletedTask; }
        public Task<Office?> FindByNumberAsync(string n, CancellationToken ct) =>
            Task.FromResult(Existing?.OfficeNumber == n ? Existing : null);
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public int SaveCalls { get; private set; }
        public Task<int> SaveChangesAsync(CancellationToken ct) { SaveCalls++; return Task.FromResult(1); }
    }

    private static readonly TimeProvider Clock =
        new FixedClock(new DateTimeOffset(2026, 4, 1, 0, 0, 0, TimeSpan.Zero));

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    [Fact]
    public async Task Execute_persists_new_office_and_saves()
    {
        var repo = new FakeOfficeRepository();
        var uow = new FakeUnitOfWork();
        var sut = new RegisterOfficeUseCase(repo, uow, Clock);

        var dto = await sut.ExecuteAsync("1234567890", "つむぎ作業所", "tester", CancellationToken.None);

        dto.OfficeNumber.Should().Be("1234567890");
        repo.Added.Should().NotBeNull();
        repo.Added!.CreatedBy.Should().Be("tester");
        repo.Added.ConcurrencyToken.Should().NotBe(Guid.Empty);
        uow.SaveCalls.Should().Be(1);
    }

    [Fact]
    public async Task Execute_rejects_duplicate_office_number()
    {
        var existing = Office.Create(Guid.NewGuid(), "1234567890", "既存", ServiceCategory.TypeB, RegionGrade.None, "u", DateTimeOffset.UnixEpoch, Guid.NewGuid());
        var repo = new FakeOfficeRepository { Existing = existing };
        var sut = new RegisterOfficeUseCase(repo, new FakeUnitOfWork(), Clock);

        var act = () => sut.ExecuteAsync("1234567890", "別名", "tester", CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Theory]
    [InlineData("", "name")]
    [InlineData("123", "")]
    public async Task Execute_rejects_blank_input(string number, string name)
    {
        var sut = new RegisterOfficeUseCase(new FakeOfficeRepository(), new FakeUnitOfWork(), Clock);

        var act = () => sut.ExecuteAsync(number, name, "tester", CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>();
    }
}
