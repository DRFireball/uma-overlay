using System.Windows;

namespace UmaCompanion.App.Tests;

/// <summary>
/// The Missing/Won tab is pure view state — no master.mdb needed. These guard the bits that are
/// easy to break silently: the two lists must never both be visible, and the tab captions must
/// re-raise when their counts change (otherwise "Missing (0)" sticks after a Trophy Room sync).
/// </summary>
public class CompanionViewModelTabTests
{
    [Fact]
    public void MissingTabIsSelectedInitially()
    {
        using var vm = new CompanionViewModel();

        Assert.False(vm.ShowWon);
        Assert.Equal(Visibility.Visible, vm.MissingListVisibility);
        Assert.Equal(Visibility.Collapsed, vm.WonListVisibility);
    }

    [Fact]
    public void ShowWonTab_SwapsWhichListIsVisible()
    {
        using var vm = new CompanionViewModel();

        vm.ShowWonTab();
        Assert.Equal(Visibility.Collapsed, vm.MissingListVisibility);
        Assert.Equal(Visibility.Visible, vm.WonListVisibility);

        vm.ShowMissingTab();
        Assert.Equal(Visibility.Visible, vm.MissingListVisibility);
        Assert.Equal(Visibility.Collapsed, vm.WonListVisibility);
    }

    [Fact]
    public void SwitchingTabs_NotifiesVisibilityAndTabColours()
    {
        using var vm = new CompanionViewModel();
        var raised = new List<string>();
        vm.PropertyChanged += (_, e) => raised.Add(e.PropertyName!);

        vm.ShowWonTab();

        Assert.Contains(nameof(vm.MissingListVisibility), raised);
        Assert.Contains(nameof(vm.WonListVisibility), raised);
        Assert.Contains(nameof(vm.MissingTabBackground), raised);
        Assert.Contains(nameof(vm.WonTabBackground), raised);
    }

    [Fact]
    public void SelectedTabUsesTheRaisedSurfaceColour()
    {
        using var vm = new CompanionViewModel();

        Assert.NotEqual(vm.MissingTabBackground, vm.WonTabBackground);
        var selected = vm.MissingTabBackground;

        vm.ShowWonTab();
        Assert.Equal(selected, vm.WonTabBackground);
    }

    [Fact]
    public void TabLabelsShowTheirCounts()
    {
        using var vm = new CompanionViewModel();

        Assert.Equal($"Missing ({vm.MissingCount})", vm.MissingTabLabel);
        Assert.Equal($"Won ({vm.WonCount})", vm.WonTabLabel);
    }
}
