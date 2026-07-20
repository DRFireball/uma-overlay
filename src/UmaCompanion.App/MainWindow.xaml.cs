using System.Windows;

namespace UmaCompanion.App;

/// <summary>The companion window. All state lives in <see cref="CompanionViewModel"/> (the DataContext).</summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private void OnShowMissing(object sender, RoutedEventArgs e)
        => (DataContext as CompanionViewModel)?.ShowMissingTab();

    private void OnShowWon(object sender, RoutedEventArgs e)
        => (DataContext as CompanionViewModel)?.ShowWonTab();

    private void OnResetWins(object sender, RoutedEventArgs e)
    {
        if (DataContext is not CompanionViewModel viewModel)
        {
            return;
        }
        var choice = MessageBox.Show(this,
            "Clear every recorded win? The missing-trophy list resets to full; " +
            "re-browsing the Trophy Room re-syncs it.",
            "Reset wins", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (choice == MessageBoxResult.Yes)
        {
            viewModel.ResetWins();
        }
    }
}
