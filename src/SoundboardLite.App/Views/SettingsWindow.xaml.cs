using SoundboardLite.App.ViewModels;
using Wpf.Ui.Controls;

namespace SoundboardLite.App.Views;

public partial class SettingsWindow : FluentWindow
{
    public SettingsWindow(SettingsViewModel viewModel)
    {
        DataContext = viewModel;
        InitializeComponent();

        // Setting DialogResult on a window shown via ShowDialog() closes it automatically.
        viewModel.RequestClose += (_, _) => DialogResult = viewModel.DialogResult;
        Loaded += async (_, _) => await viewModel.LoadAsync();
    }
}
