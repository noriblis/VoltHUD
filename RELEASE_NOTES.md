# VoltHUD 0.1.1 preview

**Preview release.** Extract VoltHUD-windows.zip and open VoltHUD.exe on Windows 10/11 with .NET Framework 4.8.

- All detected devices appear together with artwork, names, battery levels and status.
- Custom settings with opacity, size, always-on-top, Dark/Light/Windows themes and live Windows accent colour.
- Five-minute refresh, manual refresh and automatic identity recovery.
- Optional Windows startup and minimized-to-tray startup; monitoring continues while hidden.
- Optional Windows warnings below 10%, once per device until recovery to 15%. Suppression survives refreshes, reconnects, preference changes and restarts.
- Connected classic/LE Bluetooth devices from any brand, Windows-reported levels and standard BLE Battery Service support, alongside supported HID++/Centurion readers.
- Clear unavailable/coarse/estimated values and an eight-device simulation.
- No internet client, telemetry, account, driver installation or administrator requirement.

Reliability fixes serialize device queries to prevent incorrect names/icons, reject corrupted identity text, bound caches and alert history, release closed settings controls and dispose tray resources safely.

Validation: 2,558 battery/alert checks passed. UI stress completed 100 settings open/close and refresh cycles without retained settings panels or sustained handle growth. Tray monitoring and persisted warning suppression passed isolated checks. See LOCAL_REVIEW.md for evidence and limits.

Hardware verified: PRO X 2 mouse over its receiver. Other peripheral models and real Bluetooth battery transitions still need hardware validation. Battery availability and charging information depend on the device and Windows; unavailable values are not invented.

This preview is unsigned. Windows controls notification delivery and may show SmartScreen or security warnings. SHA256SUMS.txt verifies integrity, not publisher identity or antivirus acceptance. See README.md and SECURITY.md.

Published with the project owner's authorization.
