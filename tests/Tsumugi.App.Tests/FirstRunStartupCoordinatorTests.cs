using FluentAssertions;
using Tsumugi.App.Startup;
using Tsumugi.Application.UseCases.Office;
using Tsumugi.Domain.Entities;
using Tsumugi.Domain.Enums;
using Xunit;

namespace Tsumugi.App.Tests;

public sealed class FirstRunStartupCoordinatorTests
{
    private readonly InMemoryOfficeRepo _repo = new();

    private FirstRunStartupCoordinator NewSut() =>
        new(new ListOfficesUseCase(_repo));

    [Fact]
    public async Task DecideAsync_when_no_offices_returns_Wizard()
    {
        var sut = NewSut();

        var destination = await sut.DecideAsync();

        destination.Should().Be(FirstRunStartupDestination.Wizard);
    }

    [Fact]
    public async Task DecideAsync_when_office_exists_returns_Main()
    {
        _repo.Add(Office.Create(
            Guid.NewGuid(), "1234567890", "既存事業所",
            ServiceCategory.TypeB, RegionGrade.Grade4,
            "u", DateTimeOffset.UnixEpoch, Guid.NewGuid()));
        var sut = NewSut();

        var destination = await sut.DecideAsync();

        destination.Should().Be(FirstRunStartupDestination.Main);
    }

    [Fact]
    public async Task DecideAsync_propagates_repository_exception()
    {
        _repo.BeforeListAsync = _ => throw new InvalidOperationException("list failed");
        var sut = NewSut();

        var act = () => sut.DecideAsync();

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("list failed");
    }
}
