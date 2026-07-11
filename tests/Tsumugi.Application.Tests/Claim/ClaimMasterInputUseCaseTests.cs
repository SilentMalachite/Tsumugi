using FluentAssertions;
using Tsumugi.Application.Abstractions;
using Tsumugi.Application.UseCases;
using Tsumugi.Application.UseCases.Certificate;
using Tsumugi.Application.UseCases.Office;
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.Enums;

namespace Tsumugi.Application.Tests.Claim;

public sealed class ClaimMasterInputUseCaseTests
{
    private static readonly DateOnly ContractDate = new(2026, 4, 1);
    private static readonly TimeProvider Clock =
        new FixedClock(new DateTimeOffset(2026, 7, 11, 0, 0, 0, TimeSpan.Zero));

    [Fact]
    public async Task Register_office_persists_null_claim_inputs()
    {
        var repo = new OfficeRepositoryFake();
        var sut = new RegisterOfficeUseCase(repo, new UnitOfWorkFake(), Clock);

        var dto = await sut.ExecuteAsync(
            "1234567890", "つむぎ", ServiceCategory.TypeB, RegionGrade.Grade4,
            postalCode: null, address: null, phoneNumber: null,
            representativeTitleAndName: null, actor: "tester", ct: default);

        repo.Stored.Should().NotBeNull();
        repo.Stored!.PostalCode.Should().BeNull();
        repo.Stored.Address.Should().BeNull();
        repo.Stored.PhoneNumber.Should().BeNull();
        repo.Stored.RepresentativeTitleAndName.Should().BeNull();
        dto.PostalCode.Should().BeNull();
    }

    [Theory]
    [InlineData("postalCode")]
    [InlineData("address")]
    [InlineData("phoneNumber")]
    [InlineData("representative")]
    public async Task Register_office_rejects_blank_optional_claim_input(string field)
    {
        var sut = new RegisterOfficeUseCase(new OfficeRepositoryFake(), new UnitOfWorkFake(), Clock);

        var act = () => sut.ExecuteAsync(
            "1234567890", "つむぎ", ServiceCategory.TypeB, RegionGrade.Grade4,
            postalCode: field == "postalCode" ? " " : null,
            address: field == "address" ? " " : null,
            phoneNumber: field == "phoneNumber" ? " " : null,
            representativeTitleAndName: field == "representative" ? " " : null,
            actor: "tester", ct: default);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Theory]
    [InlineData("postalCode", 17)]
    [InlineData("address", 257)]
    [InlineData("phoneNumber", 33)]
    [InlineData("representative", 129)]
    public async Task Update_office_rejects_claim_input_over_max_length(string field, int length)
    {
        var current = Office.Create(
            Guid.NewGuid(), "1234567890", "つむぎ", ServiceCategory.TypeB,
            RegionGrade.Grade4, "seed", DateTimeOffset.UnixEpoch, Guid.NewGuid());
        var repo = new OfficeRepositoryFake(current);
        var sut = new UpdateOfficeUseCase(
            repo, new UnitOfWorkFake(), Clock, new Tsumugi.Application.Tests.NoopAuditTrail());
        var overLimit = new string('x', length);

        var act = () => sut.ExecuteAsync(
            current.Id, current.ConcurrencyToken, "つむぎ", ServiceCategory.TypeB,
            RegionGrade.Grade4,
            postalCode: field == "postalCode" ? overLimit : null,
            address: field == "address" ? overLimit : null,
            phoneNumber: field == "phoneNumber" ? overLimit : null,
            representativeTitleAndName: field == "representative" ? overLimit : null,
            actor: "tester", ct: default);

        var exception = await act.Should().ThrowAsync<ArgumentException>();
        exception.Which.Message.Should().NotContain(overLimit);
    }

    [Fact]
    public async Task Legacy_office_update_preserves_existing_claim_inputs()
    {
        var current = Office.Create(
            Guid.NewGuid(), "1234567890", "つむぎ", ServiceCategory.TypeB,
            RegionGrade.Grade4, "seed", DateTimeOffset.UnixEpoch, Guid.NewGuid(),
            postalCode: "100-0001", address: "東京都千代田区1-1",
            phoneNumber: "03-1234-5678", representativeTitleAndName: "代表 山田太郎");
        var repo = new OfficeRepositoryFake(current);
        var sut = new UpdateOfficeUseCase(
            repo, new UnitOfWorkFake(), Clock, new Tsumugi.Application.Tests.NoopAuditTrail());

        await sut.ExecuteAsync(
            current.Id, current.ConcurrencyToken, "新名称", ServiceCategory.TypeB,
            RegionGrade.Grade4, actor: "tester", ct: default);

        repo.Stored!.PostalCode.Should().Be("100-0001");
        repo.Stored.Address.Should().Be("東京都千代田区1-1");
        repo.Stored.PhoneNumber.Should().Be("03-1234-5678");
        repo.Stored.RepresentativeTitleAndName.Should().Be("代表 山田太郎");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(99)]
    public async Task Register_provider_accepts_certificate_entry_boundaries(int entryNumber)
    {
        var repo = new ContractedProviderRepositoryFake();
        var sut = new RegisterContractedProviderUseCase(repo, new UnitOfWorkFake(), Clock);

        var dto = await sut.ExecuteAsync(
            Guid.NewGuid(), "1234567890", "つむぎ", "就労継続支援B型", 22,
            ContractDate, terminationDate: null, notes: null,
            certificateEntryNumber: entryNumber, actor: "tester", ct: default);

        repo.Stored.Should().NotBeNull();
        repo.Stored!.CertificateEntryNumber.Should().Be(entryNumber);
        dto.CertificateEntryNumber.Should().Be(entryNumber);
    }

    [Fact]
    public async Task Register_provider_persists_null_certificate_entry_number()
    {
        var repo = new ContractedProviderRepositoryFake();
        var sut = new RegisterContractedProviderUseCase(repo, new UnitOfWorkFake(), Clock);

        await sut.ExecuteAsync(
            Guid.NewGuid(), "1234567890", "つむぎ", "就労継続支援B型", 22,
            ContractDate, terminationDate: null, notes: null,
            certificateEntryNumber: null, actor: "tester", ct: default);

        repo.Stored!.CertificateEntryNumber.Should().BeNull();
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(100)]
    public async Task Register_provider_rejects_certificate_entry_outside_official_range(int entryNumber)
    {
        var sut = new RegisterContractedProviderUseCase(
            new ContractedProviderRepositoryFake(), new UnitOfWorkFake(), Clock);

        var act = () => sut.ExecuteAsync(
            Guid.NewGuid(), "1234567890", "つむぎ", "就労継続支援B型", 22,
            ContractDate, terminationDate: null, notes: null,
            certificateEntryNumber: entryNumber, actor: "tester", ct: default);

        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }

    [Fact]
    public async Task Update_provider_persists_certificate_entry_number_when_token_matches()
    {
        var current = Provider(Guid.NewGuid());
        var repo = new ContractedProviderRepositoryFake(current);
        var sut = new UpdateContractedProviderUseCase(repo, new UnitOfWorkFake());

        await sut.ExecuteAsync(
            current.Id, current.ConcurrencyToken, current.ProviderNumber, current.ProviderName,
            current.ServiceCategory, current.ContractedSupplyDays, current.ContractDate,
            current.TerminationDate, current.Notes, certificateEntryNumber: 0,
            actor: "tester", ct: default);

        repo.Stored!.CertificateEntryNumber.Should().Be(0);
    }

    [Fact]
    public async Task Update_provider_rejects_stale_concurrency_token()
    {
        var current = Provider(Guid.NewGuid());
        var repo = new ContractedProviderRepositoryFake(current);
        var sut = new UpdateContractedProviderUseCase(repo, new UnitOfWorkFake());

        var act = () => sut.ExecuteAsync(
            current.Id, Guid.NewGuid(), current.ProviderNumber, current.ProviderName,
            current.ServiceCategory, current.ContractedSupplyDays, current.ContractDate,
            current.TerminationDate, current.Notes, certificateEntryNumber: 99,
            actor: "tester", ct: default);

        await act.Should().ThrowAsync<Tsumugi.Application.OptimisticConcurrencyException>();
    }

    private static ContractedProvider Provider(Guid token) => ContractedProvider.Create(
        Guid.NewGuid(), Guid.NewGuid(), "1234567890", "つむぎ", "就労継続支援B型",
        22, ContractDate, "seed", DateTimeOffset.UnixEpoch, token);

    private sealed class OfficeRepositoryFake(Office? initial = null) : IOfficeRepository
    {
        public Office? Stored { get; private set; } = initial;

        public Task AddAsync(Office office, CancellationToken ct)
        {
            Stored = office;
            return Task.CompletedTask;
        }

        public Task<Office?> FindByIdAsync(Guid id, CancellationToken ct) =>
            Task.FromResult(Stored?.Id == id ? Stored : null);

        public Task<Office?> FindByNumberAsync(string officeNumber, CancellationToken ct) =>
            Task.FromResult(Stored?.OfficeNumber == officeNumber ? Stored : null);

        public Task UpdateAsync(Office office, CancellationToken ct)
        {
            Stored = office;
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<Office>> ListAsync(CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<Office>>(Stored is null ? [] : [Stored]);
    }

    private sealed class ContractedProviderRepositoryFake(ContractedProvider? initial = null)
        : IContractedProviderRepository
    {
        public ContractedProvider? Stored { get; private set; } = initial;

        public Task AddAsync(ContractedProvider provider, CancellationToken ct)
        {
            Stored = provider;
            return Task.CompletedTask;
        }

        public Task<ContractedProvider?> FindByIdAsync(Guid id, CancellationToken ct) =>
            Task.FromResult(Stored?.Id == id ? Stored : null);

        public Task UpdateAsync(ContractedProvider provider, CancellationToken ct)
        {
            Stored = provider;
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<ContractedProvider>> ListByCertificateAsync(
            Guid certificateId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<ContractedProvider>>(
                Stored?.CertificateId == certificateId ? [Stored] : []);
    }

    private sealed class UnitOfWorkFake : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken ct) => Task.FromResult(1);
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
