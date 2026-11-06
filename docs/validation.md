# v0.3.0 validation

Tested on Windows 11 x64 on 2026-11-06.

- Direct and installed Windows Media Foundation output: 360 × 640, 720 × 1280, and 1080 × 1920. Each test read 32 upright colour-pattern frames and checked that replacement frames removed the previous picture.
- Local framing geometry, tracking, bundled detector initialization, update version comparisons, and invalid resolution fallback passed.
- Installed upgrade completed without requesting a reboot. Camera source DLLs use distinct filenames so existing consumers can finish using a previous component.
- Installer created Desktop and Start menu shortcuts pointing to the installed executable. The current-user startup entry points to the installed executable with `--background`.
- Compact WPF window visually inspected with version, resolution selector, camera selector, framing/mirror controls, pause, tray, preferences, and update control visible.
- Live 1920 × 1080 input observed at approximately 30 fps with 1080 × 1920 output selected.

These checks do not verify the receiving phone's layout, actual Teams/WhatsApp calls, every webcam, or every clean Windows installation. Startup registration was inspected; a full Windows reboot was not performed. This early installer is unsigned.

Local diagnostic logs and camera settings are excluded from the repository and release installer.
