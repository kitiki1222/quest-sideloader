using System;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SharpAdbClient;
using RookieMacOS.Core.Models;

namespace RookieMacOS.Core.Services;

public class InstallQueueService(AdbService adb)
{
    private CancellationTokenSource? _cts;

    public ObservableCollection<InstallJob> Jobs { get; } = [];
    public bool IsRunning => _cts is { IsCancellationRequested: false };

    public event EventHandler<InstallJob>? JobCompleted;
    public event EventHandler?             QueueCompleted;

    public void Enqueue(string apkPath)
    {
        if (Jobs.Any(j => j.ApkPath == apkPath && !j.IsDone)) return;
        var info = new FileInfo(apkPath);
        Jobs.Add(new InstallJob { ApkPath = apkPath, FileSize = info.Exists ? info.Length : 0 });
    }

    public void Remove(InstallJob job)
    {
        if (!job.IsRunning) Jobs.Remove(job);
    }

    public void ClearCompleted() =>
        Jobs.Where(j => j.IsDone).ToList().ForEach(j => Jobs.Remove(j));

    public async Task RunAsync(DeviceData device, InstallOptions options, bool continueOnError = true)
    {
        if (IsRunning) return;
        _cts = new CancellationTokenSource();

        var pending = Jobs.Where(j => j.Status == InstallStatus.Queued).ToList();

        foreach (var job in pending)
        {
            if (_cts.IsCancellationRequested) break;

            job.Status = InstallStatus.Installing;
            job.Progress = 0;

            var progress = new Progress<double>(p => job.Progress = p * 100);

            var (success, error) = await adb.InstallApkAsync(device, job.ApkPath, options, progress, _cts.Token);

            job.Status       = success ? InstallStatus.Success : InstallStatus.Failed;
            job.Progress     = success ? 100 : job.Progress;
            job.ErrorMessage = error;

            JobCompleted?.Invoke(this, job);

            if (!success && !continueOnError) break;
        }

        _cts = null;
        QueueCompleted?.Invoke(this, EventArgs.Empty);
    }

    public void Cancel() => _cts?.Cancel();
}
