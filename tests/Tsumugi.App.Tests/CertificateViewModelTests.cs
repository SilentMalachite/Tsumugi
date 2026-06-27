using FluentAssertions;
using Tsumugi.App.ViewModels;
using Tsumugi.Application.UseCases.Certificate;
using Tsumugi.Application.UseCases.Recipient;
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.ValueObjects;
using Xunit;

namespace Tsumugi.App.Tests;

public sealed class CertificateViewModelTests
{
    private readonly InMemoryCertRepo _certs = new();
    private readonly InMemoryRecipientRepoForCertificate _recipients = new();
    private readonly InMemoryUow _uow = new();
    private readonly FixedClock _clock = new(DateTimeOffset.UnixEpoch);

    private CertificateViewModel NewVm() => new(
        new ListExpiringCertificatesUseCase(_certs),
        new RegisterCertificateUseCase(_certs, _uow, _clock),
        new ListRecipientsUseCase(_recipients));

    [Fact]
    public async Task LoadAsync_populates_expiring_items()
    {
        _certs.Add(Certificate.Create(Guid.NewGuid(), Guid.NewGuid(), "near",
            new DateRange(new DateOnly(2026, 1, 1), new DateOnly(2026, 7, 1)),
            0, 0, "x", "u", DateTimeOffset.UnixEpoch, Guid.NewGuid()));

        var sut = NewVm();
        await sut.LoadAsync(new DateOnly(2026, 6, 27), thresholdDays: 30);

        sut.ExpiringItems.Should().ContainSingle();
        sut.ExpiringItems[0].RemainingDays.Should().Be(4);
    }

    [Fact]
    public async Task InitializeAsync_loads_recipients_for_selection()
    {
        var r = Recipient.Create(Guid.NewGuid(), "氏名", "シメイ",
            new DateOnly(1990, 1, 1), "u", DateTimeOffset.UnixEpoch, Guid.NewGuid());
        _recipients.Add(r);

        var vm = NewVm();
        await vm.InitializeAsync();

        vm.Recipients.Should().ContainSingle(x => x.Id == r.Id);
    }

    [Fact]
    public async Task SaveCommand_registers_certificate_for_selected_recipient()
    {
        var rid = Guid.NewGuid();
        var vm = NewVm();
        vm.SelectedRecipient = new Tsumugi.Application.Dtos.RecipientDto(
            rid, "氏名", "シメイ", new DateOnly(1990, 1, 1), Guid.NewGuid(), IsArchived: false);
        vm.CertificateNumber = "1234567890";
        vm.ValidityStart = new DateOnly(2026, 4, 1);
        vm.ValidityEnd = new DateOnly(2027, 3, 31);
        vm.SupplyDays = 23;
        vm.MonthlyCostCap = 9300;
        vm.Municipality = "杉並区";

        await vm.SaveCommand.ExecuteAsync(null);

        vm.SaveErrorMessage.Should().BeNull();
        vm.IsSaved.Should().BeTrue();
        _certs.Count.Should().Be(1);
    }

    [Fact]
    public async Task SaveCommand_without_selected_recipient_sets_error()
    {
        var vm = NewVm();
        vm.CertificateNumber = "1234567890";
        vm.ValidityStart = new DateOnly(2026, 4, 1);
        vm.ValidityEnd = new DateOnly(2027, 3, 31);

        await vm.SaveCommand.ExecuteAsync(null);

        vm.SaveErrorMessage.Should().NotBeNullOrEmpty();
        vm.IsSaved.Should().BeFalse();
        _certs.Count.Should().Be(0);
    }
}

internal sealed class InMemoryRecipientRepoForCertificate : Tsumugi.Application.Abstractions.IRecipientRepository
{
    private readonly List<Recipient> _list = [];
    public void Add(Recipient r) => _list.Add(r);
    public Task AddAsync(Recipient r, CancellationToken ct) { _list.Add(r); return Task.CompletedTask; }
    public Task<Recipient?> FindByIdAsync(Guid id, CancellationToken ct) =>
        Task.FromResult(_list.FirstOrDefault(r => r.Id == id));
    public Task UpdateAsync(Recipient r, CancellationToken ct) => Task.CompletedTask;
    public Task<IReadOnlyList<Recipient>> ListAsync(bool includeArchived, CancellationToken ct)
    {
        IEnumerable<Recipient> source = includeArchived ? _list : _list.Where(r => !r.IsArchived);
        return Task.FromResult<IReadOnlyList<Recipient>>(source.ToArray());
    }
}

internal sealed class InMemoryCertRepo : Tsumugi.Application.Abstractions.ICertificateRepository
{
    private readonly List<Certificate> _list = new();
    public int Count => _list.Count;
    public void Add(Certificate c) => _list.Add(c);
    public Task AddAsync(Certificate c, CancellationToken ct) { _list.Add(c); return Task.CompletedTask; }
    public Task<IReadOnlyList<Certificate>> ListByRecipientAsync(Guid rid, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<Certificate>>(_list.Where(c => c.RecipientId == rid).ToArray());
    public Task<IReadOnlyList<Certificate>> ListAllAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<Certificate>>(_list);
    public Task<Certificate?> FindEffectiveAsync(Guid rid, DateOnly asOf, CancellationToken ct) =>
        Task.FromResult<Certificate?>(null);
}
