using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using SharpAdbClient;
using RookieMacOS.Core.Models;

namespace RookieMacOS.Core.Services;

public class LogEventArgs(string level, string message) : EventArgs
{
    public string Level   { get; } = level;  // ok | err | info | dim
    public string Message { get; } = message;
    public DateTime Time  { get; } = DateTime.Now;
}

public class DevicesChangedEventArgs(IReadOnlyList<DeviceInfo> devices) : EventArgs
{
    public IReadOnlyList<DeviceInfo> Devices { get; } = devices;
}

public class AdbService : IDisposable
{
    // ── Config ──────────────────────────────────────────────────────────────
    private readonly string    _adbPath;
    private readonly AdbClient _client;
    private readonly AdbServer _server;
    private DeviceMonitor?     _monitor;
    private bool               _disposed;

    public event EventHandler<LogEventArgs>?           Log;
    public event EventHandler<DevicesChangedEventArgs>? DevicesChanged;

    // ── Construction ─────────────────────────────────────────────────────────
    public AdbService(string adbPath = "")
    {
        // Try common macOS ADB locations
        _adbPath = ResolveAdbPath(adbPath);
        _server  = new AdbServer();
        _client  = new AdbClient();
    }

    private static string ResolveAdbPath(string hint)
    {
        if (!string.IsNullOrEmpty(hint) && File.Exists(hint)) return hint;

        string[] candidates =
        [
            "/usr/local/bin/adb",
            "/opt/homebrew/bin/adb",       // Apple Silicon Homebrew
            "/usr/bin/adb",
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "Library/Android/sdk/platform-tools/adb"),
        ];

        return candidates.FirstOrDefault(File.Exists) ?? "adb";
    }

    // ── Server ───────────────────────────────────────────────────────────────
    public async Task<bool> StartServerAsync()
    {
        try
        {
            Emit("info", $"Starting ADB server ({_adbPath})");
            var result = await Task.Run(() =>
                _server.StartServer(_adbPath, restartServerIfNewer: false));

            if (result == StartServerResult.AlreadyRunning)
                Emit("dim", "ADB server already running on :5037");
            else
                Emit("ok", "ADB server started on :5037");

            StartMonitor();
            return true;
        }
        catch (Exception ex)
        {
            Emit("err", $"Failed to start ADB server: {ex.Message}");
            return false;
        }
    }

    private void StartMonitor()
    {
        _monitor = new DeviceMonitor(new AdbSocket(new IPEndPoint(IPAddress.Loopback, AdbClient.AdbServerPort)));
        _monitor.DeviceChanged  += async (_, _) => await RefreshDevicesAsync();
        _monitor.DeviceConnected += async (_, _) => await RefreshDevicesAsync();
        _monitor.DeviceDisconnected += async (_, _) => await RefreshDevicesAsync();
        _monitor.Start();
    }

    // ── Device Enumeration ───────────────────────────────────────────────────
    public async Task<IReadOnlyList<DeviceInfo>> RefreshDevicesAsync()
    {
        try
        {
            var rawDevices = await Task.Run(() => _client.GetDevices());
            var infos = new List<DeviceInfo>();

            foreach (var d in rawDevices)
            {
                var info = new DeviceInfo
                {
                    Serial = d.Serial,
                    Status = d.State switch
                    {
                        DeviceState.Online       => DeviceStatus.Online,
                        DeviceState.Offline      => DeviceStatus.Offline,
                        DeviceState.Unauthorized => DeviceStatus.Unauthorized,
                        DeviceState.NoPermissions=> DeviceStatus.NoPermission,
                        _                        => DeviceStatus.Offline,
                    }
                };

                if (info.Status == DeviceStatus.Online)
                    await PopulateDeviceInfoAsync(info, d);

                infos.Add(info);
            }

            DevicesChanged?.Invoke(this, new DevicesChangedEventArgs(infos));
            return infos;
        }
        catch (Exception ex)
        {
            Emit("err", $"Device refresh failed: {ex.Message}");
            return [];
        }
    }

    private async Task PopulateDeviceInfoAsync(DeviceInfo info, DeviceData device)
    {
        info.Model          = await GetPropAsync(device, "ro.product.model");
        info.Manufacturer   = await GetPropAsync(device, "ro.product.manufacturer");
        info.AndroidVersion = await GetPropAsync(device, "ro.build.version.release");
        info.SdkVersion     = await GetPropAsync(device, "ro.build.version.sdk");
        info.BatteryLevel   = await GetBatteryLevelAsync(device);
        info.ResolveType();

        Emit("ok", $"{info.Serial} — {info.DisplayName} (Android {info.AndroidVersion}, battery {info.BatteryLevel}%)");
    }

    // ── Shell Helpers ────────────────────────────────────────────────────────
    public async Task<string> GetPropAsync(DeviceData device, string key)
    {
        try
        {
            var receiver = new ConsoleOutputReceiver();
            await Task.Run(() => _client.ExecuteRemoteCommand($"getprop {key}", device, receiver));
            return receiver.ToString().Trim();
        }
        catch { return ""; }
    }

    public async Task<string> ShellAsync(DeviceData device, string command)
    {
        try
        {
            var receiver = new ConsoleOutputReceiver();
            await Task.Run(() => _client.ExecuteRemoteCommand(command, device, receiver));
            Emit("info", $"[{device.Serial}] $ {command}");
            var output = receiver.ToString().Trim();
            return output;
        }
        catch (Exception ex)
        {
            Emit("err", $"Shell error: {ex.Message}");
            return "";
        }
    }

    public async Task<int> GetBatteryLevelAsync(DeviceData device)
    {
        try
        {
            var output = await ShellAsync(device, "dumpsys battery | grep level");
            var parts  = output.Split(':');
            return parts.Length > 1 && int.TryParse(parts[1].Trim(), out var level) ? level : -1;
        }
        catch { return -1; }
    }

    // ── APK Install ───────────────────────────────────────────────────────────
    public async Task<(bool success, string error)> InstallApkAsync(
        DeviceData device,
        string apkPath,
        InstallOptions options,
        IProgress<double>? progress = null,
        CancellationToken ct = default)
    {
        var flags = options.ToFlagString();
        Emit("info", $"adb -s {device.Serial} install {flags} \"{Path.GetFileName(apkPath)}\"");

        try
        {
            progress?.Report(0.1);
            var args = string.IsNullOrWhiteSpace(flags)
                ? $"-s \"{device.Serial}\" install \"{apkPath}\""
                : $"-s \"{device.Serial}\" install {flags} \"{apkPath}\"";

            var (exitCode, output) = await RunAdbAsync(args, ct);
            progress?.Report(1.0);

            if (exitCode == 0 && output.Contains("Success", StringComparison.OrdinalIgnoreCase))
            {
                Emit("ok", $"Success — {Path.GetFileNameWithoutExtension(apkPath)} installed on {device.Serial}");
                return (true, "");
            }

            var message = string.IsNullOrWhiteSpace(output)
                ? $"adb install failed with exit code {exitCode}"
                : output.Trim();
            Emit("err", $"Install failed: {message}");
            return (false, message);
        }
        catch (OperationCanceledException)
        {
            Emit("dim", "Install cancelled");
            return (false, "Cancelled");
        }
        catch (Exception ex)
        {
            Emit("err", $"Install error: {ex.Message}");
            return (false, ex.Message);
        }
    }

    // ── Wireless ADB ──────────────────────────────────────────────────────────
    public async Task<bool> ConnectWirelessAsync(string host, int port = 5555)
    {
        try
        {
            Emit("info", $"adb connect {host}:{port}");
            var (exitCode, output) = await RunAdbAsync($"connect {host}:{port}");

            if (exitCode == 0 &&
                (output.Contains("connected", StringComparison.OrdinalIgnoreCase) ||
                 output.Contains("already connected", StringComparison.OrdinalIgnoreCase)))
            {
                Emit("ok", $"Connected to {host}:{port}");
                await RefreshDevicesAsync();
                return true;
            }

            Emit("err", $"Connect failed: {output}");
            return false;
        }
        catch (Exception ex)
        {
            Emit("err", $"Wireless connect error: {ex.Message}");
            return false;
        }
    }

    public async Task DisconnectWirelessAsync(string host, int port = 5555)
    {
        try
        {
            Emit("info", $"adb disconnect {host}:{port}");
            var (_, output) = await RunAdbAsync($"disconnect {host}:{port}");
            Emit("ok", string.IsNullOrWhiteSpace(output) ? $"Disconnected {host}:{port}" : output.Trim());
            await RefreshDevicesAsync();
        }
        catch (Exception ex)
        {
            Emit("err", $"Disconnect error: {ex.Message}");
        }
    }

    public async Task<bool> EnableTcpIpAsync(DeviceData device, int port = 5555)
    {
        try
        {
            Emit("info", $"adb -s {device.Serial} tcpip {port}");
            var (exitCode, output) = await RunAdbAsync($"-s \"{device.Serial}\" tcpip {port}");

            if (exitCode == 0)
            {
                Emit("ok", string.IsNullOrWhiteSpace(output)
                    ? $"Device restarting in TCP/IP mode on :{port}"
                    : output.Trim());
                return true;
            }

            Emit("err", $"tcpip failed: {output}");
            return false;
        }
        catch (Exception ex)
        {
            Emit("err", $"tcpip failed: {ex.Message}");
            return false;
        }
    }

    // ── Device Actions ────────────────────────────────────────────────────────
    public async Task RebootAsync(DeviceData device, string mode = "")
    {
        try
        {
            Emit("info", $"adb reboot{(string.IsNullOrEmpty(mode) ? "" : " " + mode)}");
            await Task.Run(() =>
            {
                if (string.IsNullOrEmpty(mode))
                    _client.Reboot(device);
                else
                    _client.Reboot(mode, device);
            });
            Emit("dim", $"{device.Serial} rebooting{(string.IsNullOrEmpty(mode) ? "" : " into " + mode)}…");
        }
        catch (Exception ex)
        {
            Emit("err", $"Reboot failed: {ex.Message}");
        }
    }

    public async Task<List<string>> GetPackagesAsync(DeviceData device, bool userOnly = false)
    {
        var output = await ShellAsync(device, $"pm list packages{(userOnly ? " -3" : "")}");
        return output.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                     .Select(l => l.Replace("package:", "").Trim())
                     .Where(l => !string.IsNullOrEmpty(l))
                     .ToList();
    }

    public async Task<bool> UninstallAsync(DeviceData device, string packageName)
    {
        try
        {
            Emit("info", $"adb -s {device.Serial} uninstall {packageName}");
            var (exitCode, output) = await RunAdbAsync($"-s \"{device.Serial}\" uninstall {packageName}");

            if (exitCode == 0 && output.Contains("Success", StringComparison.OrdinalIgnoreCase))
            {
                Emit("ok", $"Uninstalled {packageName}");
                return true;
            }

            Emit("err", $"Uninstall failed: {output}");
            return false;
        }
        catch (Exception ex)
        {
            Emit("err", $"Uninstall failed: {ex.Message}");
            return false;
        }
    }

    private async Task<(int exitCode, string output)> RunAdbAsync(string arguments, CancellationToken ct = default)
    {
        var psi = new ProcessStartInfo
        {
            FileName = _adbPath,
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        using var process = new Process { StartInfo = psi };
        process.Start();

        var stdoutTask = process.StandardOutput.ReadToEndAsync(ct);
        var stderrTask = process.StandardError.ReadToEndAsync(ct);
        await process.WaitForExitAsync(ct);

        var stdout = await stdoutTask;
        var stderr = await stderrTask;
        var output = string.Join("\n", new[] { stdout, stderr }.Where(s => !string.IsNullOrWhiteSpace(s))).Trim();

        return (process.ExitCode, output);
    }

    // ── Logging ───────────────────────────────────────────────────────────────
    private void Emit(string level, string message) =>
        Log?.Invoke(this, new LogEventArgs(level, message));

    // ── Disposal ──────────────────────────────────────────────────────────────
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _monitor?.Dispose();
        GC.SuppressFinalize(this);
    }
}

// ── Install Options ───────────────────────────────────────────────────────────
public record InstallOptions
{
    public bool AllowReinstall  { get; init; } = true;
    public bool AllowDowngrade  { get; init; }
    public bool KeepData        { get; init; }
    public bool GrantPermissions{ get; init; }
    public bool NoVerify        { get; init; }

    public string ToFlagString()
    {
        var flags = new List<string>();
        if (AllowReinstall)   flags.Add("-r");
        if (AllowDowngrade)   flags.Add("-d");
        if (KeepData)         flags.Add("-k");
        if (GrantPermissions) flags.Add("-g");
        if (NoVerify)         flags.Add("--no-streaming");
        return string.Join(" ", flags);
    }
}
