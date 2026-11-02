# Audio Switcher Modern

## Revision 4

Devices and microphone selection are now on the left, with output and microphone volume controls on the right. The popup is 700 pixels wide at 100% scaling and sizes its height from the visible content. Expanding or hiding the optional app mixer adds or removes only the space that its controls need. It still starts collapsed. The whole panel scrolls only when content cannot fit the available screen height.


## Revision 3

The main popup is now 420 × 680 at 100% display scaling, with roomier controls and corrected heading spacing. Output devices occupy a growing list, without a nested scrollbar. The app mixer starts hidden; select Show to open it and Hide to close it. Opening the mixer grows the popup when screen space permits. The overall panel can still scroll if an unusually long list exceeds the available screen height. Master and microphone volume remain directly accessible.


## Revision 2

Corrects the Windows device-collection interface ID that prevented device detection, keeps a reusable settings menu alive until application exit, and reduces the popup from 448 × 738 to 360 × 600 at 100% display scaling. App-mixer errors are isolated so they cannot stop microphone enumeration.

To update, Exit the existing Modern app through its tray menu, extract the updated ZIP, and run Build.cmd again. If startup with Windows was enabled and you move the program to a new folder, toggle that setting off and on from the new copy.

A standalone Windows tray audio controller, written for this request. It does not modify the uploaded AudioSwitcher.exe or import its configuration. This package includes editable C# source, a PowerShell build script, and no third-party dependencies. Windows 10/11 with .NET Framework 4.8 is the intended environment.

## Build and run

1. Extract this entire ZIP into a permanent folder, for example `Documents\AudioSwitcherModern`.
2. Double-click **Build.cmd**. This invokes the C# compiler already included with the Windows .NET Framework and launches the result. No administrator access, Visual Studio, package install, or internet download is required on a typical Windows 10/11 installation.

Alternatively, open PowerShell in the extracted folder and run:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Build.ps1 -Run
```

Subsequently, open **AudioSwitcherModern.exe** directly. Keep the executable in a permanent location before enabling startup with Windows. You can copy the executable alone elsewhere after building; the source files are only needed to rebuild it.

The execution-policy bypass applies to that PowerShell process; the script does not change your saved execution policy. If your device's managed policy blocks scripts, obey that policy.

## Controls

- Click an output device to make it the Windows default. By default this switches both regular and communications audio; Settings can turn communications switching off. A communications microphone changes with the selected input.
- The master slider controls the default output. The microphone selector and input slider control the default input, including mute.
- Type an exact percentage beside a slider and press Enter (or leave the field), drag the slider, or use the mouse wheel over it. Wheel steps are 2%; hold Shift for 1%. Focus a slider and use arrow keys for 1%, or Home/End for 0/100%.
- Select Show beside APP MIXER to reveal non-expired audio sessions on the **current default output**, including inactive sessions. Play sound in an app if it is missing. Separate sessions from one process appear as separate sliders. Apps explicitly routed to another output do not appear here.
- Star outputs to include them in quick switching. Right-click a device for a keyboard-accessible Favourite action. With no favourites saved, quick switching cycles all connected outputs.
- **Alt+Shift+A:** show/hide the popup. **Alt+Shift+S:** next favourite output. Shortcut collisions are reported in the popup footer. Shortcuts are fixed in this version; edit the source to change them.
- Click the tray icon to open/hide. Clicking elsewhere dismisses the popup unless pinned. Escape or the header's × hides it. Use the tray menu or Settings → Exit to quit.
- Settings → Start with Windows is optional, per user, and starts hidden in the tray. The app never enables it automatically.

## Responsiveness

The popup stays constructed while hidden. Audio enumeration and updates run on a dedicated background thread, and intermediate slider events are coalesced. Reopening displays the existing interface immediately and refreshes the device information asynchronously. A first launch still needs the .NET runtime to start; no literal zero-latency guarantee is made. The app polls approximately every 0.9 seconds while visible and stops polling when hidden. Quick switching performs a fresh device check.

## Validation and limitations

See `VALIDATION.md` for the checks actually performed. Windows hardware behavior, UI rendering at different DPI settings, hotkeys and measured startup speed require testing on your PC. This is a first version, not a drop-in upgrade of the old application. Existing favourites, custom hotkeys, startup-device rules and other settings are not migrated.

Windows' default-device switching uses the undocumented `IPolicyConfig` interface, a common approach in audio switchers. Future Windows versions could change this interface. Volume and session controls use Windows Core Audio APIs. Apps that deliberately stick to an output may need playback restarted after switching; exclusive-mode apps may bypass normal per-app mixing. This app does not redirect existing app sessions between devices or amplify beyond Windows' 100% setting.

The old app may remain installed, but quit it while testing this one to avoid conflicting shortcuts and device changes. If you later keep the new app, disable the old app's startup entry yourself.

Settings are stored at `%LOCALAPPDATA%\AudioSwitcherModern\settings.xml`; failures are logged to `error.log` in the same folder. There is no updater, telemetry, network access or elevation request in this app. The optional startup setting writes only `HKCU\Software\Microsoft\Windows\CurrentVersion\Run\AudioSwitcherModern`. Disable it before deleting or moving the executable.

To remove: turn off Start with Windows, Exit from the tray, then delete the program folder. Optionally delete the settings folder above.

## If something fails

If compilation fails, keep the PowerShell error text. If it builds but a control fails, check the small status line and `error.log`, and report the Windows version, action, and attached log. If Windows audio is unavailable, check the Windows Audio service and connect an audio device.

## API references

- https://learn.microsoft.com/en-us/windows/win32/api/mmdeviceapi/nn-mmdeviceapi-immdeviceenumerator
- https://learn.microsoft.com/en-us/windows/win32/api/endpointvolume/nn-endpointvolume-iaudioendpointvolume
- https://learn.microsoft.com/en-us/windows/win32/api/audiopolicy/nn-audiopolicy-iaudiosessionmanager2
- https://learn.microsoft.com/en-us/windows/win32/api/audiopolicy/nn-audiopolicy-isimpleaudiovolume
