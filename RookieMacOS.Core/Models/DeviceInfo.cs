using System;

namespace RookieMacOS.Core.Models;

public enum DeviceType { Android, Quest2, Quest3, QuestPro, Unknown }
public enum DeviceStatus { Online, Offline, Unauthorized, NoPermission }

public class DeviceInfo
{
    public string Serial       { get; init; } = "";
    public string Model        { get; set; }  = "";
    public string Manufacturer { get; set; }  = "";
    public string AndroidVersion { get; set; } = "";
    public string SdkVersion   { get; set; }  = "";
    public int    BatteryLevel { get; set; }  = -1;
    public bool   IsWireless   => Serial.Contains(':');
    public DeviceStatus Status { get; set; }  = DeviceStatus.Online;
    public DeviceType   Type   { get; private set; } = DeviceType.Unknown;

    // Called after GetProp queries complete
    public void ResolveType()
    {
        var model = Model.ToLowerInvariant();
        var mfr   = Manufacturer.ToLowerInvariant();

        if (mfr is "oculus" or "meta" || model.Contains("quest"))
        {
            Type = model switch
            {
                var m when m.Contains("quest 3") || m.Contains("eureka") => DeviceType.Quest3,
                var m when m.Contains("quest pro")|| m.Contains("seacliff") => DeviceType.QuestPro,
                var m when m.Contains("quest 2") || m.Contains("hollywood") => DeviceType.Quest2,
                _ => DeviceType.Quest3 // default to latest for unknown Meta devices
            };
        }
        else
        {
            Type = DeviceType.Android;
        }
    }

    public string DisplayName => Type switch
    {
        DeviceType.Quest3   => "Meta Quest 3",
        DeviceType.Quest2   => "Meta Quest 2",
        DeviceType.QuestPro => "Meta Quest Pro",
        _                   => string.IsNullOrEmpty(Model) ? Serial : Model
    };

    public string TypeLabel => Type switch
    {
        DeviceType.Quest3   => "Quest 3",
        DeviceType.Quest2   => "Quest 2",
        DeviceType.QuestPro => "Quest Pro",
        DeviceType.Android  => $"Android {AndroidVersion}",
        _                   => "Unknown"
    };

    // For Quest devices, developer mode must be enabled in the Meta app
    public bool RequiresDeveloperModeNote => Type is DeviceType.Quest3 or DeviceType.Quest2 or DeviceType.QuestPro;
}
