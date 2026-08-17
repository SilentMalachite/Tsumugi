using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Tsumugi.Application.Abstractions;
using Tsumugi.Application.UseCases;
using Tsumugi.Application.UseCases.Office;
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
        public Office? Updated { get; private set; }
        public Task AddAsync(Office office, CancellationToken ct) { Added = office; return Task.CompletedTask; }
        public Task<Office?> FindByIdAsync(Guid id, CancellationToken ct) =>
            Task.FromResult(Existing?.Id == id ? Existing : null);
        public Task<Office?> FindByNumberAsync(string n, CancellationToken ct) =>
            Task.FromResult(Existing?.OfficeNumber == n ? Existing : null);
        public Task UpdateAsync(Office office, CancellationToken ct) { Updated = office; return Task.CompletedTask; }
        public Task<IReadOnlyList<Office>> ListAsync(CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<Office>>(Existing is null ? Array.Empty<Office>() : new[] { Existing });
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

        var dto = await sut.ExecuteAsync("1234567890", "つむぎ作業所", ServiceCategory.TypeB, RegionGrade.Grade4, "tester", CancellationToken.None);

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

        var act = () => sut.ExecuteAsync("1234567890", "別名", ServiceCategory.TypeB, RegionGrade.Grade4, "tester", CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Theory]
    [InlineData("", "name")]
    [InlineData("123", "")]
    public async Task Execute_rejects_blank_input(string number, string name)
    {
        var sut = new RegisterOfficeUseCase(new FakeOfficeRepository(), new FakeUnitOfWork(), Clock);

        var act = () => sut.ExecuteAsync(number, name, ServiceCategory.TypeB, RegionGrade.Grade4, "tester", CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task Update_rejects_empty_id_before_db_lookup()
    {
        var sut = new UpdateOfficeUseCase(new FakeOfficeRepository(), new FakeUnitOfWork(),
            new FixedTimeProvider(DateTimeOffset.UnixEpoch), new NoopAuditTrail());
        Func<Task> act = () => sut.ExecuteAsync(
            Guid.Empty, expectedConcurrencyToken: Guid.NewGuid(),
            "名前", ServiceCategory.TypeB, RegionGrade.None, "tester", CancellationToken.None);
        await act.Should().ThrowAsync<ArgumentException>()
            .Where(e => e.ParamName == "id");
    }

    [Fact]
    public async Task Update_throws_OptimisticConcurrencyException_when_expected_token_does_not_match_current()
    {
        var currentToken = Guid.NewGuid();
        var existing = Office.Create(Guid.NewGuid(), "1234567890", "名前",
            ServiceCategory.TypeB, RegionGrade.None, "u", DateTimeOffset.UnixEpoch, currentToken);
        var repo = new FakeOfficeRepository { Existing = existing };
        var sut = new UpdateOfficeUseCase(repo, new FakeUnitOfWork(),
            new FixedTimeProvider(DateTimeOffset.UnixEpoch), new NoopAuditTrail());

        // 画面が開いた時点の古いトークン（別ユーザが先に保存して回転している想定）。
        var staleToken = Guid.NewGuid();
        Func<Task> act = () => sut.ExecuteAsync(
            existing.Id, expectedConcurrencyToken: staleToken,
            "新名", ServiceCategory.TypeB, RegionGrade.Grade2, "tester", CancellationToken.None);

        await act.Should().ThrowAsync<Tsumugi.Application.OptimisticConcurrencyException>();
    }

    [Theory]
    [InlineData("", "名称", "事業所番号は必須です。")]
    [InlineData("1234567890", "", "事業所名は必須です。")]
    public async Task Register_validation_message_carries_no_parameter_name_decoration(
        string officeNumber, string name, string expected)
    {
        // ViewModel は ex.Message をそのまま画面に出す。ArgumentException(message, paramName) の
        // Message には " (Parameter 'officeNumber')" が付き、職員に英語のデバッグ装飾が見える。
        var sut = new RegisterOfficeUseCase(
            new FakeOfficeRepository(), new FakeUnitOfWork(), Clock);

        var act = () => sut.ExecuteAsync(
            officeNumber, name, ServiceCategory.TypeB, RegionGrade.Grade4,
            "tester", CancellationToken.None);

        var ex = (await act.Should().ThrowAsync<ArgumentException>()).Which;
        ex.Message.Should().Be(expected);
    }

    [Fact]
    public async Task Register_optional_field_validation_message_carries_no_parameter_name_decoration()
    {
        var sut = new RegisterOfficeUseCase(
            new FakeOfficeRepository(), new FakeUnitOfWork(), Clock);

        var act = () => sut.ExecuteAsync(
            "1234567890", "名称", ServiceCategory.TypeB, RegionGrade.Grade4,
            postalCode: "   ", address: null, phoneNumber: null,
            representativeTitleAndName: null, "tester", CancellationToken.None);

        var ex = (await act.Should().ThrowAsync<ArgumentException>()).Which;
        ex.Message.Should().Be("郵便番号は空白以外を指定してください。");
    }

    [Fact]
    public async Task Update_validation_message_carries_no_parameter_name_decoration()
    {
        var token = Guid.NewGuid();
        var existing = Office.Create(Guid.NewGuid(), "1234567890", "旧名",
            ServiceCategory.TypeB, RegionGrade.Grade3, "u", DateTimeOffset.UnixEpoch, token);
        var repo = new FakeOfficeRepository { Existing = existing };
        var sut = new UpdateOfficeUseCase(repo, new FakeUnitOfWork(),
            new FixedTimeProvider(DateTimeOffset.UnixEpoch), new NoopAuditTrail());

        var act = () => sut.ExecuteAsync(
            existing.Id, expectedConcurrencyToken: token,
            "", ServiceCategory.TypeB, RegionGrade.Grade3, "tester", CancellationToken.None);

        var ex = (await act.Should().ThrowAsync<ArgumentException>()).Which;
        ex.Message.Should().Be("事業所名は必須です。");
    }

    [Fact]
    public async Task Register_rejects_region_none()
    {
        // 地域区分単価は報酬算定に直結する。初回ウィザードだけで弾いても、
        // 事業所管理画面から同じ不正状態へ戻せてしまう。
        var sut = new RegisterOfficeUseCase(
            new FakeOfficeRepository(), new FakeUnitOfWork(), Clock);

        var act = () => sut.ExecuteAsync(
            "1234567890", "名称", ServiceCategory.TypeB, RegionGrade.None,
            "tester", CancellationToken.None);

        var ex = (await act.Should().ThrowAsync<ArgumentException>()).Which;
        ex.Message.Should().Be("地域区分を選択してください。");
    }

    [Fact]
    public async Task Update_rejects_region_none()
    {
        var token = Guid.NewGuid();
        var existing = Office.Create(Guid.NewGuid(), "1234567890", "旧名",
            ServiceCategory.TypeB, RegionGrade.Grade3, "u", DateTimeOffset.UnixEpoch, token);
        var repo = new FakeOfficeRepository { Existing = existing };
        var sut = new UpdateOfficeUseCase(repo, new FakeUnitOfWork(),
            new FixedTimeProvider(DateTimeOffset.UnixEpoch), new NoopAuditTrail());

        var act = () => sut.ExecuteAsync(
            existing.Id, expectedConcurrencyToken: token,
            "新名", ServiceCategory.TypeB, RegionGrade.None, "tester", CancellationToken.None);

        var ex = (await act.Should().ThrowAsync<ArgumentException>()).Which;
        ex.Message.Should().Be("地域区分を選択してください。");
        repo.Updated.Should().BeNull();
    }

    [Fact]
    public async Task Update_succeeds_when_expected_token_matches_current()
    {
        var token = Guid.NewGuid();
        var existing = Office.Create(Guid.NewGuid(), "1234567890", "旧名",
            ServiceCategory.TypeB, RegionGrade.None, "u", DateTimeOffset.UnixEpoch, token);
        var repo = new FakeOfficeRepository { Existing = existing };
        var sut = new UpdateOfficeUseCase(repo, new FakeUnitOfWork(),
            new FixedTimeProvider(DateTimeOffset.UnixEpoch), new NoopAuditTrail());

        await sut.ExecuteAsync(
            existing.Id, expectedConcurrencyToken: token,
            "新名", ServiceCategory.TypeB, RegionGrade.Grade3, "tester", CancellationToken.None);

        // Update が呼ばれていれば fake が保持する Updated 値が変わる。
        repo.Updated.Should().NotBeNull();
        repo.Updated!.Name.Should().Be("新名");
        repo.Updated.RegionGrade.Should().Be(RegionGrade.Grade3);
    }
}
