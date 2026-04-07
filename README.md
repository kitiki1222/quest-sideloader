# RookieMacOS — Android & Quest Sideloader

A native macOS sideloader built with Avalonia / .NET 10. Supports all Android devices and Meta Quest 2/3/Pro via real ADB.

## Project Structure

```
RookieMacOS/
├── RookieMacOS.Core/
│   ├── Models/
│   │   ├── DeviceInfo.cs          ← Device model + Quest 3 detection
│   │   └── InstallJob.cs          ← Install queue job model
│   └── Services/
│       ├── AdbService.cs          ← All ADB operations (real SharpAdbClient)
│       └── InstallQueueService.cs ← Batch install queue
│
└── RookieMacOS.UI/
    ├── ViewModels/
    │   ├── MainViewModel.cs            ← All state + commands
    │   └── MainViewModel.Commands.cs  ← Tab/device/browse commands
    ├── Views/
    │   ├── MainWindow.axaml           ← Full UI (liquid glass dark theme)
    │   └── MainWindow.axaml.cs        ← Drag-drop, file picker
    └── Converters/
        └── Converters.cs              ← Status/battery/log color converters
```

## Setup

### 1. Install ADB

```bash
# Homebrew (recommended)
brew install android-platform-tools

# Verify
adb --version
```

### 2. NuGet dependencies

Make sure these are in your `.csproj`:

```xml
<!-- RookieMacOS.Core.csproj -->
<PackageReference Include="SharpAdbClient"               Version="2.3.23" />
<PackageReference Include="CommunityToolkit.Mvvm"        Version="8.3.2"  />

<!-- RookieMacOS.UI.csproj -->
<PackageReference Include="Avalonia"                     Version="11.3.12" />
<PackageReference Include="Avalonia.Desktop"             Version="11.3.12" />
<PackageReference Include="Avalonia.Themes.Fluent"       Version="11.3.12" />
<PackageReference Include="CommunityToolkit.Mvvm"        Version="8.3.2"  />
```

### 3. Register converters in App.axaml

```xml
<Application.Resources>
  <ResourceDictionary>
    <converters:DeviceStatusToColorConverter  x:Key="DeviceStatusToColorConverter"/>
    <converters:BatteryToColorConverter       x:Key="BatteryToColorConverter"/>
    <converters:InstallStatusToLabelConverter x:Key="InstallStatusToLabelConverter"/>
    <converters:InstallStatusToColorConverter x:Key="InstallStatusToColorConverter"/>
    <converters:IsQueuedConverter             x:Key="IsQueuedConverter"/>
    <converters:LogLevelToColorConverter      x:Key="LogLevelToColorConverter"/>
    <converters:LogLevelToTextColorConverter  x:Key="LogLevelToTextColorConverter"/>
  </ResourceDictionary>
</Application.Resources>
```

### 4. Wire BrowseRequested in MainWindow.axaml.cs

```csharp
protected override async void OnLoaded(RoutedEventArgs e)
{
    base.OnLoaded(e);
    Vm.BrowseRequested += (_, _) => OpenApkBrowser();
    await Vm.InitialiseAsync();
}
```

### 5. Build & run

```bash
dotnet build
dotnet run --project RookieMacOS.UI
```

## Quest 3 Setup

1. Install the **Meta app** on your phone and enable Developer Mode for your organization
2. Put on the Quest 3 — accept the developer prompt that appears in headset
3. Connect via USB-C — accept the "Allow USB debugging" RSA dialog in headset
4. The device appears in Sideloader as **Meta Quest 3**
5. To sideload: drag APK into Install tab → Install

### Quest-specific notes

- APKs must target arm64-v8a (Quest 3 is ARM64)
- OBB files: after installing, push OBB to `/sdcard/Android/obb/<package>/` via adb push
- Wireless ADB works on Quest 3 — use Enable tcpip → connect on same Wi-Fi
- Meta vendor USB ID: `0x2833` (handled automatically by ADB)

## ADB Path detection

`AdbService` tries these paths in order:
1. `/usr/local/bin/adb` (Intel Mac Homebrew)
2. `/opt/homebrew/bin/adb` (Apple Silicon Homebrew)
3. `/usr/bin/adb`
4. `~/Library/Android/sdk/platform-tools/adb` (Android Studio)

Override in Settings tab if yours is elsewhere.








by claude
