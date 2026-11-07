# Local reliability and memory review — 7 November 2026

Nothing was published. This report covers source review, automated checks and the connected hardware available on this PC; it is not a guarantee that all possible bugs or hardware incompatibilities have been eliminated.

## Fixes

- Persist one-shot low-battery warning history across restarts. Refreshing, reconnecting, and disabling/re-enabling notifications preserve the warning latch. A valid reported recovery to at least 15% rearms it. Charging and unknown readings do not trigger warnings; invalid percentages cannot rearm them.
- Write history using temporary-file replacement, only when it changes. Store hashes of device identities, bounded to 256 entries. If local storage is unavailable, session suppression still works, but restart suppression cannot be guaranteed.
- Serialize battery scans across VoltHUD processes. A live diagnostic overlapping widget startup exposed reply collisions, producing a truncated name and incorrect icon. The repaired overlap check identified the mouse correctly in the widget and all three diagnostic scans. Invalid UTF-8 and control characters in names are rejected so discovery can retry.
- Bound the device identity cache to 256 entries.
- Dispose the owned tray icon and make tray cleanup safe to call more than once. Rendering a preview followed by window closure previously could access an already disposed tray component.

## Validation

- Battery/alert suite: 2,558 checks passed, covering supported decoders, malformed replies, percentage bounds, warning thresholds, charging, repeat suppression, restart restoration, preference changes, identity casing, recovery and bounded history.
- UI stress: 100 settings open/close cycles and unchanged refreshes. Zero settings panels retained after test-only collection; managed memory increased by 178,328 bytes after warmup, with a handle delta of 0–1 across runs. Unchanged refreshes kept existing row controls.
- Hidden startup and hide-to-tray preserved the five-minute timer and live notification icon. Alert evaluation has no window-visibility condition.
- Atomic history creation/replacement, restart suppression and persisted recovery passed using an isolated test path, without sending artificial notifications or changing user preferences.
- Theme selection, Windows accent matching, size/opacity controls, switches, popup toggle handling, icon caching and thumbnail bounds passed the existing settings checks.
- Available hardware: three repeated scans found the connected PRO X 2 mouse at 77% with its identity ready. The reopened widget also showed the full correct name and mouse type.

## Memory observations

The previous running preview used 96.1 MiB working set, 87.6 MiB private memory and 575 handles. The reviewed app, after startup and the overlap diagnostic, used 97.9 MiB working set, 87.0 MiB private memory and 566 handles. These short samples vary with Windows caching and timing; they do not establish a meaningful overall memory reduction. The stress check found no sustained settings/control leak or handle growth. Production code does not force garbage collection or trim its working set.

Original icon sheets remain build inputs; the running app loads only prepared thumbnails. Shared frozen brushes and unchanged device rows remain cached. The .NET/WPF runtime is still a substantial baseline cost.

## Remaining validation limits

No active Bluetooth radio or additional peripheral models were available for hardware validation. Bluetooth service timeouts, real low-battery transitions and charging reports across other models still need testing on those devices. Windows controls whether native notifications are actually displayed. The executable remains an unsigned preview; no antivirus or SmartScreen guarantee is made. No startup registration or Windows notification preferences were changed by this review.

Run `build.ps1`, `test.ps1` and `ui-test.ps1` locally to reproduce the build and automated suites. UI tests use simulated devices and isolated history files.
