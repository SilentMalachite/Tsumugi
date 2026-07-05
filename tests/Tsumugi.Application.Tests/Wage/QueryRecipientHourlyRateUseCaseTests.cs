using FluentAssertions;
using Tsumugi.Application.Abstractions;
using Tsumugi.Application.UseCases.Wage;
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.Enums;
using Tsumugi.Domain.ValueObjects;
using Xunit;

namespace Tsumugi.Application.Tests.Wage;

public sealed class QueryRecipientHourlyRateUseCaseTests
{
    private static readonly Guid Office = Guid.Parse("00000000-0000-0000-0000-000000000010");
    private static readonly Guid Recipient = Guid.Parse("00000000-0000-0000-0000-000000000020");
    private static readonly DateRange Period = new(new DateOnly(2026, 4, 1), new DateOnly(2026, 9, 30));

    [Fact]
    public async Task Returns_empty_list_when_no_rates()
    {
        var repo = new FakeQueryHourlyRateRepo([]);
        var uc = new QueryRecipientHourlyRateUseCase(repo);

        var result = await uc.ExecuteAsync(Office, Recipient, default);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Returns_rates_for_office_and_recipient()
    {
        var rate = RecipientHourlyRate.NewRecord(
            Guid.NewGuid(), Office, Recipient, Period, 900,
            "alice", DateTimeOffset.UtcNow);
        var repo = new FakeQueryHourlyRateRepo([rate]);
        var uc = new QueryRecipientHourlyRateUseCase(repo);

        var result = await uc.ExecuteAsync(Office, Recipient, default);

        result.Should().HaveCount(1);
        result[0].HourlyYen.Should().Be(900);
        result[0].OfficeId.Should().Be(Office);
        result[0].RecipientId.Should().Be(Recipient);
    }

    [Fact]
    public async Task Returns_multiple_rates_mapped_correctly()
    {
        var rate1 = RecipientHourlyRate.NewRecord(
            Guid.NewGuid(), Office, Recipient, Period, 800,
            "alice", DateTimeOffset.UtcNow);
        var period2 = new DateRange(new DateOnly(2026, 10, 1), null);
        var rate2 = RecipientHourlyRate.NewRecord(
            Guid.NewGuid(), Office, Recipient, period2, 850,
            "bob", DateTimeOffset.UtcNow);
        var repo = new FakeQueryHourlyRateRepo([rate1, rate2]);
        var uc = new QueryRecipientHourlyRateUseCase(repo);

        var result = await uc.ExecuteAsync(Office, Recipient, default);

        result.Should().HaveCount(2);
        result.Select(r => r.HourlyYen).Should().Contain([800, 850]);
    }
}

file sealed class FakeQueryHourlyRateRepo(IEnumerable<RecipientHourlyRate> seed) : IRecipientHourlyRateRepository
{
    private readonly List<RecipientHourlyRate> _items = seed.ToList();
    public Task AddAsync(RecipientHourlyRate rate, CancellationToken ct)
    { _items.Add(rate); return Task.CompletedTask; }
    public Task<IReadOnlyList<RecipientHourlyRate>> ListByOfficeRecipientAsync(
        Guid officeId, Guid recipientId, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<RecipientHourlyRate>>(
            _items.Where(r => r.OfficeId == officeId && r.RecipientId == recipientId).ToArray());
    public Task<IReadOnlyList<RecipientHourlyRate>> ListByOfficeAsync(
        Guid officeId, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<RecipientHourlyRate>>(
            _items.Where(r => r.OfficeId == officeId).ToArray());
}
