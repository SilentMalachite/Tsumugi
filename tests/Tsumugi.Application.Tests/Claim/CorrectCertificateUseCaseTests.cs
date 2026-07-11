using FluentAssertions;
using Tsumugi.Application.Abstractions;
using Tsumugi.Application.UseCases.Certificate;
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.Enums;
using Tsumugi.Domain.ValueObjects;
using Xunit;

namespace Tsumugi.Application.Tests.Claim;

public sealed class CorrectCertificateUseCaseTests
{
    private static readonly DateTimeOffset CorrectionTime = DateTimeOffset.UnixEpoch.AddHours(1);

    [Fact]
    public async Task Execute_appends_next_revision_and_copies_all_existing_values()
    {
        var root = Root() with
        {
            RecipientAddress = "東京都杉並区",
            RecipientGender = Gender.Female,
            GuardianName = "保護者",
            GuardianRelationship = "母",
            Disabilities = new DisabilityCategories(false, true, true, false),
            SupportCategory = SupportCategory.Category3,
            SupplyNotes = "既存値",
            ConsultationProviderName = "相談事業所",
            ConsultationProviderNumber = "1234567890",
            ConsultationStart = new DateOnly(2026, 4, 1),
            ConsultationEnd = new DateOnly(2027, 3, 31),
            PaymentBurden = PaymentBurdenCategory.General1,
            UpperLimitManagementProvider = "上限管理事業所",
            MealProvisionApplicable = true,
            HighCostBenefitApplicable = true,
        };
        var repo = new FakeCertificateRepository(root);
        var uow = new FakeUnitOfWork();
        var sut = new CorrectCertificateUseCase(repo, uow, new FixedTimeProvider(CorrectionTime));

        var dto = await sut.ExecuteAsync(
            Input(root.RootCertificateId, root.Id), "editor", default);

        repo.Certificates.Should().HaveCount(2);
        var correction = repo.Certificates[^1];
        correction.Should().BeEquivalentTo(root, options => options
            .Excluding(certificate => certificate.Id)
            .Excluding(certificate => certificate.Revision)
            .Excluding(certificate => certificate.ExpectedHeadCertificateId)
            .Excluding(certificate => certificate.MunicipalityNumber)
            .Excluding(certificate => certificate.SubsidyMunicipalityNumber)
            .Excluding(certificate => certificate.UpperLimitManagementProviderNumber)
            .Excluding(certificate => certificate.CreatedBy)
            .Excluding(certificate => certificate.CreatedAt)
            .Excluding(certificate => certificate.ConcurrencyToken));
        correction.Id.Should().NotBe(root.Id);
        correction.RootCertificateId.Should().Be(root.Id);
        correction.Revision.Should().Be(2);
        correction.ExpectedHeadCertificateId.Should().Be(root.Id);
        correction.MunicipalityNumber.Should().Be("131156");
        correction.SubsidyMunicipalityNumber.Should().Be("131157");
        correction.UpperLimitManagementProviderNumber.Should().Be("1234567890");
        correction.CreatedBy.Should().Be("editor");
        correction.CreatedAt.Should().Be(CorrectionTime);
        dto.Id.Should().Be(correction.Id);
        dto.Revision.Should().Be(2);
        uow.SaveCalls.Should().Be(1);
    }

    [Fact]
    public async Task Execute_rejects_stale_expected_head()
    {
        var root = Root();
        var current = Correction(root);
        var repo = new FakeCertificateRepository(root, current);
        var sut = Sut(repo);

        var act = () => sut.ExecuteAsync(
            Input(root.RootCertificateId, root.Id), "editor", default);

        await act.Should().ThrowAsync<InvalidOperationException>();
        repo.Certificates.Should().HaveCount(2);
    }

    [Fact]
    public async Task Execute_rejects_expected_head_from_another_root()
    {
        var selectedRoot = Root();
        var anotherRoot = Root();
        var repo = new FakeCertificateRepository(selectedRoot, anotherRoot);
        var sut = Sut(repo);

        var act = () => sut.ExecuteAsync(
            Input(selectedRoot.RootCertificateId, anotherRoot.Id), "editor", default);

        await act.Should().ThrowAsync<InvalidOperationException>();
        repo.Certificates.Should().HaveCount(2);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("12345")]
    [InlineData("1234567")]
    [InlineData("12A456")]
    [InlineData("１２３４５６")]
    public async Task Execute_rejects_municipality_number_that_is_not_six_ascii_digits(string value)
    {
        var root = Root();
        var repo = new FakeCertificateRepository(root);
        var sut = Sut(repo);
        var input = Input(root.RootCertificateId, root.Id) with { MunicipalityNumber = value };

        var act = () => sut.ExecuteAsync(input, "editor", default);

        await act.Should().ThrowAsync<ArgumentException>();
        repo.Certificates.Should().ContainSingle();
    }

    [Theory]
    [InlineData("")]
    [InlineData("12345")]
    [InlineData("12A456")]
    public async Task Execute_rejects_invalid_subsidy_municipality_number_when_present(string value)
    {
        var root = Root();
        var repo = new FakeCertificateRepository(root);
        var sut = Sut(repo);
        var input = Input(root.RootCertificateId, root.Id) with
        {
            SubsidyMunicipalityNumber = value,
        };

        var act = () => sut.ExecuteAsync(input, "editor", default);

        await act.Should().ThrowAsync<ArgumentException>();
        repo.Certificates.Should().ContainSingle();
    }

    [Theory]
    [InlineData("")]
    [InlineData("123456789")]
    [InlineData("123456789A")]
    public async Task Execute_rejects_invalid_upper_limit_provider_number_when_present(string value)
    {
        var root = Root();
        var repo = new FakeCertificateRepository(root);
        var sut = Sut(repo);
        var input = Input(root.RootCertificateId, root.Id) with
        {
            UpperLimitManagementProviderNumber = value,
        };

        var act = () => sut.ExecuteAsync(input, "editor", default);

        await act.Should().ThrowAsync<ArgumentException>();
        repo.Certificates.Should().ContainSingle();
    }

    [Fact]
    public async Task Execute_rejects_second_correction_from_same_expected_head()
    {
        var root = Root();
        var repo = new FakeCertificateRepository(root);
        var sut = Sut(repo);
        var input = Input(root.RootCertificateId, root.Id);

        await sut.ExecuteAsync(input, "first", default);
        var second = () => sut.ExecuteAsync(input, "second", default);

        await second.Should().ThrowAsync<InvalidOperationException>();
        repo.Certificates.Should().HaveCount(2);
    }

    private static CorrectCertificateUseCase Sut(FakeCertificateRepository repo) =>
        new(repo, new FakeUnitOfWork(), new FixedTimeProvider(CorrectionTime));

    private static CorrectCertificateInput Input(Guid rootId, Guid expectedHeadId) =>
        new(rootId, expectedHeadId, "131156")
        {
            SubsidyMunicipalityNumber = "131157",
            UpperLimitManagementProviderNumber = "1234567890",
        };

    private static Certificate Root()
    {
        var id = Guid.NewGuid();
        return Certificate.Create(
            id,
            Guid.NewGuid(),
            "1234567890",
            new DateRange(new DateOnly(2026, 4, 1), new DateOnly(2027, 3, 31)),
            supplyDays: 23,
            monthlyCostCap: 9_300,
            municipality: "杉並区",
            createdBy: "creator",
            createdAt: DateTimeOffset.UnixEpoch,
            concurrencyToken: Guid.NewGuid());
    }

    private static Certificate Correction(Certificate head) => head with
    {
        Id = Guid.NewGuid(),
        RootCertificateId = head.RootCertificateId,
        Revision = head.Revision + 1,
        ExpectedHeadCertificateId = head.Id,
        CreatedAt = head.CreatedAt.AddMinutes(1),
        ConcurrencyToken = Guid.NewGuid(),
    };

    private sealed class FakeCertificateRepository(params Certificate[] certificates)
        : ICertificateRepository
    {
        public List<Certificate> Certificates { get; } = [.. certificates];

        public Task AddAsync(Certificate certificate, CancellationToken ct)
        {
            Certificates.Add(certificate);
            return Task.CompletedTask;
        }

        public Task<Certificate?> FindHeadByRootIdAsync(Guid rootCertificateId, CancellationToken ct) =>
            Task.FromResult(Certificates
                .Where(certificate => certificate.RootCertificateId == rootCertificateId)
                .MaxBy(certificate => certificate.Revision));

        public Task<IReadOnlyList<Certificate>> ListByRecipientAsync(Guid recipientId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<Certificate>>(
                Certificates.Where(certificate => certificate.RecipientId == recipientId).ToArray());

        public Task<IReadOnlyList<Certificate>> ListAllAsync(CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<Certificate>>(Certificates.ToArray());

        public Task<Certificate?> FindEffectiveAsync(Guid recipientId, DateOnly asOf, CancellationToken ct) =>
            Task.FromResult(Certificates.FirstOrDefault(
                certificate => certificate.RecipientId == recipientId && certificate.Validity.Contains(asOf)));
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public int SaveCalls { get; private set; }

        public Task<int> SaveChangesAsync(CancellationToken ct)
        {
            SaveCalls++;
            return Task.FromResult(1);
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
