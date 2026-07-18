using System;
using System.Collections.Generic;
using System.IO;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Tsumugi.App;
using Tsumugi.App.ViewModels;
using Tsumugi.Application.Abstractions;
using Tsumugi.Application.Audit;
using Tsumugi.Application.Claim;
using Tsumugi.Application.UseCases;
using Tsumugi.Application.UseCases.Certificate;
using Tsumugi.Application.UseCases.Claim;
using Tsumugi.Application.UseCases.Recipient;
using Tsumugi.Application.UseCases.Wage;
using Tsumugi.Application.UseCases.WorkRecord;
using Tsumugi.Domain.Enums;
using Tsumugi.Domain.Logic.Wage;
using Tsumugi.Domain.ValueObjects;
using Tsumugi.Infrastructure.Persistence;
using Xunit;

namespace Tsumugi.App.Tests;

public sealed class CompositionRootTests
{
    [Fact]
    public void Claim_input_workspace_is_registered_with_the_master_backed_policy_provider()
    {
        var services = new ServiceCollection().AddTsumugiServices("Data Source=:memory:");

        services.Should().Contain(service => service.ServiceType == typeof(SetClaimInputUseCase));
        services.Should().Contain(service =>
            service.ServiceType == typeof(SetAverageWageAnnualEvidenceUseCase));
        services.Should().Contain(service =>
            service.ServiceType == typeof(SetCertificateClaimEvidenceUseCase));
        services.Should().Contain(service =>
            service.ServiceType == typeof(SetUpperLimitManagementStatementUseCase));
        services.Should().NotContain(service =>
            service.ServiceType == typeof(Tsumugi.Domain.Logic.Claim.OfficeClaimProfilePolicy));
        services.Should().Contain(service =>
            service.ServiceType == typeof(SetOfficeClaimProfileUseCase));
        services.Should().Contain(service =>
            service.ServiceType == typeof(QueryClaimInputWorkspaceUseCase));
        services.Should().Contain(service => service.ServiceType == typeof(ClaimInputViewModel));

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var main = scope.ServiceProvider.GetRequiredService<MainViewModel>();
        main.ClaimInput.Should().NotBeNull();
    }

    [Fact]
    public async Task Production_navigation_loads_empty_claim_input_workspace_without_policy_rows()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"tsumugi-claim-input-{Guid.NewGuid():N}.db");
        try
        {
            var services = new ServiceCollection().AddTsumugiServices($"Data Source={dbPath}");
            using var provider = services.BuildServiceProvider();
            using var scope = provider.CreateScope();
            await scope.ServiceProvider.GetRequiredService<TsumugiDbContext>().Database.MigrateAsync();
            var recipient = await scope.ServiceProvider.GetRequiredService<RegisterRecipientUseCase>()
                .ExecuteAsync(new RegisterRecipientInput(
                    "紡木 太郎", "ツムギ タロウ", new DateOnly(1990, 1, 1)), "test", default);
            var office = await scope.ServiceProvider.GetRequiredService<RegisterOfficeUseCase>()
                .ExecuteAsync("1234567890", "Tsumugi事業所", ServiceCategory.TypeB,
                    RegionGrade.None, "test", default);
            var (certificate, _) = await scope.ServiceProvider
                .GetRequiredService<RegisterCertificateUseCase>()
                .ExecuteAsync(new RegisterCertificateInput(
                    recipient.Id, "9876543210",
                    new DateRange(new DateOnly(2026, 4, 1), new DateOnly(2027, 3, 31)),
                    23, 9_300, "杉並区")
                {
                    MunicipalityNumber = "131156",
                }, "test", default);
            _ = scope.ServiceProvider.GetRequiredService<MainViewModel>();
            var navigation = scope.ServiceProvider.GetRequiredService<
                Tsumugi.App.Navigation.IAppNavigationService>();

            var result = await navigation.NavigateAsync(new Tsumugi.App.Navigation.NavigationRequest(
                Tsumugi.App.Navigation.AppSection.ClaimInput,
                recipient.Id,
                CertificateId: certificate.Id,
                OfficeId: office.Id,
                ServiceMonth: new ServiceMonth(2026, 6)));

            result.IsSuccess.Should().BeTrue();
            scope.ServiceProvider.GetRequiredService<MainViewModel>()
                .ClaimInput!.WorkspaceLoaded.Should().BeTrue();
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            foreach (var path in new[] { dbPath, dbPath + "-shm", dbPath + "-wal" })
                if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public async Task Claim_finalization_services_use_factory_local_context_and_production_codec_v1()
    {
        var services = new ServiceCollection().AddTsumugiServices("Data Source=:memory:");
        using var provider = services.BuildServiceProvider();
        using var firstScope = provider.CreateScope();
        using var secondScope = provider.CreateScope();

        var factory = firstScope.ServiceProvider.GetRequiredService<IDbContextFactory<TsumugiDbContext>>();
        var scopedContext = firstScope.ServiceProvider.GetRequiredService<TsumugiDbContext>();
        var repository = firstScope.ServiceProvider.GetRequiredService<IClaimBatchRepository>();
        var firstStore = firstScope.ServiceProvider.GetRequiredService<IClaimFinalizationStore>();
        var secondStore = secondScope.ServiceProvider.GetRequiredService<IClaimFinalizationStore>();
        var operationRegistry = firstScope.ServiceProvider
            .GetRequiredService<IClaimFinalizationOperationRegistry>();
        var auditFactory = firstScope.ServiceProvider.GetRequiredService<IClaimAuditEntryFactory>();
        var codecRegistry = firstScope.ServiceProvider
            .GetRequiredService<IClaimSnapshotValidationCodecRegistry>();
        await using var localContext = await factory.CreateDbContextAsync();

        repository.Should().NotBeNull();
        firstStore.Should().BeSameAs(secondStore);
        operationRegistry.Should().NotBeNull();
        auditFactory.Should().NotBeNull();
        localContext.Should().NotBeSameAs(scopedContext);
        codecRegistry.HasWriteSupport.Should().BeTrue();
        var codec = codecRegistry.Find("claim-snapshot-v1", "claim-snapshot-codec-v1");
        codec.Should().NotBeNull();
        codec!.CanWrite.Should().BeTrue();
        codecRegistry.Find("unknown-schema", "unknown-codec").Should().BeNull();
        firstStore.GetType().GetConstructors().Single().GetParameters()
            .Should().NotContain(parameter => parameter.ParameterType == typeof(TsumugiDbContext));
    }

    [Fact]
    public void Claim_master_provider_is_registered_as_an_eager_singleton_instance()
    {
        var services = new ServiceCollection().AddTsumugiServices("Data Source=:memory:");
        var descriptor = services.Single(service =>
            service.ServiceType == typeof(IClaimMasterProvider));
        var policyDescriptor = services.Single(service =>
            service.ServiceType == typeof(IOfficeClaimProfilePolicyProvider));

        descriptor.ImplementationInstance.Should().NotBeNull();
        descriptor.ImplementationType.Should().BeNull();
        descriptor.ImplementationFactory.Should().BeNull();
        policyDescriptor.ImplementationInstance.Should().BeSameAs(
            descriptor.ImplementationInstance);

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var resolved = scope.ServiceProvider.GetRequiredService<IClaimMasterProvider>();
        var resolvedPolicyProvider = scope.ServiceProvider
            .GetRequiredService<IOfficeClaimProfilePolicyProvider>();

        resolved.Should().BeSameAs(descriptor.ImplementationInstance);
        resolvedPolicyProvider.Should().BeSameAs(resolved);
        resolved.ResolveVersion(new ServiceMonth(2026, 6)).Version.Value
            .Should().Be("claim-master-r8-06");
    }

    [Fact]
    public void Claim_input_repositories_are_registered_as_scoped()
    {
        var services = new ServiceCollection().AddTsumugiServices("Data Source=:memory:");
        using var provider = services.BuildServiceProvider();
        using var firstScope = provider.CreateScope();
        using var secondScope = provider.CreateScope();

        AssertScoped<IClaimInputRepository>(firstScope, secondScope);
        AssertScoped<IIntensiveSupportEpisodeRepository>(firstScope, secondScope);
        AssertScoped<IAverageWageAnnualEvidenceRepository>(firstScope, secondScope);
        AssertScoped<IOfficeClaimProfileRepository>(firstScope, secondScope);
        AssertScoped<ICertificateClaimEvidenceRepository>(firstScope, secondScope);
        AssertScoped<IUpperLimitManagementStatementRepository>(firstScope, secondScope);
    }

    [Fact]
    public void Build_resolves_use_cases_from_root()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"tsumugi-ci-{Guid.NewGuid():N}.db");
        try
        {
            using var provider = (ServiceProvider)CompositionRoot.Build($"Data Source={dbPath}");
            using var scope = provider.CreateScope();

            scope.ServiceProvider.GetRequiredService<RegisterOfficeUseCase>().Should().NotBeNull();
            scope.ServiceProvider.GetRequiredService<BackupDatabaseUseCase>().Should().NotBeNull();

            // Phase 2 D-group use cases resolve
            scope.ServiceProvider.GetRequiredService<RecordWorkUseCase>().Should().NotBeNull();
            scope.ServiceProvider.GetRequiredService<SetWageFundUseCase>().Should().NotBeNull();
            scope.ServiceProvider.GetRequiredService<CalculateWagesUseCase>().Should().NotBeNull();
            scope.ServiceProvider.GetRequiredService<CloseWagesUseCase>().Should().NotBeNull();

            // Phase 2 strategies registered as IReadOnlyList<IWageMethodStrategy> (4 instances)
            var strategies = scope.ServiceProvider.GetRequiredService<IReadOnlyList<IWageMethodStrategy>>();
            strategies.Should().HaveCount(4);

            // Phase 2 report generator
            scope.ServiceProvider.GetRequiredService<IWageReportGenerator>().Should().NotBeNull();

            // Phase 2 ViewModels resolve
            scope.ServiceProvider.GetRequiredService<WorkRecordViewModel>().Should().NotBeNull();
            scope.ServiceProvider.GetRequiredService<WageFundSettingsViewModel>().Should().NotBeNull();
            scope.ServiceProvider.GetRequiredService<WageCalculationViewModel>().Should().NotBeNull();
            scope.ServiceProvider.GetRequiredService<WageStatementViewModel>().Should().NotBeNull();
            scope.ServiceProvider.GetRequiredService<MainViewModel>().Should().NotBeNull();

            // Phase 4 S0 use cases resolve
            scope.ServiceProvider.GetRequiredService<SetRecipientHourlyRateUseCase>().Should().NotBeNull();
            scope.ServiceProvider.GetRequiredService<QueryRecipientHourlyRateUseCase>().Should().NotBeNull();
            scope.ServiceProvider.GetRequiredService<RecordWageAdjustmentUseCase>().Should().NotBeNull();
            scope.ServiceProvider.GetRequiredService<QueryWageAdjustmentUseCase>().Should().NotBeNull();

            // Phase 4 S0 ViewModels resolve
            scope.ServiceProvider.GetRequiredService<RecipientHourlyRateViewModel>().Should().NotBeNull();
            scope.ServiceProvider.GetRequiredService<WageAdjustmentViewModel>().Should().NotBeNull();
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            foreach (var f in new[] { dbPath, dbPath + "-shm", dbPath + "-wal" })
                if (File.Exists(f)) File.Delete(f);
        }
    }

    [Fact]
    public void Infrastructure_is_swappable_via_service_collection()
    {
        // App は IOfficeRepository を抽象で消費する。テストで差し替え可能であることを示す。
        var services = new ServiceCollection().AddTsumugiServices("Data Source=:memory:");
        var fake = new FakeRepo();
        services.AddScoped<IOfficeRepository>(_ => fake);

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        scope.ServiceProvider.GetRequiredService<IOfficeRepository>().Should().BeSameAs(fake);
    }

    private static void AssertScoped<T>(IServiceScope firstScope, IServiceScope secondScope)
        where T : class
    {
        var first = firstScope.ServiceProvider.GetRequiredService<T>();
        var sameScope = firstScope.ServiceProvider.GetRequiredService<T>();
        var otherScope = secondScope.ServiceProvider.GetRequiredService<T>();

        first.Should().NotBeNull();
        sameScope.Should().NotBeNull().And.BeSameAs(first);
        otherScope.Should().NotBeNull().And.NotBeSameAs(first);
    }

    private sealed class FakeRepo : IOfficeRepository
    {
        public System.Threading.Tasks.Task AddAsync(Tsumugi.Domain.Entities.Office o, System.Threading.CancellationToken ct)
            => System.Threading.Tasks.Task.CompletedTask;
        public System.Threading.Tasks.Task<Tsumugi.Domain.Entities.Office?> FindByIdAsync(Guid id, System.Threading.CancellationToken ct)
            => System.Threading.Tasks.Task.FromResult<Tsumugi.Domain.Entities.Office?>(null);
        public System.Threading.Tasks.Task<Tsumugi.Domain.Entities.Office?> FindByNumberAsync(string n, System.Threading.CancellationToken ct)
            => System.Threading.Tasks.Task.FromResult<Tsumugi.Domain.Entities.Office?>(null);
        public System.Threading.Tasks.Task UpdateAsync(Tsumugi.Domain.Entities.Office o, System.Threading.CancellationToken ct)
            => System.Threading.Tasks.Task.CompletedTask;
        public System.Threading.Tasks.Task<System.Collections.Generic.IReadOnlyList<Tsumugi.Domain.Entities.Office>> ListAsync(System.Threading.CancellationToken ct)
            => System.Threading.Tasks.Task.FromResult<System.Collections.Generic.IReadOnlyList<Tsumugi.Domain.Entities.Office>>(System.Array.Empty<Tsumugi.Domain.Entities.Office>());
    }
}
