using ConstructionFinance.Desktop.Services;
using Shouldly;
using Xunit;

namespace ConstructionFinance.Desktop.Tests.Common;

public class NavigationServiceTests
{
    [Fact]
    public void NavigateTo_sets_CurrentContent()
    {
        var nav = new NavigationService();
        var vm = new object();

        nav.NavigateTo(vm);

        nav.CurrentContent.ShouldBe(vm);
    }

    [Fact]
    public void NavigateTo_raises_event()
    {
        var nav = new NavigationService();
        object? captured = null;
        nav.CurrentContentChanged += (_, c) => captured = c;

        nav.NavigateTo("X");

        captured.ShouldBe("X");
    }

    [Fact]
    public void GoBack_restores_previous_content()
    {
        var nav = new NavigationService();
        nav.NavigateTo("A");
        nav.NavigateTo("B");

        nav.GoBack();

        nav.CurrentContent.ShouldBe("A");
    }

    [Fact]
    public void GoBack_when_empty_stack_does_nothing()
    {
        var nav = new NavigationService();
        nav.NavigateTo("A");

        nav.GoBack();      // no previous → current stays
        nav.GoBack();      // still empty

        nav.CurrentContent.ShouldBe("A");
    }

    [Fact]
    public void CanGoBack_true_after_two_navigates()
    {
        var nav = new NavigationService();
        nav.CanGoBack.ShouldBeFalse();
        nav.NavigateTo("A");
        nav.CanGoBack.ShouldBeFalse();
        nav.NavigateTo("B");
        nav.CanGoBack.ShouldBeTrue();
    }
}
