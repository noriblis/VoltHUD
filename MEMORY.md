# Memory cleanup — local measurements

Measured on this PC with one Logitech PRO X 2, the same saved widget settings, and no active Bluetooth radio. These are process measurements, not a guarantee for other devices or PCs. No forced garbage collection or working-set trimming was used in the app or measurement.

| Measurement | Baseline | Optimized, first idle check | Optimized, final check |
| --- | ---: | ---: | ---: |
| Working set | 114.5 MiB | 94.4 MiB | 102.1 MiB |
| Private memory | 98.7 MiB | 86.7 MiB | 90.2 MiB |

The final check used about 11% less working-set memory and 9% less private memory. Memory varies with Windows caching, interaction, connected device count and elapsed time; the first idle check showed a larger reduction.

## Changes

- Prepare individual, transparent icons during the build, capped at 256 pixels on the longest side. Load and cache only the icons actually displayed. The running app no longer decodes or retains the full artwork sheet or scans a full-size unknown-device image.
- Construct settings when opened, and release the popup content when closed.
- Share frozen colour brushes instead of allocating a new brush for each row or resource assignment.
- Keep existing device rows when a refresh returns the same visible data.
- Skip Bluetooth/WinRT scanning when no enabled radio exists. Enabling a radio or another device change schedules discovery again.
- Reuse the Windows device-property buffer and close completed Bluetooth async operations promptly.

The executable shrank from 2,163,200 to 1,386,496 bytes, about 36%. Original supplied artwork remains in the source assets for rebuilding. The Windows/.NET/WPF framework remains a substantial part of the process footprint.

Validation: battery decoder suite passed; settings, all theme selections, sliders and switches passed; icon cache and thumbnail limits passed; changed battery values still redraw while unchanged refreshes reuse rows. The final local widget automatically detected the mouse at 77%.

Nothing was published as part of this cleanup.
