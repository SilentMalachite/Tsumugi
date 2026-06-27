using FluentAssertions;
using Tsumugi.Application.Abstractions;
using Tsumugi.Application.UseCases.Certificate;
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.ValueObjects;
using Xunit;

namespace Tsumugi.Application.Tests;

public sealed class RegisterCertificateUseCaseTests
{
    [Fact]
    public async Task Adds_certificate_when_no_overlap()
    {
        var repo = new FakeCertificateRepository();
        var uow = new FakeUnitOfWork();
        var sut = new RegisterCertificateUseCase(repo, uow,
            new FixedTimeProvider(DateTimeOffset.UnixEpoch));

        var rid = Guid.NewGuid();
        var (dto, warnings) = await sut.ExecuteAsync(
            recipientId: rid, certificateNumber: "1234567890",
            validity: new DateRange(new DateOnly(2026, 4, 1), new DateOnly(2027, 3, 31)),
            supplyDays: 23, monthlyCostCap: 9300, municipality: "杉並区",
            actor: "u", ct: default);

        warnings.Should().BeEmpty();
        repo.Added.Should().ContainSingle();
        dto.CertificateNumber.Should().Be("1234567890");
    }

    [Fact]
    public async Task Surfaces_warning_when_period_overlaps_existing()
    {
        var repo = new FakeCertificateRepository();
        var rid = Guid.NewGuid();
        repo.Added.Add(Certificate.Create(Guid.NewGuid(), rid, "old",
            new DateRange(new DateOnly(2026, 1, 1), new DateOnly(2026, 6, 30)),
            0, 0, "x", "u", DateTimeOffset.UnixEpoch, Guid.NewGuid()));

        var sut = new RegisterCertificateUseCase(repo, new FakeUnitOfWork(),
            new FixedTimeProvider(DateTimeOffset.UnixEpoch));

        var (_, warnings) = await sut.ExecuteAsync(
            rid, "new",
            new DateRange(new DateOnly(2026, 6, 1), new DateOnly(2027, 3, 31)),  // overlaps
            0, 0, "x", "u", default);

        warnings.Should().NotBeEmpty();
        warnings.Should().ContainMatch("*重複*");
    }
    [Fact]
    public async Task Rejects_empty_recipient_id()
    {
        var sut = new RegisterCertificateUseCase(
            new FakeCertificateRepository(), new FakeUnitOfWork(),
            new FixedTimeProvider(DateTimeOffset.UnixEpoch));

        Func<Task> act = () => sut.ExecuteAsync(
            recipientId: Guid.Empty, certificateNumber: "123",
            validity: new DateRange(new DateOnly(2026, 4, 1), new DateOnly(2027, 3, 31)),
            supplyDays: 23, monthlyCostCap: 9300, municipality: "杉並区",
            actor: "u", ct: default);

        await act.Should().ThrowAsync<ArgumentException>()
            .Where(e => e.ParamName == "recipientId");
    }
}

internal sealed class FakeCertificateRepository : ICertificateRepository
{
    public List<Certificate> Added { get; } = new();
    public Task AddAsync(Certificate c, CancellationToken ct) { Added.Add(c); return Task.CompletedTask; }
    public Task<IReadOnlyList<Certificate>> ListByRecipientAsync(Guid rid, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<Certificate>>(Added.Where(c => c.RecipientId == rid).ToArray());
    public Task<IReadOnlyList<Certificate>> ListAllAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<Certificate>>(Added.ToArray());
    public Task<Certificate?> FindEffectiveAsync(Guid rid, DateOnly asOf, CancellationToken ct) =>
        Task.FromResult(Added.FirstOrDefault(c => c.RecipientId == rid && c.Validity.Contains(asOf)));
}
