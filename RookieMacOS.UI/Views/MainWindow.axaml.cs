using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using RookieMacOS.UI.ViewModels;

namespace RookieMacOS.UI.Views;

public partial class MainWindow : Window
{
    private MainViewModel Vm => (MainViewModel)DataContext!;

    public MainWindow()
    {
        InitializeComponent();

        // Wire drag-and-drop on the drop zone border
        AddHandler(DragDrop.DropEvent,    OnDrop);
        AddHandler(DragDrop.DragOverEvent, OnDragOver);
    }

    protected override async void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        Vm.BrowseRequested += (_, _) => OpenApkBrowser();
        await Vm.InitialiseAsync();
    }

    // ── Drag & Drop ───────────────────────────────────────────────────────────
    private static void OnDragOver(object? sender, DragEventArgs e)
    {
        e.DragEffects = e.Data.Contains(DataFormats.Files) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void OnDrop(object? sender, DragEventArgs e)
    {
        if (!e.Data.Contains(DataFormats.Files)) return;

        var files = e.Data.GetFiles()?
            .OfType<IStorageFile>()
            .Select(f => f.TryGetLocalPath())
            .Where(p => p is not null)
            .Cast<string>()
            .ToList() ?? [];

        Vm.AddApkFilesCommand.Execute(files);
        e.Handled = true;
    }

    // ── Browse button (called from XAML via BrowseApksCommand relay) ─────────
    // Registered as a RelayCommand in the ViewModel, but needs the StorageProvider
    // so we wire it here via a public method the VM can call back through.
    public async void OpenApkBrowser()
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title         = "Select APK files",
            AllowMultiple = true,
            FileTypeFilter =
            [
                new FilePickerFileType("Android Package") { Patterns = ["*.apk", "*.xapk"] },
            ]
        });

        var paths = files
            .Select(f => f.TryGetLocalPath())
            .Where(p => p is not null)
            .Cast<string>()
            .ToList();

        Vm.AddApkFilesCommand.Execute(paths);
    }

    // ── Tab navigation (tab-click closes any open menu etc.) ─────────────────
    // SelectedTab is bound from the VM; clicking nav buttons calls SelectTabCommand.
}
