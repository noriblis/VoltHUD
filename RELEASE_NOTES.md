# VoltHUD 0.1.1 preview

Portable desktop battery widget for Windows 10/11.

- Dark interface with blue accents, battery bar, device icons, saved position and an optional always-on-top mode.
- Multiple Logitech battery formats: HID++ unified/status/voltage, headset ADC, old battery registers, and Centurion feature discovery.
- Connected classic/LE Bluetooth peripherals from all brands, with Windows-reported battery values and standard BLE Battery Service support.
- Clear unknown/coarse/estimated battery states. Device selector and tray controls. No internet access, telemetry, account, driver installation, or administrator requirement.

**Hardware verified:** Logitech PRO X 2 mouse over its USB receiver.

**Preview limitations:** Other Logitech models and Bluetooth battery readings require hardware validation. Proprietary battery protocols outside the implemented interfaces are not supported. Bluetooth battery/charging data depends on each device and Windows; unavailable values are not invented. This release is **unsigned**, so SmartScreen or other security warnings remain possible. See README.md and SECURITY.md for details.

Extract VoltHUD-windows.zip and open VoltHUD.exe. SHA256SUMS.txt verifies the release assets' integrity. The build uses Windows inbox components and contains no third-party runtime bundle.
