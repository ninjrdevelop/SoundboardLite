using System.ComponentModel;
using SoundboardLite.App.ViewModels;
using Wpf.Ui.Controls;

namespace SoundboardLite.App.Views;

public partial class MainWindow : FluentWindow
{
    private readonly System.Windows.Forms.NotifyIcon _trayIcon;
    private bool _exitRequested;

    public MainWindow(MainViewModel viewModel)
    {
        DataContext = viewModel;
        InitializeComponent();

        _trayIcon = CreateTrayIcon();
        Closing += OnWindowClosing;
        Closed += (_, _) => _trayIcon.Dispose();
    }

    private System.Windows.Forms.NotifyIcon CreateTrayIcon()
    {
        var menu = new System.Windows.Forms.ContextMenuStrip();
        menu.Items.Add("Show Soundboard Lite", null, (_, _) => RestoreFromTray());
        menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => RequestExit());

        var icon = System.Drawing.Icon.ExtractAssociatedIcon(Environment.ProcessPath ?? "")
            ?? System.Drawing.SystemIcons.Application;

        var trayIcon = new System.Windows.Forms.NotifyIcon
        {
            Icon = icon,
            Text = "Soundboard Lite",
            Visible = true,
            ContextMenuStrip = menu,
        };
        trayIcon.DoubleClick += (_, _) => RestoreFromTray();
        return trayIcon;
    }

    private void RestoreFromTray()
    {
        Show();
        WindowState = System.Windows.WindowState.Normal;
        Activate();
    }

    /// <summary>Called by the tray menu's "Exit" item — the only path that actually closes the app;
    /// closing the window itself (titlebar X, Alt+F4) just minimizes to tray instead.</summary>
    private void RequestExit()
    {
        _exitRequested = true;
        System.Windows.Application.Current.Shutdown();
    }

    private void OnWindowClosing(object? sender, CancelEventArgs e)
    {
        if (_exitRequested) return;

        e.Cancel = true;
        Hide();
    }

    private void BoardRenameTextBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (sender is not System.Windows.Controls.TextBox { DataContext: BoardItemViewModel board }) return;

        if (e.Key == System.Windows.Input.Key.Enter)
        {
            board.CommitRenameCommand.Execute(null);
            e.Handled = true;
        }
        else if (e.Key == System.Windows.Input.Key.Escape)
        {
            board.CancelRenameCommand.Execute(null);
            e.Handled = true;
        }
    }

    private void BoardRenameTextBox_LostFocus(object sender, System.Windows.RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.TextBox { DataContext: BoardItemViewModel board })
        {
            board.CommitRenameCommand.Execute(null);
        }
    }

    private void BoardRenameTextBox_IsVisibleChanged(object sender, System.Windows.DependencyPropertyChangedEventArgs e)
    {
        if (sender is System.Windows.Controls.TextBox { IsVisible: true } textBox)
        {
            textBox.Focus();
            textBox.SelectAll();
        }
    }
}
