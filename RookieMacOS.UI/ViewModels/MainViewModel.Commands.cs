// Append these RelayCommands to MainViewModel.cs (partial class extension)
// Or merge directly into MainViewModel.cs

using CommunityToolkit.Mvvm.Input;
using RookieMacOS.Core.Models;

namespace RookieMacOS.UI.ViewModels;

// These commands are declared as partial methods so the MVVM source generator picks them up.
// Add them inside the MainViewModel partial class body.

public partial class MainViewModel
{
    [RelayCommand]
    private void SelectTab(string tab) => SelectedTab = tab;

    [RelayCommand]
    private void SelectDevice(DeviceInfo device) => SelectedDevice = device;

    [RelayCommand]
    private void BrowseApks()
    {
        // Delegate to the window's OpenApkBrowser() — view locator pattern
        // The window subscribes to this via a weak event or direct reference.
        BrowseRequested?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private async Task TestAdbAsync()
    {
        AddLog("info", $"Testing ADB at {AdbPath}…");
        var ok = await _adb.StartServerAsync();
        if (ok)
            AddLog("ok", "ADB found and server running");
        else
            AddLog("err", $"ADB not found at {AdbPath}");
    }

    // View hooks
    public event EventHandler? BrowseRequested;
}
