using System.ComponentModel;
using System.Drawing;
using System.Windows;
using System.Windows.Controls;
using H.NotifyIcon;
using H.NotifyIcon.Core;
using PulseBoost.ViewModels;
using Wpf.Ui.Controls;
using MenuItem = Wpf.Ui.Controls.MenuItem;

namespace PulseBoost.Views;

public partial class MainWindow : FluentWindow
{
    private readonly TaskbarIcon _trayIcon;
    private readonly MenuItem _boostMenuItem;
    private bool _isExplicitExit;

    public MainWindow()
    {
        InitializeComponent();

        var appIcon = System.Drawing.Icon.ExtractAssociatedIcon(Environment.ProcessPath!)
                      ?? SystemIcons.Application;

        _boostMenuItem = new MenuItem { Header = "Activate Boost" };
        _boostMenuItem.Click += (_, _) =>
        {
            if (DataContext is MainViewModel vm)
                vm.ToggleBoostCommand.Execute(null);
        };

        var ramMenuItem = new MenuItem { Header = "Boost RAM Now" };
        ramMenuItem.Click += (_, _) =>
        {
            if (DataContext is MainViewModel vm)
                vm.CleanRamOnlyCommand.Execute(null);
        };

        var openMenuItem = new MenuItem { Header = "Open PulseBoost" };
        openMenuItem.Click += (_, _) => RestoreFromTray();

        var exitMenuItem = new MenuItem { Header = "Exit Completely" };
        exitMenuItem.Click += (_, _) => ExitApplication();

        var contextMenu = new ContextMenu();
        contextMenu.Items.Add(openMenuItem);
        contextMenu.Items.Add(_boostMenuItem);
        contextMenu.Items.Add(ramMenuItem);
        contextMenu.Items.Add(new Separator());
        contextMenu.Items.Add(exitMenuItem);

        _trayIcon = new TaskbarIcon
        {
            Icon = appIcon,
            ToolTipText = "PulseBoost — Ready",
            ContextMenu = contextMenu
        };
        _trayIcon.ForceCreate();
        _trayIcon.TrayMouseDoubleClick += (_, _) => RestoreFromTray();

        if (DataContext is MainViewModel viewModel)
        {
            viewModel.BoostStateChanged += isBoosted =>
            {
                _boostMenuItem.Header = isBoosted ? "Restore Normal Mode" : "Activate Boost";
                _trayIcon.ToolTipText = isBoosted ? "PulseBoost — BOOST ACTIVE" : "PulseBoost — Ready";

                _trayIcon.ShowNotification(
                    title: "PulseBoost",
                    message: isBoosted
                        ? "Boost Mode activated! Power plan & RAM optimized."
                        : "System restored to normal mode.",
                    icon: NotificationIcon.None,
                    largeIcon: true);
            };
        }
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_isExplicitExit && DataContext is MainViewModel vm && vm.MinimizeToTrayOnClose)
        {
            e.Cancel = true;
            Hide();
            vm.SetTelemetryActive(false);

            _trayIcon.ShowNotification(
                title: "PulseBoost",
                message: "Running quietly in the system tray.",
                icon: NotificationIcon.None);
            return;
        }

        base.OnClosing(e);
    }

    private void RestoreFromTray()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
        if (DataContext is MainViewModel vm)
            vm.SetTelemetryActive(true);
    }

    private void ExitApplication()
    {
        _isExplicitExit = true;
        if (DataContext is MainViewModel vm)
            vm.Dispose();
        _trayIcon.Dispose();
        System.Windows.Application.Current.Shutdown();
    }
}