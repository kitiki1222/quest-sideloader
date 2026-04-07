using System;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;

namespace RookieMacOS.Core.Models;

public enum InstallStatus { Queued, Installing, Success, Failed }

public partial class InstallJob : ObservableObject
{
    public Guid   Id       { get; } = Guid.NewGuid();
    public string ApkPath  { get; init; } = "";
    public string FileName => Path.GetFileName(ApkPath);
    public long   FileSize { get; init; }
    public string FileSizeMb => FileSize > 0 ? $"{FileSize / 1_048_576.0:F1} MB" : "?";

    [ObservableProperty] private InstallStatus _status = InstallStatus.Queued;
    [ObservableProperty] private double        _progress;
    [ObservableProperty] private string        _errorMessage = "";
    [ObservableProperty] private string        _packageName  = "";

    public bool IsRunning => Status == InstallStatus.Installing;
    public bool IsDone    => Status is InstallStatus.Success or InstallStatus.Failed;
}
