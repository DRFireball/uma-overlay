using System.Windows;

namespace UmaCompanion.App;

/// <summary>
/// Application entry point. Builds the single <see cref="CompanionViewModel"/>, shows the window,
/// then starts the view model (locate master.mdb + open the live-state server). Startup failures
/// surface as status text in the window rather than crashing the app.
/// </summary>
public partial class App : Application
{
    private CompanionViewModel? _viewModel;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var viewModel = new CompanionViewModel();
        _viewModel = viewModel;

        var window = new MainWindow { DataContext = viewModel };
        window.Show();

        viewModel.Start();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _viewModel?.Dispose();
        base.OnExit(e);
    }
}
