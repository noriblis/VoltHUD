# Security and release trust

VoltHUD is an unsigned preview. Do not interpret a clean scan, a checksum, or open source as a guarantee of safety on every system.

## What the app does

- Reads battery, identity and feature-discovery data from local Logitech HID interfaces.
- Lists connected Bluetooth endpoints and reads Windows battery values or the standard Bluetooth Battery Service.
- Saves position, opacity, size, start-minimized, always-on-top, theme and notification preferences locally.
- Saves bounded, hashed device identities locally to suppress repeated low-battery warnings across restarts. Requests native Windows notifications only when enabled.
- Creates a diagnostic text file only when explicitly requested through `--diagnose`.

It does not require administrator privileges, install drivers or certificates, change security settings, remap inputs, register startup without an explicit opt-in, inject into processes, download code, self-update, or send telemetry. It contains no executable packer or code obfuscation. Read-only protocol queries are distinct from hardware settings commands; battery/feature/name reads are the only queries used.

## Signing and warnings

No publicly trusted code-signing certificate was available for this preview. The executable is **NotSigned**. New unsigned software may trigger SmartScreen reputation warnings or be blocked by Smart App Control or organizational policy. Even a valid signature does not guarantee immediate SmartScreen reputation. See [Microsoft's SmartScreen guidance](https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/smartscreen-reputation).

No security exclusions or disabled protections are needed or recommended. Users who require a signed publisher should wait for a signed release. A self-signed certificate is not a substitute for a publicly trusted publisher identity.

## Verify a download

Download only from this repository's Releases page. Compare the ZIP's SHA-256 with `SHA256SUMS.txt` from the same release, then extract it. `Get-FileHash .\VoltHUD-windows.zip -Algorithm SHA256` calculates the download's hash. A matching checksum verifies file integrity; it does not establish publisher identity or prove that every antivirus engine will accept the file.

The development build was scanned locally with Microsoft Defender. That result applies to the exact file scanned at that time. Release workflow builds are separate files and should be scanned separately. No “zero antivirus detections” guarantee is made.

To report a suspected issue, open an issue without including credentials, Bluetooth addresses, serial numbers, or other private information.
