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

public sealed class DisabilityCertificateViewModelTests
{
    private readonly InMemoryRecipientRepoForCertificate _recipients = new();
    private readonly InMemoryDisabilityCertificateRepo _handbooks = new();
    private readonly InMemoryCertRepo _certs = new();
    private readonly InMemoryUow _uow = new();
    private readonly FixedClock _clock = new(DateTimeOffset.UnixEpoch);

    private DisabilityCertificateViewModel NewVm() => new(
        new ListRecipientsUseCase(_recipients),
        new RegisterDisabilityCertificateUseCase(_handbooks, _uow, _clock),
        new ListDisabilityCertificatesUseCase(_handbooks),
        new QueryDisabilityCertificateRenewalsUseCase(_handbooks),
        new QueryDisabilityConsistencyUseCase(_certs, _handbooks));

    [Fact]
    public void New_view_model_exposes_renewal_and_consistency_collections()
    {
        var sut = NewVm();
        sut.ThresholdDays.Should().Be(30);
        sut.AsOfDate.Should().Be(DateOnly.FromDateTime(DateTime.Today));
        sut.RenewalDueItems.Should().BeEmpty();
        sut.ConsistencyWarnings.Should().BeEmpty();
    }

    [Fact]
    public void Renewal_display_item_uses_recipient_name_or_safe_fallback()
    {
        new RenewalDueDisplayItem(Guid.NewGuid(), "氏名", new DateOnly(2026, 9, 1), 15)
            .RecipientName.Should().Be("氏名");
        new RenewalDueDisplayItem(Guid.NewGuid(), null, new DateOnly(2026, 9, 1), 15)
            .RecipientName.Should().Be("利用者情報未取得");
    }

    [Fact]
    public async Task Initialize_and_Refresh_load_renewals_with_recipient_names()
    {
        var recipientId = Guid.NewGuid();
        _recipients.Add(Recipient.Create(
            recipientId, "山田太郎", "ヤマダタロウ",
            new DateOnly(1990, 1, 1), "u", DateTimeOffset.UnixEpoch, Guid.NewGuid()));
        _handbooks.Add(DisabilityCertificate.Create(
            Guid.NewGuid(), recipientId, DisabilityCertificateType.Mental, "2級",
            new DateOnly(2024, 8, 1), "東京都", "u", DateTimeOffset.UnixEpoch, Guid.NewGuid(),
            nextRenewalDate: new DateOnly(2026, 8, 20)));

        var sut = NewVm();
        sut.AsOfDate = new DateOnly(2026, 8, 1);
        sut.ThresholdDays = 30;
        await sut.InitializeAsync();

        sut.RenewalDueItems.Should().ContainSingle();
        sut.RenewalDueItems[0].RecipientName.Should().Be("山田太郎");
        sut.RenewalDueItems[0].RemainingDays.Should().Be(19);

        sut.RenewalDueItems.Clear();
        await sut.RefreshAlertsAsync();
        sut.RenewalDueItems.Should().ContainSingle()
            .Which.RecipientName.Should().Be("山田太郎");
    }

    [Fact]
    public async Task Refresh_displays_archived_recipient_name_for_renewal_alert()
    {
        var recipientId = Guid.NewGuid();
        _recipients.Add(Recipient.Create(
                recipientId, "アーカイブ利用者", "アーカイブリヨウシャ",
                new DateOnly(1990, 1, 1), "u", DateTimeOffset.UnixEpoch, Guid.NewGuid())
            .Archive("u", DateTimeOffset.UnixEpoch.AddDays(1)));
        _handbooks.Add(DisabilityCertificate.Create(
            Guid.NewGuid(), recipientId, DisabilityCertificateType.Mental, "2級",
            new DateOnly(2024, 8, 1), "東京都", "u", DateTimeOffset.UnixEpoch, Guid.NewGuid(),
            nextRenewalDate: new DateOnly(2026, 8, 20)));

        var sut = NewVm();
        sut.AsOfDate = new DateOnly(2026, 8, 1);
        await sut.InitializeAsync();

        sut.Recipients.Should().BeEmpty();
        sut.RenewalDueItems.Should().ContainSingle()
            .Which.RecipientName.Should().Be("アーカイブ利用者");
    }

    [Fact]
    public async Task Selecting_recipient_loads_consistency_warnings()
    {
        var recipientId = Guid.NewGuid();
        _recipients.Add(Recipient.Create(
            recipientId, "氏名", "シメイ",
            new DateOnly(1990, 1, 1), "u", DateTimeOffset.UnixEpoch, Guid.NewGuid()));
        _handbooks.Add(DisabilityCertificate.Create(
            Guid.NewGuid(), recipientId, DisabilityCertificateType.Physical, "1級",
            new DateOnly(2024, 4, 1), "東京都", "u", DateTimeOffset.UnixEpoch, Guid.NewGuid()));

        var sut = NewVm();
        await sut.InitializeAsync();
        sut.AsOfDate = new DateOnly(2026, 8, 1);
        sut.SelectedRecipient = sut.Recipients.Single();

        sut.ConsistencyWarnings.Should().ContainSingle()
            .Which.Should().Contain("身体障害").And.Contain("手帳にはありますが");
    }

    [Fact]
    public async Task Add_success_reloads_items_alerts_and_consistency()
    {
        var recipientId = Guid.NewGuid();
        _recipients.Add(Recipient.Create(
            recipientId, "氏名", "シメイ",
            new DateOnly(1990, 1, 1), "u", DateTimeOffset.UnixEpoch, Guid.NewGuid()));

        var sut = NewVm();
        await sut.InitializeAsync();
        sut.AsOfDate = new DateOnly(2026, 8, 1);
        sut.SelectedRecipient = sut.Recipients.Single();
        sut.Type = DisabilityCertificateType.Mental;
        sut.Grade = "2級";
        sut.IssuingAuthority = "東京都";
        sut.IssuedDate = new DateOnly(2026, 8, 1);
        sut.NextRenewalDate = new DateOnly(2026, 8, 15);

        await sut.AddCommand.ExecuteAsync(null);

        sut.IsSaved.Should().BeTrue();
        sut.Items.Should().ContainSingle();
        sut.RenewalDueItems.Should().ContainSingle()
            .Which.RecipientName.Should().Be("氏名");
        sut.ConsistencyWarnings.Should().ContainSingle()
            .Which.Should().Contain("精神障害");
    }
}
