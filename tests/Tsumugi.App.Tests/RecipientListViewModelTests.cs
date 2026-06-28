using FluentAssertions;
using Tsumugi.App.ViewModels;
using Tsumugi.Application.Abstractions;
using Tsumugi.Application.UseCases.Recipient;
using Tsumugi.Domain.Entities;

namespace Tsumugi.App.Tests;

public sealed class RecipientListViewModelTests
{
    private static RecipientListViewModel BuildVm(InMemoryRecipientRepo repo)
    {
        var uow = new InMemoryUow();
        var clock = new FixedClock(new DateTimeOffset(2026, 6, 28, 0, 0, 0, TimeSpan.Zero));
        return new RecipientListViewModel(
            new ListRecipientsUseCase(repo),
            new ArchiveRecipientUseCase(repo, uow, clock, new NoopAuditTrail()),
            new RestoreRecipientUseCase(repo, uow, clock, new NoopAuditTrail()));
    }

    [Fact]
    public async Task LoadAsync_populates_items_from_use_case()
    {
        var repo = new InMemoryRecipientRepo();
        repo.Add(Recipient.Create(Guid.NewGuid(), "山田", "ヤマダ",
            new DateOnly(1990, 1, 1), "u", DateTimeOffset.UnixEpoch, Guid.NewGuid()));

        var vm = BuildVm(repo);
        await vm.LoadAsync();
        vm.Items.Should().ContainSingle(r => r.KanjiName == "山田");
    }

    [Fact]
    public void EditCommand_invokes_EditRequested_with_selected_dto()
    {
        // RecipientList から RecipientEdit へ橋渡しするためのフック。
        // MainWindow 側で LoadForEdit + タブ切替を購読する。
        var repo = new InMemoryRecipientRepo();
        var vm = BuildVm(repo);
        var captured = (Tsumugi.Application.Dtos.RecipientDto?)null;
        vm.EditRequested = dto => captured = dto;
        var item = TestRecipients.Make(Guid.NewGuid());
        vm.Selected = item;

        vm.EditCommand.Execute(null);

        captured.Should().Be(item);
    }

    [Fact]
    public async Task DeleteCommand_archives_selected_and_hides_from_default_list()
    {
        var repo = new InMemoryRecipientRepo();
        var token = Guid.NewGuid();
        var r = Recipient.Create(Guid.NewGuid(), "削除太郎", "サクジョタロウ",
            new DateOnly(1980, 5, 5), "u", DateTimeOffset.UnixEpoch, token);
        repo.Add(r);
        var vm = BuildVm(repo);
        await vm.LoadAsync();
        vm.Selected = vm.Items.Single();

        await vm.DeleteCommand.ExecuteAsync(null);

        vm.Items.Should().BeEmpty("削除後は既定の一覧から消える");
        repo.Added.Single().IsArchived.Should().BeTrue();
    }

    [Fact]
    public async Task IncludeArchived_toggle_shows_archived_and_RestoreCommand_unarchives()
    {
        var repo = new InMemoryRecipientRepo();
        var token = Guid.NewGuid();
        var r = Recipient.Create(Guid.NewGuid(), "復元花子", "フクゲンハナコ",
            new DateOnly(1985, 8, 8), "u", DateTimeOffset.UnixEpoch, token);
        repo.Add(r.Archive("u", DateTimeOffset.UnixEpoch.AddDays(1)));

        var vm = BuildVm(repo);
        await vm.LoadAsync();
        vm.Items.Should().BeEmpty("既定はアーカイブ済みを除外");

        vm.IncludeArchived = true;
        await Task.Yield();  // OnIncludeArchivedChanged の fire-and-forget LoadAsync を待つ
        await vm.LoadAsync();
        vm.Items.Should().ContainSingle(x => x.IsArchived);

        vm.Selected = vm.Items.Single();
        await vm.RestoreCommand.ExecuteAsync(null);

        repo.Added.Single().IsArchived.Should().BeFalse();
    }
}

internal sealed class InMemoryRecipientRepo : IRecipientRepository
{
    // テストから直接シード・検証できるように公開（既存テストの Add(r) は互換のため残す）。
    public List<Recipient> Added { get; } = new();
    public void Add(Recipient r) => Added.Add(r);
    public Task AddAsync(Recipient r, CancellationToken ct) { Added.Add(r); return Task.CompletedTask; }
    public Task<Recipient?> FindByIdAsync(Guid id, CancellationToken ct) =>
        Task.FromResult<Recipient?>(Added.FirstOrDefault(r => r.Id == id));
    public Task UpdateAsync(Recipient r, CancellationToken ct)
    {
        var idx = Added.FindIndex(x => x.Id == r.Id);
        if (idx >= 0) Added[idx] = r;
        return Task.CompletedTask;
    }
    public Task<IReadOnlyList<Recipient>> ListAsync(bool includeArchived, CancellationToken ct)
    {
        IEnumerable<Recipient> source = includeArchived ? Added : Added.Where(r => !r.IsArchived);
        return Task.FromResult<IReadOnlyList<Recipient>>(source.ToList());
    }
}
