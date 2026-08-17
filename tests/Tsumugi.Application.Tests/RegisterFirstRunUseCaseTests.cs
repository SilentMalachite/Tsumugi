using FluentAssertions;
using Tsumugi.Application.Abstractions;
using Tsumugi.Application.Dtos;
using Tsumugi.Application.UseCases;
using Tsumugi.Application.UseCases.Office;
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.Enums;
using Xunit;

namespace Tsumugi.Application.Tests;

public sealed class RegisterFirstRunUseCaseTests
{
    private sealed class FakeOfficeRepository : IOfficeRepository
    {
        public Office? Added { get; private set; }
        public Office? Existing { get; init; }
        public int FindByNumberCalls { get; private set; }
        public int AddCalls { get; private set; }
        public CancellationToken LastFindToken { get; private set; }
        public CancellationToken LastAddToken { get; private set; }

        public Task AddAsync(Office office, CancellationToken ct)
        {
            AddCalls++;
            LastAddToken = ct;
            Added = office;
            return Task.CompletedTask;
        }

        public Task<Office?> FindByIdAsync(Guid id, CancellationToken ct) =>
            Task.FromResult(Existing?.Id == id ? Existing : null);

        public Task<Office?> FindByNumberAsync(string n, CancellationToken ct)
        {
            FindByNumberCalls++;
            LastFindToken = ct;
            return Task.FromResult(Existing?.OfficeNumber == n ? Existing : null);
        }

        public Task UpdateAsync(Office office, CancellationToken ct) => Task.CompletedTask;

        public Task<IReadOnlyList<Office>> ListAsync(CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<Office>>(
                Existing is null ? Array.Empty<Office>() : new[] { Existing });
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public int SaveCalls { get; private set; }
        public CancellationToken LastToken { get; private set; }

        public Task<int> SaveChangesAsync(CancellationToken ct)
        {
            SaveCalls++;
            LastToken = ct;
            return Task.FromResult(1);
        }
    }

    private static readonly TimeProvider Clock =
        new FixedClock(new DateTimeOffset(2026, 4, 1, 0, 0, 0, TimeSpan.Zero));

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private static RegisterFirstRunUseCase CreateSut(
        FakeOfficeRepository repo, FakeUnitOfWork uow)
        => new(new RegisterOfficeUseCase(repo, uow, Clock));

    private static RegisterFirstRunInput ValidInput(
        RegionGrade region = RegionGrade.Grade4,
        string? postalCode = null,
        string? address = null,
        string? phoneNumber = null,
        string? representativeTitleAndName = null)
        => new(
            "1234567890",
            "つむぎ作業所",
            ServiceCategory.TypeB,
            region,
            postalCode,
            address,
            phoneNumber,
            representativeTitleAndName);

    [Fact]
    public async Task Execute_persists_optional_address_contact_and_administrator()
    {
        var repo = new FakeOfficeRepository();
        var uow = new FakeUnitOfWork();
        var sut = CreateSut(repo, uow);
        var input = ValidInput(
            postalCode: "100-0001",
            address: "東京都千代田区1-1",
            phoneNumber: "03-1234-5678",
            representativeTitleAndName: "管理者 山田太郎");

        var dto = await sut.ExecuteAsync(input, "tester", CancellationToken.None);

        repo.Added.Should().NotBeNull();
        repo.Added!.PostalCode.Should().Be("100-0001");
        repo.Added.Address.Should().Be("東京都千代田区1-1");
        repo.Added.PhoneNumber.Should().Be("03-1234-5678");
        repo.Added.RepresentativeTitleAndName.Should().Be("管理者 山田太郎");
        repo.Added.CreatedBy.Should().Be("tester");
        dto.PostalCode.Should().Be("100-0001");
        dto.Address.Should().Be("東京都千代田区1-1");
        dto.PhoneNumber.Should().Be("03-1234-5678");
        dto.RepresentativeTitleAndName.Should().Be("管理者 山田太郎");
        repo.AddCalls.Should().Be(1);
        uow.SaveCalls.Should().Be(1);
    }

    [Fact]
    public async Task Execute_rejects_region_none_before_repository_lookup()
    {
        var repo = new FakeOfficeRepository();
        var uow = new FakeUnitOfWork();
        var sut = CreateSut(repo, uow);

        var act = () => sut.ExecuteAsync(
            ValidInput(RegionGrade.None), "tester", CancellationToken.None);

        var ex = await act.Should().ThrowAsync<ArgumentException>();
        ex.Which.Message.Should().Contain("地域区分");
        repo.FindByNumberCalls.Should().Be(0);
        repo.AddCalls.Should().Be(0);
        uow.SaveCalls.Should().Be(0);
    }

    [Theory]
    [InlineData("", "つむぎ作業所")]
    [InlineData("1234567890", "")]
    public async Task Execute_delegates_blank_required_validation(string number, string name)
    {
        var sut = CreateSut(new FakeOfficeRepository(), new FakeUnitOfWork());
        var input = new RegisterFirstRunInput(
            number, name, ServiceCategory.TypeB, RegionGrade.Grade4);

        var act = () => sut.ExecuteAsync(input, "tester", CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task Execute_delegates_duplicate_office_number()
    {
        var existing = Office.Create(
            Guid.NewGuid(), "1234567890", "既存", ServiceCategory.TypeB,
            RegionGrade.Grade4, "u", DateTimeOffset.UnixEpoch, Guid.NewGuid());
        var repo = new FakeOfficeRepository { Existing = existing };
        var sut = CreateSut(repo, new FakeUnitOfWork());

        var act = () => sut.ExecuteAsync(ValidInput(), "tester", CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
        repo.FindByNumberCalls.Should().Be(1);
        repo.AddCalls.Should().Be(0);
    }

    [Theory]
    [InlineData("postalCode")]
    [InlineData("address")]
    [InlineData("phoneNumber")]
    [InlineData("representative")]
    public async Task Execute_delegates_blank_optional_validation(string field)
    {
        var sut = CreateSut(new FakeOfficeRepository(), new FakeUnitOfWork());
        var input = ValidInput(
            postalCode: field == "postalCode" ? " " : null,
            address: field == "address" ? " " : null,
            phoneNumber: field == "phoneNumber" ? " " : null,
            representativeTitleAndName: field == "representative" ? " " : null);

        var act = () => sut.ExecuteAsync(input, "tester", CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task Execute_forwards_actor_and_cancellation_token_and_saves_once()
    {
        var repo = new FakeOfficeRepository();
        var uow = new FakeUnitOfWork();
        var sut = CreateSut(repo, uow);
        using var cts = new CancellationTokenSource();
        var token = cts.Token;

        var dto = await sut.ExecuteAsync(ValidInput(), "first-run-actor", token);

        dto.OfficeNumber.Should().Be("1234567890");
        repo.Added.Should().NotBeNull();
        repo.Added!.CreatedBy.Should().Be("first-run-actor");
        repo.LastFindToken.Should().Be(token);
        repo.LastAddToken.Should().Be(token);
        uow.LastToken.Should().Be(token);
        repo.AddCalls.Should().Be(1);
        uow.SaveCalls.Should().Be(1);
    }
}
