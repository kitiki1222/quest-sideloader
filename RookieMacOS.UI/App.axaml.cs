using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using RookieMacOS.UI.ViewModels;
using RookieMacOS.UI.Views;

namespace RookieMacOS.UI;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.ShutdownMode = ShutdownMode.OnMainWindowClose;

            var vm = new MainViewModel();
            var window = new MainWindow
            {
                DataContext = vm
            };

            window.Closed += (_, _) =>
            {
                vm.Dispose();
                desktop.Shutdown();
            };

            desktop.MainWindow = window;
        }

        base.OnFrameworkInitializationCompleted();
    }
}
