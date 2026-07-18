using System.Windows;

namespace UmaCompanion.App;

/// <summary>The companion window. All state lives in <see cref="CompanionViewModel"/> (the DataContext).</summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }
}
