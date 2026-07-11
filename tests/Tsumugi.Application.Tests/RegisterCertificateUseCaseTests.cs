using FluentAssertions;
using Tsumugi.Application.Abstractions;
using Tsumugi.Application.UseCases.Certificate;
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.Enums;
using Tsumugi.Domain.ValueObjects;
using Xunit;

namespace Tsumugi.Application.Tests;

public sealed class RegisterCertificateUseCaseTests
{
    private static RegisterCertificateInput MinimalInput(Guid rid, string number, DateRange validity)
        => new(rid, number, validity, SupplyDays: 23, MonthlyCostCap: 9300, Municipality: "杉並区")
        {
            MunicipalityNumber = "131156",
        };

    [Fact]
    public async Task Adds_certificate_when_no_overlap()
    {
        var repo = new FakeCertificateRepository();
        var uow = new FakeUnitOfWork();
        var sut = new RegisterCertificateUseCase(repo, uow,
            new FixedTimeProvider(DateTimeOffset.UnixEpoch));

        var rid = Guid.NewGuid();
        var input = MinimalInput(rid, "1234567890",
            new DateRange(new DateOnly(2026, 4, 1), new DateOnly(2027, 3, 31)));
        var (dto, warnings) = await sut.ExecuteAsync(input, "u", default);

        warnings.Should().BeEmpty();
        repo.Added.Should().ContainSingle();
        dto.CertificateNumber.Should().Be("1234567890");
        dto.BenefitType.Should().Be(BenefitType.Training, "既定は訓練等給付");
    }

    [Fact]
    public async Task Surfaces_warning_when_period_overlaps_existing()
    {
        var repo = new FakeCertificateRepository();
        var rid = Guid.NewGuid();
        repo.Added.Add(Certificate.Create(Guid.NewGuid(), rid, "old",
            new DateRange(new DateOnly(2026, 1, 1), new DateOnly(2026, 6, 30)),
            0, 0, "x", "u", DateTimeOffset.UnixEpoch, Guid.NewGuid()));

        var sut = new RegisterCertificateUseCase(repo, new FakeUnitOfWork(),
            new FixedTimeProvider(DateTimeOffset.UnixEpoch));

        var input = MinimalInput(rid, "new",
            new DateRange(new DateOnly(2026, 6, 1), new DateOnly(2027, 3, 31)));  // overlaps
        var (_, warnings) = await sut.ExecuteAsync(input, "u", default);

        warnings.Should().NotBeEmpty();
        warnings.Should().ContainMatch("*重複*");
    }

    [Fact]
    public async Task Rejects_empty_recipient_id()
    {
        var sut = new RegisterCertificateUseCase(
            new FakeCertificateRepository(), new FakeUnitOfWork(),
            new FixedTimeProvider(DateTimeOffset.UnixEpoch));

        var input = MinimalInput(Guid.Empty, "123",
            new DateRange(new DateOnly(2026, 4, 1), new DateOnly(2027, 3, 31)));
        Func<Task> act = () => sut.ExecuteAsync(input, "u", default);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task Persists_all_form_sections_into_entity()
    {
        var repo = new FakeCertificateRepository();
        var sut = new RegisterCertificateUseCase(repo, new FakeUnitOfWork(),
            new FixedTimeProvider(DateTimeOffset.UnixEpoch));

        var rid = Guid.NewGuid();
        var validity = new DateRange(new DateOnly(2026, 4, 1), new DateOnly(2027, 3, 31));
        var input = MinimalInput(rid, "1234567890", validity) with
        {
            RecipientAddress = "東京都杉並区...",
            RecipientGender = Gender.Female,
            GuardianName = "山田保護",
            GuardianRelationship = "母",
            Disabilities = new DisabilityCategories(false, true, true, false),
            SupportCategory = SupportCategory.Category3,
            BenefitType = BenefitType.Training,
            ServiceCategory = "就労継続支援B型",
            SupplyNotes = "週3日想定",
            ConsultationProviderName = "相談センターA",
            ConsultationProviderNumber = "9999999999",
            ConsultationStart = new DateOnly(2026, 4, 1),
            ConsultationEnd = new DateOnly(2027, 3, 31),
            PaymentBurden = PaymentBurdenCategory.General1,
            UpperLimitManagementProvider = "事業所A",
            MealProvisionApplicable = true,
            HighCostBenefitApplicable = false,
        };
        var (dto, _) = await sut.ExecuteAsync(input, "u", default);

        dto.RecipientAddress.Should().Be("東京都杉並区...");
        dto.RecipientGender.Should().Be(Gender.Female);
        dto.GuardianName.Should().Be("山田保護");
        dto.GuardianRelationship.Should().Be("母");
        dto.Disabilities.Intellectual.Should().BeTrue();
        dto.Disabilities.Mental.Should().BeTrue();
        dto.SupportCategory.Should().Be(SupportCategory.Category3);
        dto.PaymentBurden.Should().Be(PaymentBurdenCategory.General1);
        dto.MealProvisionApplicable.Should().BeTrue();
        dto.ConsultationProviderName.Should().Be("相談センターA");

        var stored = repo.Added.Single();
        stored.RecipientGender.Should().Be(Gender.Female);
        stored.Disabilities.Mental.Should().BeTrue();
    }

    [Fact]
    public async Task Rejects_inverted_consultation_period()
    {
        var sut = new RegisterCertificateUseCase(
            new FakeCertificateRepository(), new FakeUnitOfWork(),
            new FixedTimeProvider(DateTimeOffset.UnixEpoch));

        var rid = Guid.NewGuid();
        var input = MinimalInput(rid, "X",
            new DateRange(new DateOnly(2026, 4, 1), new DateOnly(2027, 3, 31))) with
        {
            ConsultationStart = new DateOnly(2027, 1, 1),
            ConsultationEnd = new DateOnly(2026, 12, 1),
        };

        Func<Task> act = () => sut.ExecuteAsync(input, "u", default);
        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*計画相談支援期間*");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("12345")]
    [InlineData("1234567")]
    [InlineData("12A456")]
    [InlineData("１２３４５６")]
    public async Task Rejects_municipality_number_that_is_not_six_ascii_digits(string? value)
    {
        var repo = new FakeCertificateRepository();
        var sut = new RegisterCertificateUseCase(
            repo, new FakeUnitOfWork(), new FixedTimeProvider(DateTimeOffset.UnixEpoch));
        var input = MinimalInput(
            Guid.NewGuid(),
            "1234567890",
            new DateRange(new DateOnly(2026, 4, 1), new DateOnly(2027, 3, 31))) with
        {
            MunicipalityNumber = value,
        };

        var act = () => sut.ExecuteAsync(input, "u", default);

        await act.Should().ThrowAsync<ArgumentException>();
        repo.Added.Should().BeEmpty();
    }

    [Theory]
    [InlineData("")]
    [InlineData("12345")]
    [InlineData("12A456")]
    public async Task Rejects_invalid_subsidy_municipality_number_when_present(string value)
    {
        var repo = new FakeCertificateRepository();
        var sut = new RegisterCertificateUseCase(
            repo, new FakeUnitOfWork(), new FixedTimeProvider(DateTimeOffset.UnixEpoch));
        var input = MinimalInput(
            Guid.NewGuid(),
            "1234567890",
            new DateRange(new DateOnly(2026, 4, 1), new DateOnly(2027, 3, 31))) with
        {
            SubsidyMunicipalityNumber = value,
        };

        var act = () => sut.ExecuteAsync(input, "u", default);

        await act.Should().ThrowAsync<ArgumentException>();
        repo.Added.Should().BeEmpty();
    }

    [Theory]
    [InlineData("")]
    [InlineData("123456789")]
    [InlineData("123456789A")]
    public async Task Rejects_invalid_upper_limit_provider_number_when_present(string value)
    {
        var repo = new FakeCertificateRepository();
        var sut = new RegisterCertificateUseCase(
            repo, new FakeUnitOfWork(), new FixedTimeProvider(DateTimeOffset.UnixEpoch));
        var input = MinimalInput(
            Guid.NewGuid(),
            "1234567890",
            new DateRange(new DateOnly(2026, 4, 1), new DateOnly(2027, 3, 31))) with
        {
            UpperLimitManagementProviderNumber = value,
        };

        var act = () => sut.ExecuteAsync(input, "u", default);

        await act.Should().ThrowAsync<ArgumentException>();
        repo.Added.Should().BeEmpty();
    }
}

internal sealed class FakeCertificateRepository : ICertificateRepository
{
    public List<Certificate> Added { get; } = new();
    public Task AddAsync(Certificate c, CancellationToken ct) { Added.Add(c); return Task.CompletedTask; }
    public Task<IReadOnlyList<Certificate>> ListByRecipientAsync(Guid rid, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<Certificate>>(Added.Where(c => c.RecipientId == rid).ToArray());
    public Task<IReadOnlyList<Certificate>> ListAllAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<Certificate>>(Added.ToArray());
    public Task<Certificate?> FindEffectiveAsync(Guid rid, DateOnly asOf, CancellationToken ct) =>
        Task.FromResult(Added.FirstOrDefault(c => c.RecipientId == rid && c.Validity.Contains(asOf)));
}
