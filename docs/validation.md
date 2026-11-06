# v0.3.1 validation

Tested on Windows 11 x64 on 2026-11-06.

- Installed legacy camera held open for discovery for 2.2 seconds without reporting any delivered frames or active use; subsequent video delivery, upright colours, pause slate, and disconnect detection passed.
- Direct and installed Windows Media Foundation tests passed at 360 × 640, 720 × 1280, and 1080 × 1920. Querying formats and selecting a stream for 1.8 seconds produced no video demand; actual frame reads produced demand and correct replacement frames.
- Popup policy tests passed for discovery, brief probes, repeated isolated probes, sustained streams on both outputs, short interruptions, a separate later stream, and paused streams.
- Installed upgrade completed without a reboot. The first installation attempt stopped mid-copy; rerunning the same installer completed successfully.
- Duplicate background launch left the controls hidden. Restarting with only `--background` preserved pause, left physical capture off, and kept the window hidden. Windows startup still points to the installed app with `--background`.
- Existing framing geometry, detector initialization, update-version comparisons, and resolution fallback checks passed.

These checks use synthetic frames and do not make calls or establish Teams/WhatsApp phone-side compatibility. Continuous background video previews still qualify as streaming. Local evidence is excluded from the repository.

## v0.3.0 validation

Tested on Windows 11 x64 on 2026-11-06.

- Direct and installed Windows Media Foundation output: 360 × 640, 720 × 1280, and 1080 × 1920. Each test read 32 upright colour-pattern frames and checked that replacement frames removed the previous picture.
- Local framing geometry, tracking, bundled detector initialization, update version comparisons, and invalid resolution fallback passed.
- Installed upgrade completed without requesting a reboot. Camera source DLLs use distinct filenames so existing consumers can finish using a previous component.
- Installer created Desktop and Start menu shortcuts pointing to the installed executable. The current-user startup entry points to the installed executable with `--background`.
- Compact WPF window visually inspected with version, resolution selector, camera selector, framing/mirror controls, pause, tray, preferences, and update control visible.
- Live 1920 × 1080 input observed at approximately 30 fps with 1080 × 1920 output selected.

These checks do not verify the receiving phone's layout, actual Teams/WhatsApp calls, every webcam, or every clean Windows installation. Startup registration was inspected; a full Windows reboot was not performed. This early installer is unsigned.

Local diagnostic logs and camera settings are excluded from the repository and release installer.
