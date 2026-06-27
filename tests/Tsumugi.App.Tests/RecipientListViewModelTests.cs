using FluentAssertions;
using Tsumugi.App.ViewModels;
using Tsumugi.Application.Abstractions;
using Tsumugi.Application.UseCases.Recipient;
using Tsumugi.Domain.Entities;

namespace Tsumugi.App.Tests;

public sealed class RecipientListViewModelTests
{
    [Fact]
    public async Task LoadAsync_populates_items_from_use_case()
    {
        var repo = new InMemoryRecipientRepo();
        repo.Add(Recipient.Create(Guid.NewGuid(), "山田", "ヤマダ",
            new DateOnly(1990, 1, 1), "u", DateTimeOffset.UnixEpoch, Guid.NewGuid()));

        var vm = new RecipientListViewModel(new ListRecipientsUseCase(repo));
        await vm.LoadAsync();
        vm.Items.Should().ContainSingle(r => r.KanjiName == "山田");
    }

    [Fact]
    public void EditCommand_invokes_EditRequested_with_selected_dto()
    {
        // RecipientList から RecipientEdit へ橋渡しするためのフック。
        // MainWindow 側で LoadForEdit + タブ切替を購読する。
        var repo = new InMemoryRecipientRepo();
        var vm = new RecipientListViewModel(new ListRecipientsUseCase(repo));
        var captured = (Tsumugi.Application.Dtos.RecipientDto?)null;
        vm.EditRequested = dto => captured = dto;
        var item = new Tsumugi.Application.Dtos.RecipientDto(
            Guid.NewGuid(), "山田", "ヤマダ", new DateOnly(1990, 1, 1));
        vm.Selected = item;

        vm.EditCommand.Execute(null);

        captured.Should().Be(item);
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
    public Task<IReadOnlyList<Recipient>> ListAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<Recipient>>(Added);
}
