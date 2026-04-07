using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SharpAdbClient;
using RookieMacOS.Core.Models;
using RookieMacOS.Core.Services;

namespace RookieMacOS.UI.ViewModels;

public partial class LogEntry(string level, string message, DateTime time) : ObservableObject
{
    public string   Level   { get; } = level;
    public string   Message { get; } = message;
    public string   Time    { get; } = time.ToString("HH:mm:ss");
    public bool     IsError => Level == "err";
}

public partial class InstalledPackage(string pkg, string displayName) : ObservableObject
{
    public string PackageName { get; } = pkg;
    public string DisplayName { get; } = displayName;
}

public partial class MainViewModel : ObservableObject, IDisposable
{
// ── Services ─────────────────────────────────────────────────────────────
    private readonly AdbService          _adb;
    private readonly InstallQueueService _queue;

    // ── State ─────────────────────────────────────────────────────────────────
    [ObservableProperty] private string       _selectedTab    = "install";
    [ObservableProperty] private DeviceInfo?  _selectedDevice;
    [ObservableProperty] private bool         _adbRunning;
    [ObservableProperty] private bool         _isScanning;
    [ObservableProperty] private bool         _isBusy;

    // Install tab
    [ObservableProperty] private bool _optDowngrade;
    [ObservableProperty] private bool _optKeepData;
    [ObservableProperty] private bool _optGrantPerms = true;
    [ObservableProperty] private bool _optNoVerify;
    [ObservableProperty] private bool _optContinueOnError = true;

    // Wireless tab
    [ObservableProperty] private string _wirelessIp       = "192.168.";
    [ObservableProperty] private int    _wirelessPort      = 5555;
    [ObservableProperty] private string _wirelessStatus    = "idle";   // idle|connecting|connected|error
    [ObservableProperty] private string _wirelessError     = "";

    // Apps tab
    [ObservableProperty] private string _packageSearch    = "";
    [ObservableProperty] private bool   _showSystemApps;

    // Terminal tab
    [ObservableProperty] private string _shellInput  = "";
    [ObservableProperty] private string _shellOutput = "";

    // Settings
    [ObservableProperty] private string _adbPath = "/usr/local/bin/adb";

    // Collections
    public ObservableCollection<DeviceInfo>      Devices          { get; } = [];
    public ObservableCollection<InstallJob>      InstallJobs      { get; }
    public ObservableCollection<InstalledPackage> InstalledPackages { get; } = [];
    public ObservableCollection<LogEntry>        Logs             { get; } = [];

    public int ErrorCount => Logs.Count(l => l.IsError);

    // ── Construction ─────────────────────────────────────────────────────────
    public MainViewModel()
    {
        _adb   = new AdbService();
        _queue = new InstallQueueService(_adb);

        InstallJobs = _queue.Jobs;

        _adb.Log            += OnAdbLog;
        _adb.DevicesChanged += OnDevicesChanged;
        _queue.JobCompleted  += (_, job) => AddLog("info", $"Job done: {job.FileName} → {job.Status}");
        _queue.QueueCompleted += (_, _)  => AddLog("ok",   $"Batch complete — {InstallJobs.Count(j => j.Status == InstallStatus.Success)}/{InstallJobs.Count} succeeded");
    }

    // ── Startup ───────────────────────────────────────────────────────────────
    [RelayCommand]
    public async Task InitialiseAsync()
    {
        AdbRunning = await _adb.StartServerAsync();
        if (AdbRunning) await ScanDevicesAsync();
    }

    // ── Device Management ─────────────────────────────────────────────────────
    [RelayCommand]
    public async Task ScanDevicesAsync()
    {
        IsScanning = true;
        AddLog("info", "Scanning for ADB devices…");
        var devices = await _adb.RefreshDevicesAsync();
        IsScanning = false;
        if (!devices.Any()) AddLog("dim", "No devices found. Connect via USB or wireless.");
    }

    private void OnDevicesChanged(object? sender, DevicesChangedEventArgs e)
    {
        var incoming = e.Devices.ToList();

        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            var previouslySelectedSerial = SelectedDevice?.Serial;

            Devices.Clear();
            foreach (var d in incoming)
                Devices.Add(d);

            SelectedDevice =
                Devices.FirstOrDefault(d => d.Serial == previouslySelectedSerial)
                ?? Devices.FirstOrDefault(d => d.Status == DeviceStatus.Online)
                ?? Devices.FirstOrDefault();
        });
    }

    [RelayCommand]
    public async Task RebootDeviceAsync(string mode = "")
    {
        if (SelectedDevice is null) return;
        var device = GetSelectedDeviceData();
        if (device is null) return;
        await _adb.RebootAsync(device, mode);
    }

    // ── Install ───────────────────────────────────────────────────────────────
    [RelayCommand]
    public void AddApkFiles(IEnumerable<string> paths)
    {
        foreach (var path in paths)
        {
            if (path.EndsWith(".apk", StringComparison.OrdinalIgnoreCase) ||
                path.EndsWith(".xapk", StringComparison.OrdinalIgnoreCase))
                _queue.Enqueue(path);
        }
    }

    [RelayCommand]
    public async Task InstallAllAsync()
    {
        if (SelectedDevice is null || _queue.IsRunning) return;
        var device = GetSelectedDeviceData();
        if (device is null) return;

        AddLog("info", $"Starting batch install → {SelectedDevice.DisplayName}");
        await _queue.RunAsync(device, BuildOptions(), OptContinueOnError);
    }

    [RelayCommand]
    public async Task InstallSingleAsync(InstallJob job)
    {
        if (SelectedDevice is null) return;
        var device = GetSelectedDeviceData();
        if (device is null) return;

        // Reset and run just this one
        job.Status   = InstallStatus.Queued;
        job.Progress = 0;
        job.ErrorMessage = "";

        var (success, error) = await _adb.InstallApkAsync(
            device, job.ApkPath, BuildOptions(),
            new Progress<double>(p => job.Progress = p * 100));

        job.Status       = success ? InstallStatus.Success : InstallStatus.Failed;
        job.ErrorMessage = error;
    }

    [RelayCommand]
    public void RemoveJob(InstallJob job) => _queue.Remove(job);

    [RelayCommand]
    public void ClearCompleted() => _queue.ClearCompleted();

    [RelayCommand]
    public void CancelInstall() => _queue.Cancel();

    private InstallOptions BuildOptions() => new()
    {
        AllowReinstall   = true,
        AllowDowngrade   = OptDowngrade,
        KeepData         = OptKeepData,
        GrantPermissions = OptGrantPerms,
        NoVerify         = OptNoVerify,
    };

    // ── Apps Tab ───────────────────────────────────────────────────────────────
    [RelayCommand]
    public async Task RefreshPackagesAsync()
    {
        var device = GetSelectedDeviceData();
        if (device is null) return;

        IsBusy = true;
        AddLog("info", $"pm list packages{(ShowSystemApps ? "" : " -3")}");
        var packages = await _adb.GetPackagesAsync(device, userOnly: !ShowSystemApps);
        InstalledPackages.Clear();
        foreach (var pkg in packages.OrderBy(p => p))
        {
            var display = pkg.Split('.').LastOrDefault(pkg) ?? pkg;
            InstalledPackages.Add(new InstalledPackage(pkg, display));
        }
        AddLog("ok", $"{InstalledPackages.Count} packages");
        IsBusy = false;
    }

    [RelayCommand]
    public async Task UninstallPackageAsync(InstalledPackage pkg)
    {
        var device = GetSelectedDeviceData();
        if (device is null) return;
        var ok = await _adb.UninstallAsync(device, pkg.PackageName);
        if (ok) InstalledPackages.Remove(pkg);
    }

    // ── Wireless ──────────────────────────────────────────────────────────────
    [RelayCommand]
    public async Task WirelessConnectAsync()
    {
        if (string.IsNullOrWhiteSpace(WirelessIp)) return;
        WirelessStatus = "connecting";
        WirelessError  = "";

        var ok = await _adb.ConnectWirelessAsync(WirelessIp, WirelessPort);
        WirelessStatus = ok ? "connected" : "error";
        if (!ok) WirelessError = $"Could not connect to {WirelessIp}:{WirelessPort}";
    }

    [RelayCommand]
    public async Task WirelessDisconnectAsync()
    {
        await _adb.DisconnectWirelessAsync(WirelessIp, WirelessPort);
        WirelessStatus = "idle";
    }

    [RelayCommand]
    public async Task EnableTcpIpAsync()
    {
        var device = GetSelectedDeviceData();
        if (device is null) return;
        await _adb.EnableTcpIpAsync(device, WirelessPort);
    }

    // ── Terminal ──────────────────────────────────────────────────────────────
    [RelayCommand]
    public async Task RunShellAsync()
    {
        var device = GetSelectedDeviceData();
        if (device is null || string.IsNullOrWhiteSpace(ShellInput)) return;

        var cmd = ShellInput.Trim();
        ShellInput = "";

        // Strip "adb shell" prefix if typed
        var shellCmd = cmd.StartsWith("adb shell ") ? cmd[10..] : cmd;

        var output = await _adb.ShellAsync(device, shellCmd);
        ShellOutput = string.IsNullOrEmpty(output)
            ? ShellOutput + $"\n$ {shellCmd}\n(no output)"
            : ShellOutput + $"\n$ {shellCmd}\n{output}";
    }

    [RelayCommand]
    public void ClearShell() => ShellOutput = "";

    // ── Logs ──────────────────────────────────────────────────────────────────
    [RelayCommand]
    public void ClearLogs()
    {
        Logs.Clear();
        OnPropertyChanged(nameof(ErrorCount));
    }

    private void AddLog(string level, string message)
    {
        Logs.Add(new LogEntry(level, message, DateTime.Now));
        OnPropertyChanged(nameof(ErrorCount));
    }

    private void OnAdbLog(object? sender, LogEventArgs e)
    {
        // Marshal to UI thread
        Avalonia.Threading.Dispatcher.UIThread.Post(() => AddLog(e.Level, e.Message));
    }

    // ── Helpers ───────────────────────────────────────────────────────────────
    private DeviceData? GetSelectedDeviceData()
    {
        if (SelectedDevice is null) return null;
        try
        {
            return new AdbClient()
                .GetDevices()
                .FirstOrDefault(d => d.Serial == SelectedDevice.Serial);
        }
        catch { return null; }
    }


    public void Dispose()
    {
        _adb.Dispose();
    }
}
