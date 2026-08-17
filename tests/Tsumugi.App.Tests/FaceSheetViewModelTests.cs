using FluentAssertions;
using Tsumugi.App.ViewModels;
using Tsumugi.Application.Abstractions;
using Tsumugi.Application.Dtos;
using Tsumugi.Application.UseCases.Recipient;
using Tsumugi.Domain.Entities;
using Xunit;

namespace Tsumugi.App.Tests;

public sealed class FaceSheetViewModelTests
{
    private readonly InMemoryRecipientRepoForCertificate _recipients = new();
    private readonly InMemoryFaceSheetRepo _sheets = new();
    private readonly InMemoryUow _uow = new();
    private readonly MutableFaceSheetClock _clock = new(DateTimeOffset.UnixEpoch);

    private FaceSheetViewModel NewVm() => new(
        new ListRecipientsUseCase(_recipients),
        new GetLatestFaceSheetUseCase(_sheets),
        new SaveFaceSheetUseCase(_sheets, _uow, _clock),
        new QueryFaceSheetHistoryUseCase(_sheets));

    [Fact]
    public void New_view_model_exposes_history_and_selected_changes()
    {
        var sut = NewVm();
        sut.HistoryItems.Should().BeEmpty();
        sut.SelectedHistoryItem.Should().BeNull();
        sut.SelectedChanges.Should().BeEmpty();
    }

    [Theory]
    [InlineData("ReceivesDisabilityPension", "True", "あり")]
    [InlineData("ReceivesDisabilityPension", "False", "なし")]
    [InlineData("Address", "東京都", "東京都")]
    [InlineData("UnknownProperty", "値", "値")]
    public void Change_display_item_localizes_known_fields_and_preserves_values(
        string propertyName, string oldValue, string expectedOldValue)
    {
        var item = FaceSheetChangeDisplayItem.From(
            new FaceSheetChangeDto(propertyName, oldValue, "新値"));

        item.PropertyName.Should().Be(propertyName == "ReceivesDisabilityPension" ? "障害年金の受給" :
            propertyName == "Address" ? "住所" : propertyName);
        item.OldValue.Should().Be(expectedOldValue);
    }

    [Fact]
    public async Task Selecting_recipient_loads_history_and_previous_diff_for_latest()
    {
        var recipientId = Guid.NewGuid();
        _recipients.Add(Recipient.Create(
            recipientId, "氏名", "シメイ",
            new DateOnly(1990, 1, 1), "u", DateTimeOffset.UnixEpoch, Guid.NewGuid()));
        var oldest = FaceSheet.Create(
            Guid.NewGuid(), recipientId, "first", DateTimeOffset.UnixEpoch, Guid.NewGuid(),
            address: "旧住所", receivesDisabilityPension: false);
        var newest = FaceSheet.Create(
            Guid.NewGuid(), recipientId, "second", DateTimeOffset.UnixEpoch.AddHours(1), Guid.NewGuid(),
            address: "新住所", receivesDisabilityPension: true);
        _sheets.Add(oldest);
        _sheets.Add(newest);

        var sut = NewVm();
        await sut.InitializeAsync();
        sut.SelectedRecipient = sut.Recipients.Single();

        sut.HistoryItems.Should().HaveCount(2);
        sut.SelectedHistoryItem!.FaceSheet.Id.Should().Be(newest.Id);
        sut.SelectedChanges.Should().Contain(x =>
            x.PropertyName == "住所" && x.OldValue == "旧住所" && x.NewValue == "新住所");
        sut.SelectedChanges.Should().Contain(x =>
            x.PropertyName == "障害年金の受給" && x.OldValue == "なし" && x.NewValue == "あり");
    }

    [Fact]
    public async Task Selecting_oldest_history_shows_empty_changes()
    {
        var recipientId = Guid.NewGuid();
        _recipients.Add(Recipient.Create(
            recipientId, "氏名", "シメイ",
            new DateOnly(1990, 1, 1), "u", DateTimeOffset.UnixEpoch, Guid.NewGuid()));
        var oldest = FaceSheet.Create(
            Guid.NewGuid(), recipientId, "first", DateTimeOffset.UnixEpoch, Guid.NewGuid(),
            address: "旧住所");
        var newest = FaceSheet.Create(
            Guid.NewGuid(), recipientId, "second", DateTimeOffset.UnixEpoch.AddHours(1), Guid.NewGuid(),
            address: "新住所");
        _sheets.Add(oldest);
        _sheets.Add(newest);

        var sut = NewVm();
        await sut.InitializeAsync();
        sut.SelectedRecipient = sut.Recipients.Single();
        sut.SelectedHistoryItem = sut.HistoryItems.First();

        sut.SelectedHistoryItem.FaceSheet.Id.Should().Be(oldest.Id);
        sut.SelectedChanges.Should().BeEmpty();
    }

    [Fact]
    public async Task Save_reloads_history_selects_latest_and_keeps_IsSaved()
    {
        var recipientId = Guid.NewGuid();
        _recipients.Add(Recipient.Create(
            recipientId, "氏名", "シメイ",
            new DateOnly(1990, 1, 1), "u", DateTimeOffset.UnixEpoch, Guid.NewGuid()));
        _sheets.Add(FaceSheet.Create(
            Guid.NewGuid(), recipientId, "first", DateTimeOffset.UnixEpoch, Guid.NewGuid(),
            address: "旧住所"));

        var sut = NewVm();
        await sut.InitializeAsync();
        sut.SelectedRecipient = sut.Recipients.Single();
        sut.Address = "新住所";
        _clock.Advance(TimeSpan.FromHours(1));

        await sut.SaveCommand.ExecuteAsync(null);

        sut.IsSaved.Should().BeTrue();
        sut.HistoryItems.Should().HaveCount(2);
        sut.SelectedHistoryItem!.FaceSheet.Address.Should().Be("新住所");
        sut.SelectedChanges.Should().Contain(x =>
            x.PropertyName == "住所" && x.OldValue == "旧住所" && x.NewValue == "新住所");
    }

    [Fact]
    public async Task Switching_recipient_discards_stale_latest_and_history_results()
    {
        var recipientA = Recipient.Create(
            Guid.NewGuid(), "利用者A", "リヨウシャエー",
            new DateOnly(1990, 1, 1), "u", DateTimeOffset.UnixEpoch, Guid.NewGuid());
        var recipientB = Recipient.Create(
            Guid.NewGuid(), "利用者B", "リヨウシャビー",
            new DateOnly(1991, 1, 1), "u", DateTimeOffset.UnixEpoch, Guid.NewGuid());
        _recipients.Add(recipientA);
        _recipients.Add(recipientB);
        _sheets.Add(FaceSheet.Create(
            Guid.NewGuid(), recipientA.Id, "a", DateTimeOffset.UnixEpoch, Guid.NewGuid(),
            address: "Aの住所"));
        _sheets.Add(FaceSheet.Create(
            Guid.NewGuid(), recipientB.Id, "b", DateTimeOffset.UnixEpoch, Guid.NewGuid(),
            address: "Bの住所"));
        var releaseA = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var bHistoryLoaded = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _sheets.BeforeFindLatestByRecipientAsync = async (recipientId, _) =>
        {
            if (recipientId == recipientA.Id) await releaseA.Task;
        };
        _sheets.BeforeListByRecipientAsync = (recipientId, _) =>
        {
            if (recipientId == recipientB.Id) bHistoryLoaded.SetResult();
            return Task.CompletedTask;
        };

        var sut = NewVm();
        await sut.InitializeAsync();
        sut.SelectedRecipient = sut.Recipients.Single(recipient => recipient.Id == recipientA.Id);
        sut.SelectedRecipient = sut.Recipients.Single(recipient => recipient.Id == recipientB.Id);
        await bHistoryLoaded.Task;

        sut.Address.Should().Be("Bの住所");
        sut.HistoryItems.Should().ContainSingle()
            .Which.FaceSheet.RecipientId.Should().Be(recipientB.Id);
        sut.SelectedHistoryItem!.FaceSheet.RecipientId.Should().Be(recipientB.Id);

        releaseA.SetResult();
        await Task.Yield();
        await Task.Yield();

        sut.Address.Should().Be("Bの住所");
        sut.HistoryItems.Should().ContainSingle()
            .Which.FaceSheet.RecipientId.Should().Be(recipientB.Id);
        sut.SelectedHistoryItem!.FaceSheet.RecipientId.Should().Be(recipientB.Id);
        sut.SelectedChanges.Should().BeEmpty();
    }
}

internal sealed class InMemoryFaceSheetRepo : IFaceSheetRepository
{
    private readonly List<FaceSheet> _list = [];
    public Func<Guid, CancellationToken, Task>? BeforeFindLatestByRecipientAsync { get; set; }
    public Func<Guid, CancellationToken, Task>? BeforeListByRecipientAsync { get; set; }

    public void Add(FaceSheet sheet) => _list.Add(sheet);
    public Task AddAsync(FaceSheet faceSheet, CancellationToken ct)
    {
        _list.Add(faceSheet);
        return Task.CompletedTask;
    }

    public async Task<FaceSheet?> FindLatestByRecipientAsync(Guid recipientId, CancellationToken ct)
    {
        if (BeforeFindLatestByRecipientAsync is not null)
            await BeforeFindLatestByRecipientAsync(recipientId, ct);

        return _list.Where(sheet => sheet.RecipientId == recipientId)
            .OrderByDescending(sheet => sheet.CreatedAt)
            .FirstOrDefault();
    }

    public async Task<IReadOnlyList<FaceSheet>> ListByRecipientAsync(Guid recipientId, CancellationToken ct)
    {
        if (BeforeListByRecipientAsync is not null)
            await BeforeListByRecipientAsync(recipientId, ct);

        return _list.Where(sheet => sheet.RecipientId == recipientId)
            .OrderBy(sheet => sheet.CreatedAt)
            .ToArray();
    }
}

internal sealed class MutableFaceSheetClock(DateTimeOffset start) : TimeProvider
{
    private DateTimeOffset _now = start;
    public void Advance(TimeSpan delta) => _now = _now.Add(delta);
    public override DateTimeOffset GetUtcNow() => _now;
}
