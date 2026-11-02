# Validation

This is a first source release. The actual Windows executable has not been run against Windows audio hardware here.

## Passed

Revision 4 additions:

- Compiled the two-column production source with warnings treated as errors.
- Rendered and inspected both the collapsed and expanded mixer layouts with sample devices and sessions.
- Verified the collapsed height returns exactly after Show/Hide and that unused body space is no more than 15 pixels in the layout harness.
- Reran the existing control, settings menu and worker checks.


Revision 3 additions:

- Compiled the revised production source with warnings treated as errors.
- Rendered and inspected the 420 × 680 layout with three sample outputs and a collapsed app mixer.
- Confirmed both device and mixer lists have nested scrolling disabled.
- Tested Show/Hide, mixer expansion within screen bounds, and return to the default view.
- Reran percentage input, stable polling, device reconnection, repeated settings menu and slider coalescing checks.


Revision 2 additions:

- Checked all 10 documented COM interface IDs and both activation IDs against Microsoft Windows SDK headers. Corrected `IMMDeviceCollection` to `0BD7A1BE-7A1A-44DB-8397-CC5392387B5E`. The undocumented policy interface is excluded from that comparison.
- Repeatedly opened and closed the persistent settings menu in the UI harness without disposing it during a close event.
- Rechecked the compact 360 × 600 layout and the earlier control/worker regressions.


- Unmodified production C# source compiled with Mono's C# compiler, restricted to C# 5, with optimization, the supplied Windows manifest and warnings treated as errors: zero errors and zero warnings. This checks compatibility with the older C# language level used by the Windows Framework compiler; the supplied PowerShell build itself still requires Windows.
- UI rendered under Mono in a virtual display using a temporary test harness with sample endpoint/session data. Only Windows-specific native window hooks and audio enumeration were substituted in the harness. Production files retain the actual Windows API calls.
- Header and footer controls fit within their panels. Inspected the rendered UI and corrected clipped buttons and a horizontal scrollbar.
- Typed percentage values clamp to 0–100, and invalid text restores the current slider value.
- A normal polling update preserves existing app volume controls rather than recreating them.
- No-device and device-reconnection snapshots disable and restore master controls and rebuild the mixer appropriately.
- A blocked worker followed by 100 pending changes for one slider produces one write with the final value, rather than replaying 100 old values.
- ZIP contents and XML manifest checked during packaging.

`Preview.png` is the rendered sample UI, not a screenshot from your PC. Windows fonts, scrollbar appearance, native menus and DPI rendering can differ.

## Windows checks still needed

1. Extract the ZIP, run Build.cmd and confirm the executable opens.
2. Switch between speakers/headphones; confirm default playback and optional communications devices in Windows settings.
3. Drag master volume, type a value then press Enter, and test mute. Change volume externally and verify it updates here after leaving the percentage field.
4. Play audio in a few apps and verify each app session's volume and mute. The mixer shows sessions on the default output only.
5. Select and mute/unmute the microphone, then unplug/reconnect a USB or Bluetooth device.
6. Star outputs, test Alt+Shift+S and Alt+Shift+A, hide/reopen from the tray and compare responsiveness.
7. Optionally enable Start with Windows and test on your next login. Disable it before deleting or moving the executable.
8. Check scaling on your display, particularly if you use multiple monitors with different DPI settings.

No measured Windows startup or reopen latency is claimed. Windows Core Audio COM behavior and the undocumented default-device policy interface remain unverified in this environment.
