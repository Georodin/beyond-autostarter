# Beyond Autostarter

Beyond Autostarter is a Windows tray utility that monitors a list of executables and can relaunch them if they exit. It was built for Pangolin BEYOND and also works with other Windows desktop applications.

## Run on Windows

1. Download and extract the Windows x64 ZIP from [Releases](https://github.com/Georodin/beyond-autostarter/releases).
2. Run `BeyondAutoRestart.exe` with the accompanying configuration file in the same folder.
3. Choose **Add EXE** and use **Browse** in the new row to pick an executable. Repeat for each application you want to monitor.
4. Set each application's **First start after** delay and choose whether to **Start if stopped**. Select **Save** to apply the list and timing settings.
5. Enable **Start with Windows** to run the utility when you sign in. If you move the utility to another folder, turn this option off and back on to update the shortcut.

The Windows x64 package is self-contained and includes the .NET Desktop Runtime. Once configured, the utility starts in the notification area. Double-click its tray icon or choose **Open settings** to reopen the window. Close the window or choose **Exit** from the tray menu to stop monitoring.

**Check interval** controls how often each executable is checked. **First start after** controls the initial wait for that application. **Restart delay** gives an application time to come back after it stops. The utility checks the full executable path in the current Windows session and uses the executable's folder as its working directory.

**Stop the Explorer shell** is an optional kiosk setting and is off by default. While a monitored application is running, it can close the current desktop and taskbar. To restore them, use Task Manager to stop Beyond Autostarter and run `explorer.exe`.

Windows stores settings per user. Replacing the executable and configuration file in the same folder keeps the saved application list. A separate copy in another folder may need to be configured again.

## Build from source

Use the [.NET 10 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/10.0) on Windows.

```powershell
dotnet build .\BeyondAutoRestart_Project\BeyondAutoRestart.sln -c Release
dotnet publish .\BeyondAutoRestart_Project\BeyondAutoRestart.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o .\artifacts\win-x64
```
