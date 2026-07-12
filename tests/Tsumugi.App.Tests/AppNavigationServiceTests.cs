using System.Runtime.CompilerServices;
using CommunityToolkit.Mvvm.Messaging;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Tsumugi.App;
using Tsumugi.App.Navigation;
using Tsumugi.App.ViewModels;
using Tsumugi.Application.Dtos;
using Tsumugi.Application.UseCases;
using Tsumugi.Application.UseCases.Certificate;
using Tsumugi.Application.UseCases.Recipient;
using Tsumugi.Domain.Enums;
using Tsumugi.Domain.ValueObjects;
using Tsumugi.Infrastructure.Persistence;

namespace Tsumugi.App.Tests;

public sealed class AppNavigationServiceTests
{
    [Fact]
    public void AppSection_values_match_current_and_future_tab_indices()
    {
        Enum.GetValues<AppSection>().Should().Equal(
            AppSection.RecipientList,
            AppSection.RecipientEdit,
            AppSection.DisabilityCertificate,
            AppSection.FaceSheet,
            AppSection.Certificate,
            AppSection.Contract,
            AppSection.DailyRecord,
            AppSection.Office,
            AppSection.OfficeCapability,
            AppSection.WorkRecord,
            AppSection.WageFundSettings,
            AppSection.RecipientHourlyRate,
            AppSection.WageAdjustment,
            AppSection.WageCalculation,
            AppSection.WageStatement,
            AppSection.ClaimInput,
            AppSection.ClaimPreparation);

        Enum.GetValues<AppSection>()
            .Select((section, index) => ((int)section, index))
            .Should().OnlyContain(item => item.Item1 == item.index);
    }

    [Fact]
    public void NavigationErrorCode_is_closed_and_explicit()
    {
        Enum.GetValues<NavigationErrorCode>().Should().Equal(
            NavigationErrorCode.NavigationTargetUnavailable,
            NavigationErrorCode.InvalidNavigationContext);
    }

    [Fact]
    public void MainWindow_binds_SelectedIndex_two_way_and_has_no_direct_index_writes()
    {
        var root = FindRepositoryRoot();
        var xaml = File.ReadAllText(Path.Combine(root, "src", "Tsumugi.App", "MainWindow.axaml"));
        var codeBehind = File.ReadAllText(
            Path.Combine(root, "src", "Tsumugi.App", "MainWindow.axaml.cs"));

        xaml.Should().Contain(
            "SelectedIndex=\"{Binding SelectedSection, Mode=TwoWay}\"");
        codeBehind.Should().NotContain("MainTabs.SelectedIndex");
        codeBehind.Should().NotContain("MainTabs.SelectionChanged");
        codeBehind.Should().Contain("AppSection.RecipientEdit");
        codeBehind.Should().Contain("AppSection.RecipientList");
        codeBehind.Should().Contain("nameof(MainViewModel.SelectedSection)");
    }

    [Fact]
    public void Navigation_service_and_messenger_are_scoped_while_main_view_model_is_transient()
    {
        var services = new ServiceCollection().AddTsumugiServices("Data Source=:memory:");

        services.Single(x => x.ServiceType == typeof(IAppNavigationService)).Lifetime
            .Should().Be(ServiceLifetime.Scoped);
        services.Single(x => x.ServiceType == typeof(IMessenger)).Lifetime
            .Should().Be(ServiceLifetime.Scoped);
        services.Single(x => x.ServiceType == typeof(MainViewModel)).Lifetime
            .Should().Be(ServiceLifetime.Transient);
    }

    [Fact]
    public async Task Navigation_service_does_not_strongly_retain_main_or_owned_view_models()
    {
        using var provider = (ServiceProvider)CompositionRoot.Build("Data Source=:memory:");
        using var scope = provider.CreateScope();
        var navigation = scope.ServiceProvider.GetRequiredService<IAppNavigationService>();
        var weakReferences = ResolveMainAndCaptureWeakReferences(scope.ServiceProvider);

        ForceFullGarbageCollection();

        weakReferences.Should().OnlyContain(
            item => !item.Reference.IsAlive,
            "the scoped navigation service must not strongly retain {0}",
            string.Join(", ", weakReferences.Select(item => item.Name)));
        var request = new NavigationRequest(AppSection.RecipientList);
        var result = await navigation.NavigateAsync(request);
        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(NavigationErrorCode.NavigationTargetUnavailable);
        result.Request.Should().BeSameAs(request);
    }

    [Fact]
    public void AppNavigationService_has_no_ViewModel_dependency()
    {
        var viewModelTypes = typeof(ViewModelBase).Assembly.GetTypes()
            .Where(type => type != typeof(ViewModelBase)
                && typeof(ViewModelBase).IsAssignableFrom(type))
            .ToHashSet();
        var implementation = typeof(AppNavigationService);

        implementation.GetConstructors()
            .SelectMany(constructor => constructor.GetParameters())
            .Select(parameter => parameter.ParameterType)
            .Intersect(viewModelTypes)
            .Should().BeEmpty();
        implementation.GetFields(
                System.Reflection.BindingFlags.Instance
                | System.Reflection.BindingFlags.Public
                | System.Reflection.BindingFlags.NonPublic)
            .Select(field => field.FieldType)
            .Intersect(viewModelTypes)
            .Should().BeEmpty();
    }

    [Fact]
    public void Navigation_target_view_models_do_not_reference_each_other()
    {
        Type[] targets =
        [
            typeof(CertificateViewModel),
            typeof(DailyRecordViewModel),
            typeof(OfficeViewModel),
        ];

        foreach (var target in targets)
        {
            var dependencies = target.GetConstructors()
                .SelectMany(constructor => constructor.GetParameters())
                .Select(parameter => parameter.ParameterType)
                .Concat(target.GetProperties().Select(property => property.PropertyType))
                .Concat(target.GetFields(
                        System.Reflection.BindingFlags.Instance
                        | System.Reflection.BindingFlags.Public
                        | System.Reflection.BindingFlags.NonPublic)
                    .Select(field => field.FieldType));

            dependencies.Should().NotContain(
                dependency => targets.Contains(dependency) && dependency != target,
                $"{target.Name} must not reference another navigation target ViewModel");
        }
    }

    [Fact]
    public async Task Typed_requests_dispatch_only_the_target_context_and_change_selection_on_success()
    {
        await using var fixture = await NavigationFixture.CreateAsync();
        var sut = fixture.Navigation;
        var main = fixture.Main;
        var serviceDate = new DateOnly(2026, 6, 12);
        var serviceMonth = new ServiceMonth(2026, 6);

        var certificateResult = await sut.NavigateAsync(new NavigationRequest(
            AppSection.Certificate,
            fixture.RecipientId,
            serviceDate,
            fixture.CertificateId));

        certificateResult.IsSuccess.Should().BeTrue();
        main.SelectedSection.Should().Be(AppSection.Certificate);
        main.Certificate.RecipientId.Should().Be(fixture.RecipientId);
        main.Certificate.SelectedRecipient!.Id.Should().Be(fixture.RecipientId);
        main.Certificate.AsOfDate.Should().Be(serviceDate);
        main.Certificate.SelectedCertificate!.Id.Should().Be(fixture.CertificateId);
        main.DailyRecord.RecipientId.Should().Be(Guid.Empty);
        main.Office.SelectedItem.Should().BeNull();

        var dailyResult = await sut.NavigateAsync(new NavigationRequest(
            AppSection.DailyRecord,
            fixture.RecipientId,
            serviceDate,
            ServiceMonth: serviceMonth));

        dailyResult.IsSuccess.Should().BeTrue();
        main.SelectedSection.Should().Be(AppSection.DailyRecord);
        main.DailyRecord.RecipientId.Should().Be(fixture.RecipientId);
        main.DailyRecord.SelectedRecipient!.Id.Should().Be(fixture.RecipientId);
        main.DailyRecord.Year.Should().Be(2026);
        main.DailyRecord.Month.Should().Be(6);
        main.Certificate.SelectedCertificate!.Id.Should().Be(fixture.CertificateId);
        main.Office.SelectedItem.Should().BeNull();

        var officeResult = await sut.NavigateAsync(new NavigationRequest(
            AppSection.Office,
            OfficeId: fixture.OfficeId));

        officeResult.IsSuccess.Should().BeTrue();
        main.SelectedSection.Should().Be(AppSection.Office);
        main.Office.SelectedItem!.Id.Should().Be(fixture.OfficeId);
        main.Certificate.SelectedCertificate!.Id.Should().Be(fixture.CertificateId);
        main.DailyRecord.RecipientId.Should().Be(fixture.RecipientId);
    }

    [Theory]
    [InlineData(AppSection.ClaimInput)]
    [InlineData(AppSection.ClaimPreparation)]
    public async Task Future_target_returns_closed_error_without_changing_selection_or_request(
        AppSection futureSection)
    {
        await using var fixture = await NavigationFixture.CreateAsync();
        fixture.Main.SelectedSection = AppSection.DailyRecord;
        var request = new NavigationRequest(
            futureSection,
            fixture.RecipientId,
            ServiceMonth: new ServiceMonth(2026, 6));

        var result = await fixture.Navigation.NavigateAsync(request);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(NavigationErrorCode.NavigationTargetUnavailable);
        result.Request.Should().BeSameAs(request);
        fixture.Main.SelectedSection.Should().Be(AppSection.DailyRecord);
        fixture.Main.LastNavigationResult.Should().BeSameAs(result);
        fixture.Main.LastNavigationResult!.Request.Should().BeSameAs(request);
    }

    [Fact]
    public async Task Invalid_context_returns_closed_error_without_changing_selection_or_request()
    {
        await using var fixture = await NavigationFixture.CreateAsync();
        fixture.Main.SelectedSection = AppSection.RecipientList;
        var request = new NavigationRequest(
            AppSection.Certificate,
            fixture.RecipientId,
            OfficeId: fixture.OfficeId);

        var result = await fixture.Navigation.NavigateAsync(request);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(NavigationErrorCode.InvalidNavigationContext);
        result.Request.Should().BeSameAs(request);
        fixture.Main.SelectedSection.Should().Be(AppSection.RecipientList);
        fixture.Main.LastNavigationResult.Should().BeSameAs(result);
    }

    [Fact]
    public async Task Asynchronous_context_load_failure_keeps_selection_and_original_request()
    {
        await using var fixture = await NavigationFixture.CreateAsync();
        fixture.Main.SelectedSection = AppSection.WageStatement;
        var request = new NavigationRequest(
            AppSection.Certificate,
            fixture.RecipientId,
            CertificateId: Guid.NewGuid());

        var result = await fixture.Navigation.NavigateAsync(request);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(NavigationErrorCode.InvalidNavigationContext);
        result.Request.Should().BeSameAs(request);
        fixture.Main.SelectedSection.Should().Be(AppSection.WageStatement);
        fixture.Main.LastNavigationResult.Should().BeSameAs(result);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null
            && !File.Exists(Path.Combine(directory.FullName, "Tsumugi.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new DirectoryNotFoundException("Repository root was not found.");
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (string Name, WeakReference Reference)[]
        ResolveMainAndCaptureWeakReferences(IServiceProvider services)
    {
        var main = services.GetRequiredService<MainViewModel>();
        return main.GetType().GetProperties()
            .Where(property => typeof(ViewModelBase).IsAssignableFrom(property.PropertyType))
            .Select(property => (
                property.Name,
                new WeakReference(property.GetValue(main)
                    ?? throw new InvalidOperationException($"{property.Name} was null."))))
            .Prepend((nameof(MainViewModel), new WeakReference(main)))
            .ToArray();
    }

    private static void ForceFullGarbageCollection()
    {
        for (var attempt = 0; attempt < 3; attempt++)
        {
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true, compacting: true);
            GC.WaitForPendingFinalizers();
        }
    }

    private sealed class NavigationFixture : IAsyncDisposable
    {
        private readonly ServiceProvider _provider;
        private readonly AsyncServiceScope _scope;
        private readonly string _dbPath;

        private NavigationFixture(
            ServiceProvider provider,
            AsyncServiceScope scope,
            string dbPath,
            MainViewModel main,
            IAppNavigationService navigation,
            Guid recipientId,
            Guid certificateId,
            Guid officeId)
        {
            _provider = provider;
            _scope = scope;
            _dbPath = dbPath;
            Main = main;
            Navigation = navigation;
            RecipientId = recipientId;
            CertificateId = certificateId;
            OfficeId = officeId;
        }

        public MainViewModel Main { get; }
        public IAppNavigationService Navigation { get; }
        public Guid RecipientId { get; }
        public Guid CertificateId { get; }
        public Guid OfficeId { get; }

        public static async Task<NavigationFixture> CreateAsync()
        {
            var dbPath = Path.Combine(
                Path.GetTempPath(),
                $"tsumugi-navigation-{Guid.NewGuid():N}.db");
            var provider = (ServiceProvider)CompositionRoot.Build($"Data Source={dbPath}");
            var scope = provider.CreateAsyncScope();

            await scope.ServiceProvider.GetRequiredService<TsumugiDbContext>()
                .Database.MigrateAsync();
            var recipient = await scope.ServiceProvider
                .GetRequiredService<RegisterRecipientUseCase>()
                .ExecuteAsync(
                    new RegisterRecipientInput(
                        "紡木 太郎",
                        "ツムギ タロウ",
                        new DateOnly(1990, 1, 1)),
                    "test",
                    default);
            var office = await scope.ServiceProvider
                .GetRequiredService<RegisterOfficeUseCase>()
                .ExecuteAsync(
                    "1234567890",
                    "Tsumugi事業所",
                    ServiceCategory.TypeB,
                    RegionGrade.None,
                    "test",
                    default);
            var (certificate, _) = await scope.ServiceProvider
                .GetRequiredService<RegisterCertificateUseCase>()
                .ExecuteAsync(
                    new RegisterCertificateInput(
                        recipient.Id,
                        "9876543210",
                        new DateRange(
                            new DateOnly(2026, 4, 1),
                            new DateOnly(2027, 3, 31)),
                        23,
                        9_300,
                        "杉並区")
                    {
                        MunicipalityNumber = "131156",
                    },
                    "test",
                    default);

            var main = scope.ServiceProvider.GetRequiredService<MainViewModel>();
            var navigation = scope.ServiceProvider.GetRequiredService<IAppNavigationService>();
            return new NavigationFixture(
                provider,
                scope,
                dbPath,
                main,
                navigation,
                recipient.Id,
                certificate.Id,
                office.Id);
        }

        public async ValueTask DisposeAsync()
        {
            await _scope.DisposeAsync();
            await _provider.DisposeAsync();
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            foreach (var path in new[] { _dbPath, _dbPath + "-shm", _dbPath + "-wal" })
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
        }
    }
}
