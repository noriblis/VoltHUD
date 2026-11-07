# VoltHUD

<img src="assets/app-badge.png" alt="VoltHUD" width="440">

A small, quiet Windows desktop widget for Logitech and Bluetooth battery levels.

![VoltHUD preview](assets/preview.png)

## Download

Get **VoltHUD-windows.zip** from [GitHub Releases](https://github.com/noriblis/VoltHUD/releases). Extract it and open **VoltHUD.exe**. No installer, administrator access, account, or G HUB is required. Windows 10/11 with the built-in .NET Framework 4.8 is required. Windows on ARM has not been tested.

**This is an unsigned preview.** A clean antivirus scan cannot guarantee that antivirus or Windows SmartScreen will never show a warning. See [SECURITY.md](SECURITY.md). A signed release requires a trusted publisher certificate; this project does not install a certificate or change Windows protections.

## Use

- Drag the header to move the widget. Its position is saved.
- **⋯ → Devices** selects a peripheral; each entry includes its battery level when available.
- **⋯ → Always on top** keeps the widget above other windows.
- **−** hides the widget in the notification area. Double-click its battery icon, or launch VoltHUD again, to show it.
- **Refresh** or **F5** checks again. Automatic refresh runs every 60 seconds.
- **⋯ → Quit** exits, including the tray icon.

Blue accents use `#00B8FC`. A low numeric battery level uses an orange accent. Unknown battery levels display a dash. Coarse levels such as “Good” remain words; voltage-derived levels are explicitly labelled as estimates.

## Compatibility

VoltHUD discovers battery capabilities instead of maintaining a hardcoded list of supported model names. It targets Logitech mice, keyboards, headsets and other battery-powered peripherals plus connected Bluetooth devices from any brand. **It does not claim universal Logitech compatibility.** Some devices use proprietary formats that are not implemented or expose no usable battery information.

| Interface | Implemented reading | Verification |
| --- | --- | --- |
| Logitech HID++ 2.0 | Unified battery `0x1004`, status `0x1000` | PRO X 2 mouse verified over its receiver |
| Logitech HID++ voltage | `0x1001`, headset ADC `0x1F20` | Decoder tests; additional hardware testing needed |
| Logitech HID++ 1.0 | Read-only battery registers `0x0D` and `0x07` | Decoder tests; additional hardware testing needed |
| Logitech Centurion | Discovered direct/bridged battery feature `0x0104` | Decoder tests; transport needs hardware testing |
| Bluetooth, all brands | Windows battery property matched to connected classic/LE endpoints | Enumeration tested; battery hardware testing still needed |
| Bluetooth LE | Standard Battery Service `0x180F`, Battery Level `0x2A19` | Implemented; battery hardware testing still needed |

Receiver slots 1–6 and the direct USB device index are queried. HID++ vendor collections and known headset collection formats are used. Keyboards, trackballs and headsets are not filtered out. Device IDs distinguish identical models when the hardware provides a unit ID. Firmware without the documented battery features, unsupported proprietary headsets, and Bluetooth devices that do not expose battery information may have no reading.

Bluetooth entries are filtered by Windows' connected state, not merely by paired status. Windows-supplied battery values can be cached. Bluetooth normally does not provide a charging flag, so VoltHUD does not infer one. No Bluetooth pairing or radio settings are changed.

## Privacy

The app has no internet client, analytics, advertising, automatic updater, or startup registration. It reads local HID/Windows/Bluetooth battery interfaces. It only saves position and the always-on-top preference in `%LOCALAPPDATA%\VoltHUD\settings.txt`. Device identifiers are used locally for matching and never uploaded.

Optional local diagnostic: `VoltHUD.exe --diagnose diagnostic.txt`. This includes device names and battery information, but no serial numbers, device paths, or Bluetooth addresses. Review it before sharing. `--windowed` shows a taskbar entry. `--preview path.png` renders an illustrative preview using sample data.

## Build and test

On Windows 10/11, from the repository directory:

```powershell
.\test.ps1
.\build.ps1
```

The scripts use the Windows inbox C# compiler, .NET Framework assemblies, and Windows Runtime metadata. No third-party packages or SDK download is needed. The app is written in C# using WPF, a tray icon, and Windows' HID and Bluetooth APIs. Output: `dist\VoltHUD.exe`. Tests validate malformed battery data, feature formats, charging states, coarse-level handling and voltage interpolation bounds. They cannot replace hardware compatibility testing.

To publish a preview, run the **Windows release** workflow with a new version tag. The workflow tests and builds the same source, packages the executable and publishes SHA-256 checksums. See [RELEASE_NOTES.md](RELEASE_NOTES.md) for current limitations.

## Protocol references

- [Logitech HID++ documentation](https://github.com/Logitech/cpg-docs/tree/master/hidpp20)
- [Solaar protocol documentation and research](https://github.com/pwr-Solaar/Solaar)
- [HeadsetControl headset protocol research](https://github.com/Sapd/HeadsetControl)
- [HaloBattery protocol descriptions](https://github.com/HeyOkay/HaloBattery/blob/main/docs/protocols.md)
- [Microsoft Bluetooth device enumeration](https://learn.microsoft.com/en-us/windows/apps/develop/devices-sensors/enumerate-devices)

Independent project. Logitech and Logitech G are trademarks of Logitech Europe S.A. and/or its affiliates. VoltHUD is not affiliated with, sponsored by, or endorsed by Logitech. No Logitech logo artwork is included.

Code is licensed under the [MIT License](LICENSE). The badge and device artwork were supplied by the project owner for VoltHUD.
