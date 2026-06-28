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

        var input = new RegisterRecipientInput("山田太郎", "ヤマダタロウ", new DateOnly(1990, 1, 1));
        var dto = await sut.ExecuteAsync(input, "tester", default);

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
        Func<Task> act = () => sut.ExecuteAsync(
            new RegisterRecipientInput(" ", "x", new DateOnly(1990, 1, 1)), "u", default);
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task Rejects_unrealistic_date_of_birth_via_validator()
    {
        var sut = new RegisterRecipientUseCase(
            new FakeRecipientRepository(), new FakeUnitOfWork(),
            new FixedTimeProvider(DateTimeOffset.UnixEpoch));
        Func<Task> act = () => sut.ExecuteAsync(
            new RegisterRecipientInput("山田", "ヤマダ", DateOnly.MinValue), "u", default);
        await act.Should().ThrowAsync<DateValidationException>();
    }

    [Fact]
    public async Task Persists_disability_and_contact_fields()
    {
        var repo = new FakeRecipientRepository();
        var sut = new RegisterRecipientUseCase(repo, new FakeUnitOfWork(),
            new FixedTimeProvider(DateTimeOffset.UnixEpoch));
        var input = new RegisterRecipientInput("田中", "タナカ", new DateOnly(1990, 1, 1))
        {
            Disabilities = new Tsumugi.Domain.ValueObjects.DisabilityCategories(true, false, true, false),
            PostalCode = "100-0001",
            Address = "東京都千代田区...",
            PhoneNumber = "03-0000-0000",
            EmailAddress = "tanaka@example.com",
            EmergencyContactName = "緊急一郎",
            EmergencyContactRelationship = "兄",
            EmergencyContactPhone = "090-0000-0000",
        };
        var dto = await sut.ExecuteAsync(input, "u", default);

        dto.Disabilities.Physical.Should().BeTrue();
        dto.Disabilities.Mental.Should().BeTrue();
        dto.Disabilities.Intellectual.Should().BeFalse();
        dto.PhoneNumber.Should().Be("03-0000-0000");
        dto.Address.Should().Be("東京都千代田区...");
        dto.EmergencyContactName.Should().Be("緊急一郎");

        var stored = repo.Added.Single();
        stored.PostalCode.Should().Be("100-0001");
        stored.EmailAddress.Should().Be("tanaka@example.com");
        stored.EmergencyContactPhone.Should().Be("090-0000-0000");
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
    public Task<IReadOnlyList<Recipient>> ListAsync(bool includeArchived, CancellationToken ct)
    {
        IEnumerable<Recipient> source = includeArchived
            ? Added
            : Added.Where(r => !r.IsArchived);
        return Task.FromResult<IReadOnlyList<Recipient>>(source.ToList());
    }
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
