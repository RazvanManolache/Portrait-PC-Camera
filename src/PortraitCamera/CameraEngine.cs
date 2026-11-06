using OpenCvSharp;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PortraitCamera;

public record EngineStatus(string Message, bool Connected, bool Capturing, bool Paused,
    int People, int InputWidth, int InputHeight, double Fps, bool Registered, string Output = "Legacy", int OutputWidth = 720, int OutputHeight = 1280);

internal sealed class CameraEngine : IDisposable
{
    int Width = 720, Height = 1280;
    public volatile Settings Options = new();
    public volatile bool Preview;
    public volatile bool Paused;
    public event Action<BitmapSource>? Frame;
    public event Action<EngineStatus>? Status;
    public event Action? CallStarted;
    readonly CancellationTokenSource stop = new();
    Task? worker;
    public void Start() => worker = Task.Run(Run);
    void Run()
    {
        nint output = 0;
        nint windowsOutput = 0;
        VideoCapture? capture = null;
        try
        {
            Width = Settings.ValidWidth(Options.OutputWidth); Height = Width * 16 / 9;
            output = NativeCamera.scCreateCamera(Width, Height, 30);
            if (output == 0) throw new InvalidOperationException("Portrait Camera is already running. Close the other copy and try again.");
            if (OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000) && WindowsCameraOutput.Installed())
            {
                int hr = WindowsCameraOutput.pwStartSized(out windowsOutput, Width, Height);
                if (hr < 0) throw new InvalidOperationException($"Windows camera registration failed (0x{hr:X8}). Reinstall Portrait Camera to repair it.");
            }
            using var detector = new PersonDetector();
            using var raw = new Mat();
            using var rotated = new Mat();
            using var portrait = new Mat();
            using var slate = new Mat(Height, Width, MatType.CV_8UC3);
            var framing = new Framing();
            Rect[] subjects = [];
            var clock = Stopwatch.StartNew();
            double previous = 0, lastUse = -10, retryAt = 0, lastReport = -1, lastPreview = -1, lastGood = 0;
            int frameCount = 0, reportFrames = 0, failedReads = 0, rotation = -1;
            double reportTime = 0;
            bool wasConnected = false;
            string activeId = "", requestedId = "", message = "Ready for your next call";
            bool registered = windowsOutput != 0 || NativeCamera.List(true).Any(d => d.Name.StartsWith("Portrait Camera", StringComparison.Ordinal));
            while (!stop.IsCancellationRequested)
            {
                double now = clock.Elapsed.TotalSeconds;
                double dt = now - previous; previous = now;
                var options = Options;
                int requestedWidth = Settings.ValidWidth(options.OutputWidth);
                if (requestedWidth != Width)
                {
                    if (windowsOutput != 0) { WindowsCameraOutput.pwStop(windowsOutput); windowsOutput = 0; }
                    NativeCamera.scDeleteCamera(output); output = 0;
                    Width = requestedWidth; Height = Width * 16 / 9;
                    output = NativeCamera.scCreateCamera(Width, Height, 30);
                    if (output == 0) throw new InvalidOperationException("Could not change camera resolution. Restart Portrait Camera.");
                    if (WindowsCameraOutput.Installed())
                    {
                        int hr = WindowsCameraOutput.pwStartSized(out windowsOutput, Width, Height);
                        if (hr < 0) throw new InvalidOperationException($"Could not change Windows camera resolution (0x{hr:X8}). Restart Portrait Camera.");
                    }
                    slate.Create(Height, Width, MatType.CV_8UC3);
                    wasConnected = false;
                }
                bool connected = NativeCamera.scIsConnected(output) || (windowsOutput != 0 && WindowsCameraOutput.pwConnected(windowsOutput));
                if (connected && !wasConnected) CallStarted?.Invoke();
                wasConnected = connected;
                bool demand = (Preview || connected) && !Paused;
                if (demand) lastUse = now;
                if (requestedId != options.CameraId)
                {
                    requestedId = options.CameraId;
                    capture?.Dispose(); capture = null; activeId = ""; retryAt = 0;
                    subjects = []; framing.Reset();
                }
                if (capture != null && (Paused || now - lastUse > 2.5))
                {
                    capture.Dispose(); capture = null; activeId = ""; subjects = [];
                }
                if (capture == null && demand && now >= retryAt)
                {
                    retryAt = now + 3;
                    var devices = NativeCamera.List();
                    // Remember device identity, never silently switch to another camera after unplugging.
                    var device = string.IsNullOrEmpty(options.CameraId) ? devices.FirstOrDefault() : devices.FirstOrDefault(d => d.Id == options.CameraId);
                    if (device == null) message = "Camera disconnected. Plug it in or choose another.";
                    else
                    {
                        message = "Opening " + device.Name + "…";
                        Status?.Invoke(new(message, connected, false, Paused, 0, 0, 0, 0, registered));
                        var candidate = new VideoCapture();
                        try
                        {
                            if (candidate.Open(device.Index, VideoCaptureAPIs.DSHOW))
                            {
                                if (device.Id.Contains("usb#", StringComparison.OrdinalIgnoreCase))
                                    candidate.Set(VideoCaptureProperties.FourCC, VideoWriter.FourCC('M', 'J', 'P', 'G'));
                                candidate.Set(VideoCaptureProperties.FrameWidth, 1920);
                                candidate.Set(VideoCaptureProperties.FrameHeight, 1080);
                                candidate.Set(VideoCaptureProperties.Fps, 30);
                                capture = candidate; activeId = device.Id; failedReads = 0; lastGood = now; framing.Reset();
                            }
                            else { candidate.Dispose(); message = "Camera unavailable. Check webcam protection and other camera apps, or choose another source."; }
                        }
                        catch { candidate.Dispose(); throw; }
                    }
                }
                bool valid = false;
                if (capture != null && demand)
                {
                    if (capture.Read(raw) && !raw.Empty())
                    {
                        valid = true; failedReads = 0; lastGood = now;
                        switch (options.Rotation)
                        {
                            case 90: Cv2.Rotate(raw, rotated, RotateFlags.Rotate90Clockwise); break;
                            case 180: Cv2.Rotate(raw, rotated, RotateFlags.Rotate180); break;
                            case 270: Cv2.Rotate(raw, rotated, RotateFlags.Rotate90Counterclockwise); break;
                            default: raw.CopyTo(rotated); break;
                        }
                        if (rotation != options.Rotation) { subjects = []; framing.Reset(); rotation = options.Rotation; }
                        if (options.AutoFrame && frameCount % 6 == 0) subjects = detector.Detect(rotated);
                        if (!options.AutoFrame) subjects = [];
                        var roi = framing.Crop(rotated.Width, rotated.Height, options.Zoom, options.AutoFrame, options.Position, subjects, dt);
                        using (var region = new Mat(rotated, roi)) Cv2.Resize(region, portrait, new Size(Width, Height));
                        if (options.Mirror) Cv2.Flip(portrait, portrait, FlipMode.Y);
                        if (windowsOutput != 0) WindowsCameraOutput.pwSend(windowsOutput, portrait.Data);
                        NativeCamera.scSendFrame(output, portrait.Data);
                        frameCount++; reportFrames++;
                        message = connected ? "Your portrait camera is in use" : "Preview on · ready for a call";
                    }
                    else
                    {
                        message = "Camera unavailable. Check webcam protection and other camera apps, or choose another source.";
                        if (++failedReads >= 8 || now - lastGood > 2)
                        {
                            capture.Dispose(); capture = null; activeId = ""; retryAt = now + 2; subjects = [];
                        }
                    }
                }
                if (!valid)
                {
                    if (Paused) message = "Camera paused · your video is hidden";
                    else if (!demand) message = "Ready for your next call";
                    DrawSlate(slate, Paused ? "Camera paused" : demand ? "Camera unavailable" : "Ready when you are");
                    if (windowsOutput != 0) WindowsCameraOutput.pwSend(windowsOutput, slate.Data);
                    NativeCamera.scSendFrame(output, slate.Data);
                }
                if (Preview && now - lastPreview >= .10)
                {
                    var image = valid ? portrait : slate;
                    var bitmap = BitmapSource.Create(Width, Height, 96, 96, PixelFormats.Bgr24, null, image.Data,
                        (int)(image.Step() * image.Height), (int)image.Step());
                    bitmap.Freeze(); Frame?.Invoke(bitmap); lastPreview = now;
                }
                if (now - lastReport >= 1)
                {
                    registered = windowsOutput != 0 || NativeCamera.List(true).Any(d => d.Name.StartsWith("Portrait Camera", StringComparison.Ordinal));
                    var status = new EngineStatus(message, connected, valid, Paused, subjects.Length,
                        valid ? raw.Width : 0, valid ? raw.Height : 0, reportFrames / Math.Max(.01, now - reportTime), registered, windowsOutput != 0 ? "Windows camera" : "Legacy", Width, Height);
                    Status?.Invoke(status);
                    Directory.CreateDirectory(Settings.Folder);
                    File.WriteAllText(Path.Combine(Settings.Folder, "status.json"), JsonSerializer.Serialize(status));
                    lastReport = reportTime = now; reportFrames = 0;
                }
                // scSendFrame controls the output cadence even when the physical camera is idle.
            }
        }
        catch (Exception ex)
        {
            Directory.CreateDirectory(Settings.Folder);
            File.AppendAllText(Path.Combine(Settings.Folder, "errors.log"), DateTimeOffset.Now + " " + ex + Environment.NewLine);
            Status?.Invoke(new("Camera could not start: " + ex.Message, false, false, false, 0, 0, 0, 0, false));
        }
        finally
        {
            capture?.Dispose();
            if (windowsOutput != 0) WindowsCameraOutput.pwStop(windowsOutput);
            if (output != 0) NativeCamera.scDeleteCamera(output);
        }
    }
    internal static void DrawSlate(Mat frame, string message)
    {
        if (frame.Width != 720 || frame.Height != 1280)
        {
            using var full = new Mat(1280, 720, MatType.CV_8UC3);
            DrawSlate(full, message);
            Cv2.Resize(full, frame, frame.Size());
            return;
        }
        frame.SetTo(new Scalar(33, 23, 18));
        Cv2.Rectangle(frame, new Rect(270, 390, 180, 270), new Scalar(193, 224, 104), 5, LineTypes.AntiAlias);
        Cv2.Circle(frame, new OpenCvSharp.Point(360, 480), 28, new Scalar(193, 224, 104), -1, LineTypes.AntiAlias);
        Cv2.Ellipse(frame, new OpenCvSharp.Point(360, 575), new Size(52, 55), 0, 180, 360, new Scalar(193, 224, 104), 5, LineTypes.AntiAlias);
        PutCentered(frame, "PORTRAIT CAMERA", 745, .9, new Scalar(230, 235, 240));
        PutCentered(frame, message, 805, .65, new Scalar(150, 155, 165));
    }
    static void PutCentered(Mat image, string text, int y, double scale, Scalar color)
    {
        var size = Cv2.GetTextSize(text, HersheyFonts.HersheySimplex, scale, 1, out _);
        Cv2.PutText(image, text, new OpenCvSharp.Point((image.Width - size.Width) / 2, y), HersheyFonts.HersheySimplex, scale, color, 1, LineTypes.AntiAlias);
    }
    public void Dispose()
    {
        stop.Cancel();
        worker?.Wait(TimeSpan.FromSeconds(4));
        // Never delete the native sender from another thread while a frame is being written.
    }
}
