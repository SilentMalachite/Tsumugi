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
}

internal sealed class InMemoryRecipientRepo : IRecipientRepository
{
    private readonly List<Recipient> _list = new();
    public void Add(Recipient r) => _list.Add(r);
    public Task AddAsync(Recipient r, CancellationToken ct) { _list.Add(r); return Task.CompletedTask; }
    public Task<Recipient?> FindByIdAsync(Guid id, CancellationToken ct) =>
        Task.FromResult<Recipient?>(_list.FirstOrDefault(r => r.Id == id));
    public Task UpdateAsync(Recipient r, CancellationToken ct) => Task.CompletedTask;
    public Task<IReadOnlyList<Recipient>> ListAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<Recipient>>(_list);
}
