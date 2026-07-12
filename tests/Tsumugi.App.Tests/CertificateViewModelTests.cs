using FluentAssertions;
using Tsumugi.App.ViewModels;
using Tsumugi.Application.Abstractions;
using Tsumugi.Application.UseCases.Certificate;
using Tsumugi.Application.UseCases.Recipient;
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.Enums;
using Tsumugi.Domain.ValueObjects;
using Xunit;

namespace Tsumugi.App.Tests;

public sealed class CertificateViewModelTests
{
    private readonly InMemoryCertRepo _certs = new();
    private readonly InMemoryRecipientRepoForCertificate _recipients = new();
    private readonly InMemoryContractedProviderRepo _providers = new();
    private readonly InMemoryUow _uow = new();
    private readonly FixedClock _clock = new(DateTimeOffset.UnixEpoch);

    private CertificateViewModel NewVm() => new(
        new ListExpiringCertificatesUseCase(_certs),
        new RegisterCertificateUseCase(_certs, _uow, _clock),
        new ListRecipientsUseCase(_recipients),
        new ListCertificatesByRecipientUseCase(_certs),
        new RegisterContractedProviderUseCase(_providers, _uow, _clock),
        new ListContractedProvidersUseCase(_providers));

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
    public async Task SaveCommand_registers_certificate_with_form_section_fields()
    {
        var rid = Guid.NewGuid();
        var vm = NewVm();
        vm.SelectedRecipient = TestRecipients.Make(rid, "氏名", "シメイ");
        vm.CertificateNumber = "1234567890";
        vm.ValidityStart = new DateOnly(2026, 4, 1);
        vm.ValidityEnd = new DateOnly(2027, 3, 31);
        vm.SupplyDays = 23;
        vm.MonthlyCostCap = 9300;
        vm.Municipality = "杉並区";
        vm.MunicipalityNumber = "131156";

        // 各セクションを埋める
        vm.RecipientAddress = "東京都杉並区...";
        vm.RecipientGender = Gender.Female;
        vm.DisabilityIntellectual = true;
        vm.DisabilityMental = true;
        vm.SupportCategory = SupportCategory.Category2;
        vm.BenefitType = BenefitType.Training;
        vm.ServiceCategory = "就労継続支援B型";
        vm.PaymentBurden = PaymentBurdenCategory.LowIncome;
        vm.MealProvisionApplicable = true;
        vm.ConsultationProviderName = "相談センターA";

        await vm.SaveCommand.ExecuteAsync(null);

        vm.SaveErrorMessage.Should().BeNull();
        vm.IsSaved.Should().BeTrue();
        var stored = _certs.AllForTest.Single();
        stored.RecipientGender.Should().Be(Gender.Female);
        stored.Disabilities.Intellectual.Should().BeTrue();
        stored.Disabilities.Mental.Should().BeTrue();
        stored.SupportCategory.Should().Be(SupportCategory.Category2);
        stored.PaymentBurden.Should().Be(PaymentBurdenCategory.LowIncome);
        stored.MunicipalityNumber.Should().Be("131156");
        stored.MealProvisionApplicable.Should().BeTrue();
        stored.ConsultationProviderName.Should().Be("相談センターA");
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

    [Fact]
    public async Task AddProviderCommand_appends_contracted_provider_to_selected_certificate()
    {
        var rid = Guid.NewGuid();
        var cert = Certificate.Create(Guid.NewGuid(), rid, "1234567890",
            new DateRange(new DateOnly(2026, 4, 1), new DateOnly(2027, 3, 31)),
            23, 9300, "杉並区", "u", DateTimeOffset.UnixEpoch, Guid.NewGuid());
        _certs.Add(cert);

        var vm = NewVm();
        vm.SelectedRecipient = TestRecipients.Make(rid, "氏名", "シメイ");
        await Task.Yield();
        // SelectedCertificate は OnSelectedRecipientChanged → ReloadCertificatesAsync 経由で読まれる。
        // 明示的に reload を待ち、先頭をセット。
        await vm.RefreshAsync();
        vm.SelectedCertificate = vm.CertificatesForRecipient.FirstOrDefault();
        vm.SelectedCertificate.Should().NotBeNull();

        vm.ProviderNumber = "1010101010";
        vm.ProviderName = "Tsumugi作業所";
        vm.ProviderServiceCategory = "就労継続支援B型";
        vm.ProviderSupplyDays = 23;
        vm.ProviderContractDate = new DateOnly(2026, 4, 1);

        await vm.AddProviderCommand.ExecuteAsync(null);

        vm.ProviderSaveErrorMessage.Should().BeNull();
        _providers.AllForTest.Single().ProviderName.Should().Be("Tsumugi作業所");
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
    public IReadOnlyList<Certificate> AllForTest => _list;
    public void Add(Certificate c) => _list.Add(c);
    public Task AddAsync(Certificate c, CancellationToken ct) { _list.Add(c); return Task.CompletedTask; }
    public Task<IReadOnlyList<Certificate>> ListByRecipientAsync(Guid rid, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<Certificate>>(_list.Where(c => c.RecipientId == rid).ToArray());
    public Task<IReadOnlyList<Certificate>> ListAllAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<Certificate>>(_list);
    public Task<Certificate?> FindEffectiveAsync(Guid rid, DateOnly asOf, CancellationToken ct) =>
        Task.FromResult<Certificate?>(null);
}

internal sealed class InMemoryContractedProviderRepo : IContractedProviderRepository
{
    private readonly List<ContractedProvider> _list = new();
    public IReadOnlyList<ContractedProvider> AllForTest => _list;
    public Task AddAsync(ContractedProvider p, CancellationToken ct) { _list.Add(p); return Task.CompletedTask; }
    public Task<ContractedProvider?> FindByIdAsync(Guid id, CancellationToken ct) =>
        Task.FromResult(_list.FirstOrDefault(p => p.Id == id));
    public Task UpdateAsync(ContractedProvider p, CancellationToken ct)
    {
        var idx = _list.FindIndex(x => x.Id == p.Id);
        if (idx >= 0) _list[idx] = p;
        return Task.CompletedTask;
    }
    public Task<IReadOnlyList<ContractedProvider>> ListByCertificateAsync(Guid certId, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<ContractedProvider>>(
            _list.Where(p => p.CertificateId == certId).ToArray());
}
