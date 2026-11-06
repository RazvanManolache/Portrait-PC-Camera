# Portrait Camera

A small Windows tray app that turns a landscape webcam into a portrait camera for calls. Install once, choose **Portrait Camera** in your calling app, and use the popup to switch sources or adjust framing.

## Install and use

Download the installer from [GitHub Releases](https://github.com/RazvanManolache/Portrait-PC-Camera/releases/latest). Installation uses the normal Windows administrator prompt to register the camera for packaged apps and the Windows camera service. The installer includes the .NET runtime, both 64-bit and 32-bit camera components, OpenCV, and the detection model. No Python, OBS, model downloads, accounts, or separate runtime installer is needed. Desktop and Start menu shortcuts are created automatically.

1. Choose your source camera in the popup.
2. Select **Portrait Camera** in your calling app's camera settings. Keep your normal microphone selected there.
3. Leave **Keep me in frame** enabled for automatic framing, or disable it for manual positioning.
4. Use **Hide to tray** when done adjusting. The controls reappear when another app starts using the virtual camera; this behavior is optional.

Closing the window hides it to the tray. Right-click the tray icon to quit. By default an installed copy starts quietly with Windows; disable this in preferences if desired. The physical webcam is released shortly after both the preview and calling app stop using it. Pause releases the source and sends a privacy screen instead of retaining the last frame.

Camera discovery and format checks do not activate capture or open the controls. Automatic popups require sustained delivered frames (at least 1.5 seconds and 20 frames); short probes remain quiet. Paused mode never opens the controls automatically, and the paused setting survives restarts. Hiding the controls during a stream keeps them hidden through short interruptions. Apps that continuously request video are treated as streaming, including background previews; the camera cannot determine whether a remote call is in progress.

Local `activity.jsonl` diagnostics record stream transitions and reasons for opening the controls. Logs rotate at 256 KB and contain no images; they are not uploaded.

Choose **360 × 640**, **720 × 1280 (default)**, or **1080 × 1920** portrait output at a target of **30 fps**. Changing size restarts the virtual camera; select it again in the receiving app if needed. A larger output does not add detail beyond the source webcam and portrait crop. A horizontal webcam is cropped, not stretched. If you physically mount the webcam sideways, choose its orientation in the framing preferences. Mirror affects the outgoing video as well as the preview.

Startup is configured during installation, even if the app is not opened at the end of setup. Upgrades preserve an existing startup opt-out. The version is shown in the window. **Check updates** queries this repository's latest stable GitHub release. Automatic checks run on launch at most once per day and can be disabled in preferences. A newer release opens on GitHub when you choose **View update**; updates are never silently installed. GitHub receives the ordinary network request and app version, but no frames or camera settings.

## Compatibility and limits

- Windows 11, Intel or AMD 64-bit. ARM64 is not a supported build target.
- Provides a native Windows Media Foundation virtual camera, plus **Portrait Camera (Legacy)** for DirectShow clients. Windows may append “Windows Virtual Camera” to the native camera name. The native camera exists while the tray app runs.
- The calling service chooses its outgoing aspect ratio and the receiving phone's layout. Portrait Camera cannot force the other person's phone to fill its screen. Test your intended calling app before relying on this for a call.
- Detection runs locally on CPU. Face detection handles typical seated calls; full-body detection is a fallback. It follows one person, holds framing briefly when they disappear, then returns to center. This is not automatic group framing.
- A camera busy in another program may not be available directly. Close that program or choose its virtual camera output (for example NVIDIA Broadcast) as the input.
- Webcam protection in security software can block newly installed camera apps. If it identifies Portrait Camera as blocked, review its prompt yourself and allow access if you choose. The installer does not change security or camera-permission settings.
- Settings persist by device identity. Unplugging a selected camera does not silently switch to a different camera. Use Refresh after adding a device.
- The app must remain running in the tray for the virtual camera to supply video. Restart an already-open calling app if it does not refresh its camera list after installation.
- This is an unsigned early build. Public distribution still needs code signing and testing across calling apps and clean Windows machines.

Teams and WhatsApp camera selectors and phone-side video layout have not yet been verified with this version. The installed camera has passed local 720 × 1280 transport, upright color-pattern, pause-screen, consumer connection/disconnection, and positive detection-fixture tests. The native source passed both direct and installed Media Foundation 720 × 1280 video and colour tests. Camera enumeration inside Teams and Windows Camera package contexts sees the native camera; COM activation also succeeds. Live webcam input was observed at 1920 × 1080 and about 30 fps. These checks do not establish actual app-selector or remote call compatibility.

No frames are recorded or uploaded by the application. Settings, current capture status, and error diagnostics are stored under `%LOCALAPPDATA%\PortraitCamera`. The status file contains dimensions and state, not pictures. Uninstall through Windows Installed Apps; preferences are retained so reinstalling does not lose your camera choice.

## Build

Clone with `git clone --recurse-submodules https://github.com/RazvanManolache/Portrait-PC-Camera.git`. If already cloned, run `git submodule update --init --recursive`.

Build-machine requirements only: .NET 10 SDK, Visual Studio 2022 C++ tools with Windows SDK, CMake, and Inno Setup 6.4 or later.

```powershell
.\scripts\build.ps1 -InnoCompiler 'C:\Program Files (x86)\Inno Setup 6\ISCC.exe'
```

The release installer and SHA-256 checksum are created under `dist`. End users do not need these development tools. Native camera libraries and the pinned OpenCV package use static C++ runtimes; self-contained publish carries the .NET runtime.

## Validation

```powershell
# Geometry, bundled detectors, device enumeration, and virtual transport.
# Install first and quit the tray app before running this.
& "$env:ProgramFiles\Portrait Camera\PortraitCamera.exe" --self-test --virtual --output "$PWD\artifacts\validation"
```

Add `--physical` to test 60 frames from a USB camera; `--camera "Camera name"` chooses a specific source. Self-tests save JSON results and a synthetic virtual-camera test pattern, not webcam frames. Test results from this development machine are in `artifacts` and are not part of the installer.

## Implementation

WPF tray application; OpenCV DirectShow capture; bundled YuNet face detector and HOG person detector; smoothed normalized crop tracking; native Media Foundation NV12 output and legacy DirectShow output. The native source runs in the Windows camera service and receives frames through a local named pipe restricted to the owner and Windows services. The native camera is built from a pinned MIT-licensed Softcam snapshot with a unique COM class and private shared-memory names, so it does not overwrite an existing Softcam installation.

Softcam upstream: https://github.com/tshino/softcam at `6f5d8f9b5f7dc37a86a119b881bc75e1860fb3ec`.
Microsoft's registration layout: https://learn.microsoft.com/en-us/windows/win32/directshow/layout-of-the-registry-keys

See `THIRD-PARTY-NOTICES.txt` and bundled license files for dependencies.

## Release maintenance

Use stable version tags such as `v0.3.0`. Update the project version and installer version together, build, and attach the installer and `SHA256SUMS.txt` to a published GitHub Release. Drafts and prereleases are excluded from app update checks. The updater repository is defined in `src/PortraitCamera/Updates.cs`.
## GitHub builds

Open **Actions → Windows camera installer → Run workflow**. Each successful run
provides the Windows installer, SHA-256 checksum, source commit and offline
self-test report under **Artifacts → PortraitCamera-Windows-installer**.
Pushes and pull requests build automatically. Native x64/x86 components are built
from pinned submodules and the .NET runtime is included.

CI checks framing, bundled face detection, update parsing and camera enumeration
without registering a camera or installing the application. Physical-camera,
virtual-camera and Windows 11 installation checks still require a real machine.
