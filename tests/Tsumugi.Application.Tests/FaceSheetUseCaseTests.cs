using FluentAssertions;
using Tsumugi.Application.Abstractions;
using Tsumugi.Application.UseCases.Recipient;
using Tsumugi.Domain.Entities;
using Xunit;

namespace Tsumugi.Application.Tests;

public sealed class FaceSheetUseCaseTests
{
    [Fact]
    public async Task GetLatest_returns_null_when_no_record_exists()
    {
        var repo = new FakeFaceSheetRepository();
        var sut = new GetLatestFaceSheetUseCase(repo);
        var result = await sut.ExecuteAsync(Guid.NewGuid(), default);
        result.Should().BeNull();
    }

    [Fact]
    public async Task Save_appends_new_record_each_time_and_latest_is_returned()
    {
        var repo = new FakeFaceSheetRepository();
        var rid = Guid.NewGuid();
        var clock = new MutableClock(new DateTimeOffset(2026, 6, 28, 0, 0, 0, TimeSpan.Zero));
        var save = new SaveFaceSheetUseCase(repo, new FakeUnitOfWork(), clock);
        var get = new GetLatestFaceSheetUseCase(repo);

        await save.ExecuteAsync(new SaveFaceSheetInput(rid) { PhoneNumber = "111-0000" }, "u", default);
        clock.Advance(TimeSpan.FromHours(1));
        await save.ExecuteAsync(new SaveFaceSheetInput(rid)
        {
            PhoneNumber = "222-0000",
            PrimaryDoctorName = "山田医師",
        }, "u", default);

        repo.Added.Should().HaveCount(2, "保存ごとに新バージョンが追記される");
        var latest = await get.ExecuteAsync(rid, default);
        latest.Should().NotBeNull();
        latest!.PhoneNumber.Should().Be("222-0000");
        latest.PrimaryDoctorName.Should().Be("山田医師");
    }

    [Fact]
    public async Task Save_rejects_empty_recipient()
    {
        var sut = new SaveFaceSheetUseCase(
            new FakeFaceSheetRepository(), new FakeUnitOfWork(),
            new FixedTimeProvider(DateTimeOffset.UnixEpoch));
        var act = () => sut.ExecuteAsync(new SaveFaceSheetInput(Guid.Empty), "u", default);
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task History_query_returns_empty_changes_for_oldest_version()
    {
        var recipientId = Guid.NewGuid();
        var repo = new FakeFaceSheetRepository();
        var oldest = FaceSheet.Create(
            Guid.NewGuid(), recipientId, "first", DateTimeOffset.UnixEpoch, Guid.NewGuid(),
            phoneNumber: "111");
        var newest = FaceSheet.Create(
            Guid.NewGuid(), recipientId, "second", DateTimeOffset.UnixEpoch.AddHours(1), Guid.NewGuid(),
            phoneNumber: "222");
        repo.Added.Add(newest);
        repo.Added.Add(oldest);
        var sut = new QueryFaceSheetHistoryUseCase(repo);

        var result = await sut.ExecuteAsync(recipientId, default);

        result.Should().HaveCount(2);
        result[0].FaceSheet.Id.Should().Be(oldest.Id);
        result[0].FaceSheet.CreatedAt.Should().Be(oldest.CreatedAt);
        result[0].FaceSheet.CreatedBy.Should().Be("first");
        result[0].ChangesFromPrevious.Should().BeEmpty();
        result[1].FaceSheet.Id.Should().Be(newest.Id);
        result[1].ChangesFromPrevious.Should().ContainSingle()
            .Which.Should().BeEquivalentTo(new
            {
                PropertyName = "PhoneNumber",
                OldValue = "111",
                NewValue = "222",
            });
    }
}

internal sealed class FakeFaceSheetRepository : IFaceSheetRepository
{
    public List<FaceSheet> Added { get; } = new();
    public Task AddAsync(FaceSheet faceSheet, CancellationToken ct)
    { Added.Add(faceSheet); return Task.CompletedTask; }
    public Task<FaceSheet?> FindLatestByRecipientAsync(Guid recipientId, CancellationToken ct) =>
        Task.FromResult(Added.Where(f => f.RecipientId == recipientId)
            .OrderByDescending(f => f.CreatedAt).FirstOrDefault());

    public Task<IReadOnlyList<FaceSheet>> ListByRecipientAsync(Guid recipientId, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<FaceSheet>>(
            Added.Where(f => f.RecipientId == recipientId)
                .OrderBy(f => f.CreatedAt)
                .ToArray());
}

internal sealed class MutableClock(DateTimeOffset start) : TimeProvider
{
    private DateTimeOffset _now = start;
    public void Advance(TimeSpan delta) => _now = _now.Add(delta);
    public override DateTimeOffset GetUtcNow() => _now;
}
