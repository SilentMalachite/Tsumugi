using System.Globalization;
using FluentAssertions;
using Tsumugi.App.Converters;
using Tsumugi.App.ViewModels;
using Tsumugi.Application.Abstractions;
using Tsumugi.Application.Dtos;
using Tsumugi.Application.UseCases.Claim;
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.Enums;
using Tsumugi.Domain.Logic.Claim;
using Tsumugi.Domain.Logic.Claim.Models;
using Tsumugi.Domain.ValueObjects;

namespace Tsumugi.App.Tests;

public sealed class ClaimInputViewModelTests
{
    private static readonly ServiceMonth Month = new(2026, 6);

    [Fact]
    public void Constructor_uses_workspace_query_and_exactly_the_five_owned_setters()
    {
        var dependencies = typeof(ClaimInputViewModel)
            .GetConstructors()
            .Single()
            .GetParameters()
            .Select(parameter => parameter.ParameterType)
            .ToArray();

        dependencies.Should().Contain(typeof(QueryClaimInputWorkspaceUseCase));
        dependencies.Should().Contain(typeof(SetClaimInputUseCase));
        dependencies.Should().Contain(typeof(SetAverageWageAnnualEvidenceUseCase));
        dependencies.Should().Contain(typeof(SetOfficeClaimProfileUseCase));
        dependencies.Should().Contain(typeof(SetCertificateClaimEvidenceUseCase));
        dependencies.Should().Contain(typeof(SetUpperLimitManagementStatementUseCase));
        dependencies.Should().NotContain(typeof(SetIntensiveSupportEpisodeUseCase));
    }

    [Fact]
    public async Task Load_reads_history_heads_and_active_owned_values()
    {
        var fixture = CreateFixture(withActiveClaimInput: true);

        await fixture.Sut.LoadAsync();

        fixture.Sut.WorkspaceLoaded.Should().BeTrue();
        fixture.Sut.ClaimInputCurrentHeadId.Should().Be(fixture.ClaimInput.Items[0].Id);
        fixture.Sut.ClaimInputEffectiveHeadId.Should().Be(fixture.ClaimInput.Items[0].Id);
        fixture.Sut.ClaimInputRevisions.Should().ContainSingle();
        fixture.Sut.UpperLimitManagementResult.Should().Be(UpperLimitManagementResult.Result2);
        fixture.Sut.UpperLimitManagedAmountYen.Should().Be(1_000);
        fixture.Sut.MunicipalSubsidyAmountYen.Should().Be(500);
    }

    [Fact]
    public async Task MunicipalSubsidyAmountYen_is_an_editable_observable_property_included_in_the_save_payload()
    {
        // Task 7 fix round 1: MunicipalSubsidyAmountYen was previously read-only (preserved
        // verbatim from the last loaded revision on every save). It is now an owned,
        // user-editable field exposed by ClaimInputView, mirroring the sibling
        // UpperLimitManagedAmountYen field. This test proves a value the user enters through
        // the view model actually reaches the save payload instead of being silently discarded.
        var fixture = CreateFixture(withActiveClaimInput: false);
        await fixture.Sut.LoadAsync();
        fixture.Sut.UpperLimitManagementResult = UpperLimitManagementResult.Result1;
        fixture.Sut.UpperLimitManagedAmountYen = 100;
        fixture.Sut.MunicipalSubsidyAmountYen = 1_234;

        await fixture.Sut.SaveClaimInputAsync();

        var created = fixture.ClaimInput.Items.Should().ContainSingle().Subject;
        created.MunicipalSubsidyAmountYen.Should().Be(1_234);
    }

    [Fact]
    public async Task Correcting_claim_input_preserves_the_four_non_owned_values_and_saves_the_edited_subsidy_amount()
    {
        var fixture = CreateFixture(withActiveClaimInput: true);
        await fixture.Sut.LoadAsync();
        fixture.Sut.UpperLimitManagementResult = UpperLimitManagementResult.Result3;
        fixture.Sut.UpperLimitManagedAmountYen = 2_000;
        fixture.Sut.MunicipalSubsidyAmountYen = 750;

        await fixture.Sut.SaveClaimInputAsync();

        fixture.ClaimInput.Items.Should().HaveCount(2);
        var saved = fixture.ClaimInput.Items[^1];
        saved.Kind.Should().Be(RecordKind.Correct);
        saved.ExpectedHeadId.Should().Be(fixture.ClaimInput.Items[0].Id);
        // MunicipalSubsidyAmountYen is now owned by ClaimInputView: the edited value (750) is
        // saved, not the previously loaded value (500) that "preserve" would have carried over.
        saved.MunicipalSubsidyAmountYen.Should().Be(750);
        saved.ExceptionalUsageStartMonth.Should().Be(Month);
        saved.ExceptionalUsageEndMonth.Should().Be(Month);
        saved.ExceptionalUsageDays.Should().Be(10);
        saved.StandardUsageDayTotal.Should().Be(22);
    }

    [Fact]
    public async Task Context_change_clears_workspace_synchronously_and_blocks_save()
    {
        var fixture = CreateFixture(withActiveClaimInput: true);
        await fixture.Sut.LoadAsync();

        fixture.Sut.SourceFiscalYear++;

        fixture.Sut.WorkspaceLoaded.Should().BeFalse();
        fixture.Sut.ClaimInputRevisions.Should().BeEmpty();
        fixture.Sut.UpperLimitManagementResult.Should().BeNull();
        fixture.Sut.UpperLimitManagedAmountYen.Should().BeNull();
        fixture.Sut.MunicipalSubsidyAmountYen.Should().BeNull();
        await fixture.Sut.SaveClaimInputAsync();
        fixture.ClaimInput.Items.Should().ContainSingle();
        fixture.Sut.ErrorMessage.Should().Be("請求入力履歴を再読込してください。");
    }

    [Fact]
    public async Task Cancelled_claim_input_requires_explicit_reentry_and_clears_non_owned_values()
    {
        var fixture = CreateFixture(withActiveClaimInput: true);
        var root = fixture.ClaimInput.Items[0];
        var cancelId = Guid.NewGuid();
        fixture.ClaimInput.Items.Add(new ClaimInput
        {
            Id = cancelId,
            OfficeId = root.OfficeId,
            RecipientId = root.RecipientId,
            ServiceMonth = root.ServiceMonth,
            RootId = root.RootId,
            Revision = 2,
            Kind = RecordKind.Cancel,
            ExpectedHeadId = root.Id,
            CreatedAt = DateTimeOffset.UnixEpoch.AddHours(1),
            CreatedBy = "operator",
            ConcurrencyToken = Guid.NewGuid(),
        });
        await fixture.Sut.LoadAsync();
        fixture.Sut.UpperLimitManagementResult = UpperLimitManagementResult.Result1;
        fixture.Sut.UpperLimitManagedAmountYen = 300;

        await fixture.Sut.SaveClaimInputAsync();
        fixture.ClaimInput.Items.Should().HaveCount(2);

        fixture.Sut.ReenterClaimInput();
        fixture.Sut.UpperLimitManagementResult = UpperLimitManagementResult.Result1;
        fixture.Sut.UpperLimitManagedAmountYen = 300;
        await fixture.Sut.SaveClaimInputAsync();

        var reentered = fixture.ClaimInput.Items.Should().HaveCount(3).And.Subject.Last();
        reentered.Kind.Should().Be(RecordKind.Correct);
        reentered.ExpectedHeadId.Should().Be(cancelId);
        reentered.MunicipalSubsidyAmountYen.Should().BeNull();
        reentered.ExceptionalUsageStartMonth.Should().BeNull();
        reentered.ExceptionalUsageEndMonth.Should().BeNull();
        reentered.ExceptionalUsageDays.Should().BeNull();
        reentered.StandardUsageDayTotal.Should().BeNull();
    }

    [Fact]
    public async Task New_claim_input_starts_with_null_expected_head_and_null_non_owned_values()
    {
        var fixture = CreateFixture(withActiveClaimInput: false);
        await fixture.Sut.LoadAsync();
        fixture.Sut.UpperLimitManagementResult = UpperLimitManagementResult.Result2;
        fixture.Sut.UpperLimitManagedAmountYen = 900;

        await fixture.Sut.SaveClaimInputAsync();

        var created = fixture.ClaimInput.Items.Should().ContainSingle().Subject;
        created.Kind.Should().Be(RecordKind.New);
        created.ExpectedHeadId.Should().BeNull();
        created.MunicipalSubsidyAmountYen.Should().BeNull();
        created.ExceptionalUsageStartMonth.Should().BeNull();
        created.ExceptionalUsageEndMonth.Should().BeNull();
        created.ExceptionalUsageDays.Should().BeNull();
        created.StandardUsageDayTotal.Should().BeNull();
    }

    [Fact]
    public async Task Load_discards_response_when_context_changes_while_query_is_in_flight()
    {
        var fixture = CreateFixture(withActiveClaimInput: true);
        fixture.ClaimInput.BlockNextLoad();

        var load = fixture.Sut.LoadAsync();
        await fixture.ClaimInput.LoadStarted.WaitAsync(TimeSpan.FromSeconds(5));
        fixture.Sut.SourceFiscalYear++;
        fixture.ClaimInput.ReleaseLoad();
        await load;

        fixture.Sut.WorkspaceLoaded.Should().BeFalse();
        fixture.Sut.ClaimInputRevisions.Should().BeEmpty();
        fixture.Sut.ClaimInputCurrentHeadId.Should().BeNull();
    }

    [Fact]
    public async Task Query_failure_uses_fixed_sanitized_message()
    {
        var fixture = CreateFixture(withActiveClaimInput: true);
        var existing = fixture.ClaimInput.Items[0];
        var secondRoot = Guid.NewGuid();
        fixture.ClaimInput.Items.Add(existing with
        {
            Id = secondRoot,
            RootId = secondRoot,
            UpperLimitManagedAmountYen = 999_999,
        });

        await fixture.Sut.LoadAsync();

        fixture.Sut.WorkspaceLoaded.Should().BeFalse();
        fixture.Sut.ErrorMessage.Should().Be("請求入力履歴を再読込してください。");
        fixture.Sut.ErrorMessage.Should().NotContain(secondRoot.ToString())
            .And.NotContain("Claim input query failed");
    }

    [Fact]
    public async Task Average_wage_reentry_preserves_chain_identity_period()
    {
        var fixture = CreateFixture(withActiveClaimInput: false);
        var rootId = Guid.NewGuid();
        var cancelId = Guid.NewGuid();
        var periodStart = new DateOnly(2025, 4, 1);
        var periodEnd = new DateOnly(2026, 3, 31);
        fixture.AverageWage.Items.AddRange([
            new AverageWageAnnualEvidence
            {
                Id = rootId,
                OfficeId = fixture.Sut.OfficeId,
                SourceFiscalYear = fixture.Sut.SourceFiscalYear,
                PeriodStart = periodStart,
                PeriodEnd = periodEnd,
                RootId = rootId,
                Revision = 1,
                Kind = RecordKind.New,
                AnnualWagePaidYen = 1_000,
                AnnualExtendedUsers = 10,
                AnnualOpeningDays = 20,
                Completeness = FiscalYearCompleteness.Complete,
                EvidenceDocumentId = "evidence",
                DailyEvidenceReference = "daily",
                MonthlyEvidenceReference = "monthly",
                ConfirmedAt = DateTimeOffset.UnixEpoch,
                ConfirmedBy = "reviewer",
                ConfirmationReason = "checked",
                CreatedAt = DateTimeOffset.UnixEpoch,
                CreatedBy = "operator",
                ConcurrencyToken = Guid.NewGuid(),
            },
            new AverageWageAnnualEvidence
            {
                Id = cancelId,
                OfficeId = fixture.Sut.OfficeId,
                SourceFiscalYear = fixture.Sut.SourceFiscalYear,
                PeriodStart = periodStart,
                PeriodEnd = periodEnd,
                RootId = rootId,
                Revision = 2,
                Kind = RecordKind.Cancel,
                ExpectedHeadId = rootId,
                CreatedAt = DateTimeOffset.UnixEpoch.AddHours(1),
                CreatedBy = "operator",
                ConcurrencyToken = Guid.NewGuid(),
            },
        ]);
        await fixture.Sut.LoadAsync();

        fixture.Sut.ReenterAverageWage();

        fixture.Sut.AverageWagePeriodStart.Should().Be(periodStart);
        fixture.Sut.AverageWagePeriodEnd.Should().Be(periodEnd);
        fixture.Sut.AverageWageCurrentHeadId.Should().Be(cancelId);
    }

    [Fact]
    public async Task Office_profile_save_reports_fixed_master_unavailable_message()
    {
        var fixture = CreateFixture(
            withActiveClaimInput: false,
            new UnavailableOfficeClaimProfilePolicyProvider());
        await fixture.Sut.LoadAsync();
        fixture.Sut.ProfileEffectiveFrom = (DateOnly)DateOnlyConverter.Instance.ConvertBack(
            "2026-06-01", typeof(DateOnly), null, CultureInfo.InvariantCulture)!;
        fixture.Sut.MasterVersion = (ClaimMasterVersion)ClaimMasterVersionConverter.Instance
            .ConvertBack("not-seeded", typeof(ClaimMasterVersion?), null,
                CultureInfo.InvariantCulture)!;
        fixture.Sut.ReformStatus = R8ReformStatus.ReformTarget;
        fixture.Sut.AverageWageBandOption = (AverageWageBandOption)
            AverageWageBandOptionConverter.Instance.ConvertBack(
                "Numeric:1", typeof(AverageWageBandOption?), null,
                CultureInfo.InvariantCulture)!;

        await fixture.Sut.SaveOfficeProfileAsync();

        fixture.OfficeProfile.Items.Should().BeEmpty();
        fixture.Sut.WorkspaceLoaded.Should().BeTrue();
        fixture.Sut.ErrorMessage.Should().Be("請求制度マスターを利用できません。");
        fixture.Sut.ErrorMessage.Should().NotContain("not-seeded");
    }

    [Fact]
    public async Task Average_wage_supports_new_correct_cancel_and_explicit_reentry()
    {
        var fixture = CreateFixture(withActiveClaimInput: false);
        await fixture.Sut.LoadAsync();
        ConfigureAverageWage(fixture.Sut);

        await fixture.Sut.SaveAverageWageAsync();
        fixture.Sut.AnnualWagePaidYen = 1_200_000;
        await fixture.Sut.SaveAverageWageAsync();
        await fixture.Sut.CancelAverageWageAsync();
        fixture.Sut.ReenterAverageWage();
        ConfigureAverageWage(fixture.Sut);
        await fixture.Sut.SaveAverageWageAsync();

        fixture.AverageWage.Items.Select(item => item.Kind).Should().Equal(
            RecordKind.New, RecordKind.Correct, RecordKind.Cancel, RecordKind.Correct);
        fixture.AverageWage.Items[^1].ExpectedHeadId
            .Should().Be(fixture.AverageWage.Items[^2].Id);
    }

    [Fact]
    public async Task Office_profile_supports_new_correct_cancel_and_explicit_reentry()
    {
        var fixture = CreateFixture(withActiveClaimInput: false);
        await fixture.Sut.LoadAsync();
        ConfigureOfficeProfile(fixture.Sut);

        await fixture.Sut.SaveOfficeProfileAsync();
        fixture.Sut.OfficeProfileEvidenceDocumentId = "corrected-evidence";
        await fixture.Sut.SaveOfficeProfileAsync();
        await fixture.Sut.CancelOfficeProfileAsync();
        fixture.Sut.ReenterOfficeProfile();
        ConfigureOfficeProfile(fixture.Sut);
        await fixture.Sut.SaveOfficeProfileAsync();

        fixture.OfficeProfile.Items.Select(item => item.Kind).Should().Equal(
            RecordKind.New, RecordKind.Correct, RecordKind.Cancel, RecordKind.Correct);
        fixture.OfficeProfile.Items[^2].MasterVersion.Should().BeNull();
        fixture.OfficeProfile.Items[^1].ExpectedHeadId
            .Should().Be(fixture.OfficeProfile.Items[^2].Id);
        fixture.OfficeProfile.Items[^1].CapacityHeadcount.Should().Be(20);
        fixture.OfficeProfile.Items[^1].StaffingKey.Should().Be("staff-test");
        fixture.OfficeProfile.Items[^1].RegionKey.Should().Be("region-test");
    }

    [Fact]
    public async Task Office_profile_round_trips_billing_tokens_through_reload()
    {
        var fixture = CreateFixture(withActiveClaimInput: false);
        await fixture.Sut.LoadAsync();
        ConfigureOfficeProfile(fixture.Sut);

        await fixture.Sut.SaveOfficeProfileAsync();

        fixture.Sut.CapacityHeadcount.Should().Be(20);
        fixture.Sut.StaffingKey.Should().Be("staff-test");
        fixture.Sut.RegionKey.Should().Be("region-test");
    }

    [Fact]
    public async Task Load_populates_billing_token_options_from_master_without_hardcoding()
    {
        // Task 9b: StaffingKey/RegionKeyの選択肢はマスタ由来（FakeClaimMasterProviderが
        // 供給するstaff-test/region-test）であり、VM/View側に語彙をハードコードしない。
        var fixture = CreateFixture(withActiveClaimInput: false);

        await fixture.Sut.LoadAsync();

        fixture.Sut.StaffingKeyOptions.Should().Equal("staff-test");
        fixture.Sut.RegionKeyOptions.Should().Equal("region-test");
    }

    [Fact]
    public async Task Certificate_evidence_supports_new_correct_cancel_and_explicit_reentry()
    {
        var fixture = CreateFixture(withActiveClaimInput: false);
        await fixture.Sut.LoadAsync();
        ConfigureCertificateEvidence(fixture.Sut);

        await fixture.Sut.SaveCertificateEvidenceAsync();
        fixture.Sut.MonthlyCostCapYen = 10_000;
        await fixture.Sut.SaveCertificateEvidenceAsync();
        await fixture.Sut.CancelCertificateEvidenceAsync();
        fixture.Sut.ReenterCertificateEvidence();
        ConfigureCertificateEvidence(fixture.Sut);
        await fixture.Sut.SaveCertificateEvidenceAsync();

        fixture.CertificateEvidence.Items.Select(item => item.Kind).Should().Equal(
            RecordKind.New, RecordKind.Correct, RecordKind.Cancel, RecordKind.Correct);
        fixture.CertificateEvidence.Items[^1].ExpectedHeadId
            .Should().Be(fixture.CertificateEvidence.Items[^2].Id);
    }

    [Fact]
    public async Task Statement_supports_new_correct_cancel_and_explicit_reentry()
    {
        var fixture = CreateFixture(withActiveClaimInput: false);
        await fixture.Sut.LoadAsync();
        ConfigureStatement(fixture.Sut);

        await fixture.Sut.SaveStatementAsync();
        fixture.Sut.StatementTotalCostYen = 1_200;
        fixture.Sut.StatementLines[0].TotalCostYen = 1_200;
        await fixture.Sut.SaveStatementAsync();
        await fixture.Sut.CancelStatementAsync();
        fixture.Sut.ReenterStatement();
        ConfigureStatement(fixture.Sut);
        await fixture.Sut.SaveStatementAsync();

        fixture.Statement.Items.Select(item => item.Header.Kind).Should().Equal(
            RecordKind.New, RecordKind.Correct, RecordKind.Cancel, RecordKind.Correct);
        fixture.Statement.Items[^2].Lines.Should().BeEmpty();
        fixture.Statement.Items[^1].Header.ExpectedHeadId
            .Should().Be(fixture.Statement.Items[^2].Header.Id);
    }

    private static void ConfigureAverageWage(ClaimInputViewModel sut)
    {
        sut.AverageWagePeriodStart = new DateOnly(2025, 4, 1);
        sut.AverageWagePeriodEnd = new DateOnly(2026, 3, 31);
        sut.AnnualWagePaidYen = 1_000_000;
        sut.AnnualExtendedUsers = 100;
        sut.AnnualOpeningDays = 240;
        sut.Completeness = FiscalYearCompleteness.Complete;
        sut.AverageWageEvidenceDocumentId = "wage-evidence";
        sut.DailyEvidenceReference = "daily-evidence";
        sut.MonthlyEvidenceReference = "monthly-evidence";
        sut.AverageWageConfirmedAt = DateTimeOffset.UnixEpoch;
        sut.AverageWageConfirmedBy = "reviewer";
        sut.AverageWageConfirmationReason = "checked";
    }

    private static void ConfigureOfficeProfile(ClaimInputViewModel sut)
    {
        sut.ProfileEffectiveFrom = new DateOnly(2026, 6, 1);
        sut.MasterVersion = new ClaimMasterVersion("test-master");
        sut.ReformStatus = R8ReformStatus.ReformTarget;
        sut.AverageWageBandOption =
            new AverageWageBandOption(AverageWageBandOptionKind.Numeric, 1);
        sut.OfficeProfileEvidenceDocumentId = "designation-ledger";
        sut.OfficeProfileConfirmedAt = DateTimeOffset.UnixEpoch;
        sut.OfficeProfileConfirmedBy = "reviewer";
        sut.OfficeProfileConfirmationReason = "checked";
        sut.CapacityHeadcount = 20;
        sut.StaffingKey = "staff-test";
        sut.RegionKey = "region-test";
    }

    private static void ConfigureCertificateEvidence(ClaimInputViewModel sut)
    {
        sut.CertificateValidityStart = new DateOnly(2026, 4, 1);
        sut.CertificateValidityEnd = new DateOnly(2027, 3, 31);
        sut.MonthlyCostCapIsEntered = true;
        sut.MonthlyCostCapYen = 9_300;
        sut.UpperLimitManagementApplicability =
            UpperLimitManagementApplicability.Applicable;
        sut.UpperLimitManagementOfficeNumber = "1234567890";
        sut.Article31Status = Article31SpecialBurdenStatus.NotApplicable;
        sut.Article31AmountIsEntered = false;
        sut.Article31EffectivePeriod =
            new DateRange(new DateOnly(2026, 4, 1), new DateOnly(2027, 3, 31));
        sut.CertificateOriginalDocumentReference = "certificate-original";
        sut.CertificateEvidenceConfirmedAt = DateTimeOffset.UnixEpoch;
        sut.CertificateEvidenceConfirmedBy = "reviewer";
        sut.CertificateEvidenceConfirmationReason = "checked";
    }

    private static void ConfigureStatement(ClaimInputViewModel sut)
    {
        sut.MunicipalityNumber = "131156";
        sut.StatementCertificateNumber = "1234567890";
        sut.StatementCertificateMonthlyCostCapIsEntered = true;
        sut.StatementCertificateMonthlyCostCapYen = 9_300;
        sut.StatementUpperLimitManagementApplicability =
            UpperLimitManagementApplicability.Applicable;
        sut.CertificateManagingOfficeNumber = "9876543210";
        sut.ManagingOfficeNumber = "9876543210";
        sut.ManagingOfficeName = "管理事業所";
        sut.OriginalCreationKind = "original";
        sut.StatementReceivedAt = DateTimeOffset.UnixEpoch;
        sut.StatementOriginalDocumentReference = "statement-original";
        sut.StatementIsConfirmed = true;
        sut.StatementConfirmedAt = DateTimeOffset.UnixEpoch;
        sut.StatementConfirmedBy = "reviewer";
        sut.StatementConfirmationReason = "checked";
        sut.StatementResult = UpperLimitManagementResult.Result2;
        sut.StatementTotalCostIsEntered = true;
        sut.StatementTotalCostYen = 1_000;
        sut.StatementTotalPreManagementBurdenIsEntered = true;
        sut.StatementTotalPreManagementBurdenYen = 500;
        sut.StatementTotalManagedBurdenIsEntered = true;
        sut.StatementTotalManagedBurdenYen = 500;
        sut.StatementLines.Clear();
        sut.StatementLines.Add(new UpperLimitManagementStatementLineViewModel
        {
            LineNumber = 1,
            OfficeNumber = "9876543210",
            OfficeName = "管理事業所",
            TotalCostIsEntered = true,
            TotalCostYen = 1_000,
            PreManagementBurdenIsEntered = true,
            PreManagementBurdenYen = 500,
            ManagedBurdenIsEntered = true,
            ManagedBurdenYen = 500,
        });
    }

    private static Fixture CreateFixture(
        bool withActiveClaimInput,
        IOfficeClaimProfilePolicyProvider? policyProvider = null)
    {
        var officeId = Guid.NewGuid();
        var recipientId = Guid.NewGuid();
        var certificateId = Guid.NewGuid();
        var claimInput = new FakeClaimInputRepository();
        if (withActiveClaimInput)
        {
            var id = Guid.NewGuid();
            claimInput.Items.Add(new ClaimInput
            {
                Id = id,
                OfficeId = officeId,
                RecipientId = recipientId,
                ServiceMonth = Month,
                RootId = id,
                Revision = 1,
                Kind = RecordKind.New,
                UpperLimitManagementResult = UpperLimitManagementResult.Result2,
                UpperLimitManagedAmountYen = 1_000,
                MunicipalSubsidyAmountYen = 500,
                ExceptionalUsageStartMonth = Month,
                ExceptionalUsageEndMonth = Month,
                ExceptionalUsageDays = 10,
                StandardUsageDayTotal = 22,
                CreatedAt = DateTimeOffset.UnixEpoch,
                CreatedBy = "operator",
                ConcurrencyToken = Guid.NewGuid(),
            });
        }

        var averageWage = new FakeAverageWageRepository();
        var officeProfile = new FakeOfficeProfileRepository();
        var certificateEvidence = new FakeCertificateEvidenceRepository();
        var statement = new FakeStatementRepository();
        var policy = CreateOfficePolicy();
        policyProvider ??= new FixedOfficeClaimProfilePolicyProvider(policy);
        var query = new QueryClaimInputWorkspaceUseCase(
            claimInput, averageWage, officeProfile, certificateEvidence, statement,
            policyProvider);
        var uow = new FakeUnitOfWork();
        var clock = new FixedTimeProvider(DateTimeOffset.UnixEpoch.AddDays(1));
        var sut = new ClaimInputViewModel(
            null!, null!, null!, query,
            new SetClaimInputUseCase(claimInput, uow, clock),
            new SetAverageWageAnnualEvidenceUseCase(averageWage, uow, clock),
            new SetOfficeClaimProfileUseCase(officeProfile, uow, clock, policyProvider),
            new SetCertificateClaimEvidenceUseCase(certificateEvidence, uow, clock),
            new SetUpperLimitManagementStatementUseCase(statement, uow, clock),
            new QueryClaimBillingTokenOptionsUseCase(new FakeClaimMasterProvider()))
        {
            OfficeId = officeId,
            RecipientId = recipientId,
            CertificateId = certificateId,
            Year = Month.Year,
            Month = Month.Month,
            SourceFiscalYear = 2025,
        };
        return new Fixture(
            sut, claimInput, averageWage, officeProfile, certificateEvidence, statement);
    }

    private static OfficeClaimProfilePolicy CreateOfficePolicy()
    {
        var version = new ClaimMasterVersion("test-master");
        var option = new AverageWageBandOption(AverageWageBandOptionKind.Numeric, 1);
        var rule = new AverageWageBandOptionVersionRule(
            version, Month, null, [option],
            new Dictionary<R8ReformStatus, IReadOnlyCollection<AverageWageBandOption>>
            {
                [R8ReformStatus.ReformTarget] = [option],
            });
        return new OfficeClaimProfilePolicy(
            version, [rule], new DateOnly(2026, 6, 1), date => date.AddYears(3));
    }

    private sealed record Fixture(
        ClaimInputViewModel Sut,
        FakeClaimInputRepository ClaimInput,
        FakeAverageWageRepository AverageWage,
        FakeOfficeProfileRepository OfficeProfile,
        FakeCertificateEvidenceRepository CertificateEvidence,
        FakeStatementRepository Statement);

    private sealed class UnavailableOfficeClaimProfilePolicyProvider
        : IOfficeClaimProfilePolicyProvider
    {
        public OfficeClaimProfilePolicy Resolve(ClaimMasterVersion masterVersion) =>
            throw new ClaimMasterPolicyUnavailableException(
                ClaimMasterPolicyUnavailableCode.Unavailable);
    }

    private sealed class FakeClaimMasterProvider : IClaimMasterProvider
    {
        public ClaimMasterRelease ResolveVersion(ServiceMonth serviceMonth) =>
            throw new NotSupportedException();

        public ClaimCalculationMasterBundle ResolveCalculationMasters(ServiceMonth serviceMonth) => new(
            BasicRewards: [],
            UnitAdjustments: [],
            RegionUnitPrices:
            [
                new RegionUnitPriceMasterRow(
                    "price-test", "region-test", "b-type", 10.00m, Month, null, [SourceRef()]),
            ],
            BurdenCaps: [],
            TransitionRules: [],
            ServiceCodes: [],
            ConditionDefinitions:
            [
                new ClaimConditionDefinition(
                    "cond-staff-test", Month, null, ClaimConditionKind.Staffing,
                    ClaimConditionOperator.Equals,
                    new ClaimConditionTokenOperand("staff-test"), [SourceRef()]),
            ]);

        private static ClaimSourceRef SourceRef() => new(
            "doc-1",
            "0000000000000000000000000000000000000000000000000000000000000000",
            "loc",
            ClaimSourceEvidenceRole.Authoritative,
            [ClaimSourceSupport.Conditions]);
    }

    private sealed class FakeClaimInputRepository : IClaimInputRepository
    {
        private TaskCompletionSource? _loadStarted;
        private TaskCompletionSource? _releaseLoad;
        public List<ClaimInput> Items { get; } = [];
        public Task LoadStarted => _loadStarted?.Task ?? Task.CompletedTask;
        public void BlockNextLoad()
        {
            _loadStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
            _releaseLoad = new(TaskCreationOptions.RunContinuationsAsynchronously);
        }
        public void ReleaseLoad() => _releaseLoad?.TrySetResult();
        public Task AddAsync(ClaimInput input, CancellationToken ct)
        {
            Items.Add(input);
            return Task.CompletedTask;
        }
        public async Task<IReadOnlyList<ClaimInput>> ListHistoryAsync(
            Guid officeId, Guid recipientId, ServiceMonth serviceMonth, CancellationToken ct)
        {
            if (_loadStarted is { } started && _releaseLoad is { } release)
            {
                _loadStarted = null;
                started.TrySetResult();
                await release.Task.WaitAsync(ct);
                _releaseLoad = null;
            }
            return Items.Where(item => item.OfficeId == officeId
                && item.RecipientId == recipientId && item.ServiceMonth == serviceMonth).ToArray();
        }
    }

    private sealed class FakeAverageWageRepository : IAverageWageAnnualEvidenceRepository
    {
        public List<AverageWageAnnualEvidence> Items { get; } = [];
        public Task AddAsync(AverageWageAnnualEvidence evidence, CancellationToken ct)
        {
            Items.Add(evidence);
            return Task.CompletedTask;
        }
        public Task<IReadOnlyList<AverageWageAnnualEvidence>> ListHistoryAsync(
            Guid officeId, int sourceFiscalYear, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<AverageWageAnnualEvidence>>(Items.Where(item =>
                item.OfficeId == officeId && item.SourceFiscalYear == sourceFiscalYear).ToArray());
    }

    private sealed class FakeOfficeProfileRepository : IOfficeClaimProfileRepository
    {
        public List<OfficeClaimProfile> Items { get; } = [];
        public Task AddAsync(OfficeClaimProfile profile, CancellationToken ct)
        {
            Items.Add(profile);
            return Task.CompletedTask;
        }
        public Task<IReadOnlyList<OfficeClaimProfile>> ListByOfficeAsync(
            Guid officeId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<OfficeClaimProfile>>(
                Items.Where(item => item.OfficeId == officeId).ToArray());
    }

    private sealed class FakeCertificateEvidenceRepository : ICertificateClaimEvidenceRepository
    {
        public List<CertificateClaimEvidence> Items { get; } = [];
        public Task AddAsync(CertificateClaimEvidence evidence, CancellationToken ct)
        {
            Items.Add(evidence);
            return Task.CompletedTask;
        }
        public Task<IReadOnlyList<CertificateClaimEvidence>> ListByCertificateAsync(
            Guid certificateId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<CertificateClaimEvidence>>(
                Items.Where(item => item.CertificateId == certificateId).ToArray());
    }

    private sealed class FakeStatementRepository : IUpperLimitManagementStatementRepository
    {
        public List<UpperLimitManagementStatementAggregate> Items { get; } = [];
        public Task AddAsync(UpperLimitManagementStatement statement,
            IReadOnlyCollection<UpperLimitManagementStatementLine> lines, CancellationToken ct)
        {
            Items.Add(new UpperLimitManagementStatementAggregate(statement, lines));
            return Task.CompletedTask;
        }
        public Task<IReadOnlyList<UpperLimitManagementStatementAggregate>>
            ListHistoryAggregatesAsync(Guid managingOfficeId, Guid recipientId,
                ServiceMonth serviceMonth, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<UpperLimitManagementStatementAggregate>>(Items.Where(item =>
                item.Header.ManagingOfficeId == managingOfficeId
                && item.Header.RecipientId == recipientId
                && item.Header.ServiceMonth == serviceMonth).ToArray());
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken ct) => Task.FromResult(1);
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
