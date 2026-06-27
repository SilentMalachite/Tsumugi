using FluentAssertions;
using Tsumugi.Application.Abstractions;
using Tsumugi.Application.UseCases.Recipient;
using Tsumugi.Application.Validation;
using Tsumugi.Domain.Entities;
using Xunit;

namespace Tsumugi.Application.Tests;

public sealed class RegisterRecipientUseCaseTests
{
    [Fact]
    public async Task Adds_recipient_with_generated_id_and_token()
    {
        var repo = new FakeRecipientRepository();
        var uow = new FakeUnitOfWork();
        var tp = new FixedTimeProvider(new DateTimeOffset(2026, 6, 27, 0, 0, 0, TimeSpan.Zero));
        var sut = new RegisterRecipientUseCase(repo, uow, tp);

        var dto = await sut.ExecuteAsync(
            kanjiName: "山田太郎", kanaName: "ヤマダタロウ",
            dateOfBirth: new DateOnly(1990, 1, 1), actor: "tester", default);

        repo.Added.Should().ContainSingle();
        dto.KanjiName.Should().Be("山田太郎");
        uow.SaveCount.Should().Be(1);
    }

    [Fact]
    public async Task Rejects_blank_kanji_name()
    {
        var sut = new RegisterRecipientUseCase(
            new FakeRecipientRepository(), new FakeUnitOfWork(),
            new FixedTimeProvider(DateTimeOffset.UnixEpoch));
        Func<Task> act = () => sut.ExecuteAsync(" ", "x", new DateOnly(1990, 1, 1), "u", default);
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task Rejects_unrealistic_date_of_birth_via_validator()
    {
        var sut = new RegisterRecipientUseCase(
            new FakeRecipientRepository(), new FakeUnitOfWork(),
            new FixedTimeProvider(DateTimeOffset.UnixEpoch));
        Func<Task> act = () => sut.ExecuteAsync("山田", "ヤマダ", DateOnly.MinValue, "u", default);
        await act.Should().ThrowAsync<DateValidationException>();
    }
}

internal sealed class FakeRecipientRepository : IRecipientRepository
{
    public List<Recipient> Added { get; } = new();
    public Task AddAsync(Recipient r, CancellationToken ct) { Added.Add(r); return Task.CompletedTask; }
    public Task<Recipient?> FindByIdAsync(Guid id, CancellationToken ct) =>
        Task.FromResult(Added.SingleOrDefault(r => r.Id == id));
    public Task UpdateAsync(Recipient r, CancellationToken ct)
    {
        Added[Added.FindIndex(x => x.Id == r.Id)] = r;
        return Task.CompletedTask;
    }
    public Task<IReadOnlyList<Recipient>> ListAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<Recipient>>(Added);
}

internal sealed class FakeUnitOfWork : IUnitOfWork
{
    public int SaveCount { get; private set; }
    public Task<int> SaveChangesAsync(CancellationToken ct) { SaveCount++; return Task.FromResult(SaveCount); }
}

internal sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}
