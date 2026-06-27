using FluentAssertions;
using Tsumugi.App.ViewModels;
using Tsumugi.Application.UseCases.Certificate;
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.ValueObjects;
using Xunit;

namespace Tsumugi.App.Tests;

public sealed class CertificateViewModelTests
{
    [Fact]
    public async Task LoadAsync_populates_expiring_items()
    {
        var repo = new InMemoryCertRepo();
        repo.Add(Certificate.Create(Guid.NewGuid(), Guid.NewGuid(), "near",
            new DateRange(new DateOnly(2026, 1, 1), new DateOnly(2026, 7, 1)),
            0, 0, "x", "u", DateTimeOffset.UnixEpoch, Guid.NewGuid()));

        var sut = new CertificateViewModel(new ListExpiringCertificatesUseCase(repo));
        await sut.LoadAsync(new DateOnly(2026, 6, 27), thresholdDays: 30);

        sut.ExpiringItems.Should().ContainSingle();
        sut.ExpiringItems[0].RemainingDays.Should().Be(4);
    }
}

internal sealed class InMemoryCertRepo : Tsumugi.Application.Abstractions.ICertificateRepository
{
    private readonly List<Certificate> _list = new();
    public void Add(Certificate c) => _list.Add(c);
    public Task AddAsync(Certificate c, CancellationToken ct) { _list.Add(c); return Task.CompletedTask; }
    public Task<IReadOnlyList<Certificate>> ListByRecipientAsync(Guid rid, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<Certificate>>(_list.Where(c => c.RecipientId == rid).ToArray());
    public Task<IReadOnlyList<Certificate>> ListAllAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<Certificate>>(_list);
    public Task<Certificate?> FindEffectiveAsync(Guid rid, DateOnly asOf, CancellationToken ct) =>
        Task.FromResult<Certificate?>(null);
}
