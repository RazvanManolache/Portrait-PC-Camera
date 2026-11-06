using OpenCvSharp;
using System.Diagnostics;
using System.IO;
using System.Text.Json;

namespace PortraitCamera;

internal static class SelfTest
{
    public static int Run(string[] args)
    {
        string folder = args.SkipWhile(x => x != "--output").Skip(1).FirstOrDefault() ?? Path.Combine(Settings.Folder, "validation");
        Directory.CreateDirectory(folder);
        var checks = new List<object>();
        try
        {
            var current = new Version(0, 3, 0);
            Require(Updates.Parse("{\"tag_name\":\"v0.4.0\",\"draft\":false,\"prerelease\":false,\"html_url\":\"https://example.org\"}", current).ReleasePage?.Host == "github.com", "New releases use the configured GitHub repository");
            Require(Updates.Parse("{\"tag_name\":\"v0.3.0\",\"draft\":false,\"prerelease\":false}", current).ReleasePage == null, "Current version is not an update");
            Require(Updates.Parse("{\"tag_name\":\"v0.2.0\",\"draft\":false,\"prerelease\":false}", current).ReleasePage == null, "Older versions are not updates");
            Require(Updates.Parse("{\"tag_name\":\"v0.4.0\",\"draft\":false,\"prerelease\":true}", current).ReleasePage == null, "Preview releases are skipped");
            Require(Updates.Parse("{\"tag_name\":\"invalid\",\"draft\":false,\"prerelease\":false}", current).ReleasePage == null, "Invalid tags are rejected");
            Require(Settings.ValidWidth(999) == 720, "Invalid saved resolutions fall back to HD");
            checks.Add(new { test = "updates-and-resolution-settings", passed = true });
            if (args.Contains("--updates"))
                checks.Add(new { test = "github-update-endpoint", result = Updates.CheckAsync().GetAwaiter().GetResult() });
            var framing = new Framing();
            foreach (var size in new[] { new Size(1920, 1080), new Size(640, 480), new Size(1080, 1920), new Size(320, 240) })
            foreach (double zoom in new[] { 1d, 1.5, 2 })
            foreach (double position in new[] { 0d, .5, 1 })
            {
                var crop = framing.Crop(size.Width, size.Height, zoom, false, position, [], .033);
                Require(crop.X >= 0 && crop.Y >= 0 && crop.Right <= size.Width && crop.Bottom <= size.Height, "Crop stays inside source");
                Require(Math.Abs((double)crop.Width / crop.Height - 9d / 16) < .01, "Portrait aspect ratio");
            }
            framing.Reset();
            int initial = framing.Crop(1920, 1080, 1, true, .5, [], .033).X;
            Rect[] person = [new Rect(1600, 200, 160, 220)];
            int first = framing.Crop(1920, 1080, 1, true, .5, person, .033).X;
            Require(first > initial && first - initial < 100, "Tracking eases instead of jumping");
            Rect tracked = default;
            for (int i = 0; i < 100; i++) tracked = framing.Crop(1920, 1080, 1, true, .5, person, .033);
            Require(tracked.X > 1100, "Tracking follows a person near the edge");
            Rect returned = default;
            for (int i = 0; i < 240; i++) returned = framing.Crop(1920, 1080, 1, true, .5, [], .033);
            Require(Math.Abs(returned.X - initial) < 10, "Tracking recenters after losing a person");
            checks.Add(new { test = "framing", passed = true, combinations = 36 });
            using (var detector = new PersonDetector())
            using (var empty = new Mat(480, 640, MatType.CV_8UC3, Scalar.Black))
            {
                for (int i = 0; i < 3; i++) Require(detector.Detect(empty).Length == 0, "Blank image has no people");
                checks.Add(new { test = "bundled-face-and-body-detectors", passed = true });
            }
            var devices = NativeCamera.List(true);
            checks.Add(new { test = "enumeration", passed = true, cameras = devices.Select(d => d.Name).ToArray() });
            string? fixture = args.SkipWhile(a => a != "--image").Skip(1).FirstOrDefault();
            if (fixture != null)
            {
                using var image = Cv2.ImRead(fixture); using var detector = new PersonDetector();
                Require(!image.Empty(), "Detection fixture loads");
                int detected = detector.Detect(image).Length;
                Require(detected > 0, "A person is detected in the positive fixture");
                checks.Add(new { test = "positive-person-detection", passed = true, detected });
            }
            if (args.Contains("--physical"))
            {
                string? preferred = args.SkipWhile(a => a != "--camera").Skip(1).FirstOrDefault();
                var device = devices.FirstOrDefault(d => preferred != null && d.Name == preferred)
                    ?? devices.FirstOrDefault(d => d.Id.Contains("usb#", StringComparison.OrdinalIgnoreCase))
                    ?? devices.FirstOrDefault(d => d.Name != "Portrait Camera");
                Require(device != null, "A physical camera is available");
                using var capture = new VideoCapture(device!.Index, VideoCaptureAPIs.DSHOW);
                if (device.Id.Contains("usb#", StringComparison.OrdinalIgnoreCase))
                    capture.Set(VideoCaptureProperties.FourCC, VideoWriter.FourCC('M', 'J', 'P', 'G'));
                capture.Set(VideoCaptureProperties.FrameWidth, 1920); capture.Set(VideoCaptureProperties.FrameHeight, 1080);
                capture.Set(VideoCaptureProperties.Fps, 30);
                using var frame = new Mat(); using var detector = new PersonDetector();
                int count = 0, maxPeople = 0; double detectionMs = 0; var timer = Stopwatch.StartNew();
                while (count < 60 && timer.Elapsed < TimeSpan.FromSeconds(12))
                {
                    if (!capture.Read(frame) || frame.Empty()) continue;
                    if (count % 10 == 0) { var detectionTimer = Stopwatch.StartNew(); maxPeople = Math.Max(maxPeople, detector.Detect(frame).Length); detectionMs += detectionTimer.Elapsed.TotalMilliseconds; }
                    count++;
                }
                checks.Add(new { test = "physical-capture", passed = count == 60, camera = device.Name, frames = count,
                    width = frame.Width, height = frame.Height, elapsedSeconds = timer.Elapsed.TotalSeconds, detectionMs, maxPeople });
                Require(count == 60, "Physical camera delivers 60 frames");
            }
            if (args.Contains("--virtual")) VirtualTest(folder, checks, devices);
            File.WriteAllText(Path.Combine(folder, "results.json"), JsonSerializer.Serialize(new { passed = true, timestamp = DateTimeOffset.Now, checks }, new JsonSerializerOptions { WriteIndented = true }));
            return 0;
        }
        catch (Exception ex)
        {
            File.WriteAllText(Path.Combine(folder, "results.json"), JsonSerializer.Serialize(new { passed = false, error = ex.ToString(), checks }, new JsonSerializerOptions { WriteIndented = true }));
            return 1;
        }
    }
    static void VirtualTest(string folder, List<object> checks, List<CameraDevice> devices)
    {
        var device = devices.FirstOrDefault(d => d.Name == "Portrait Camera (Legacy)") ?? devices.FirstOrDefault(d => d.Name == "Portrait Camera");
        Require(device != null, "Virtual camera is installed");
        nint sender = NativeCamera.scCreateCamera(720, 1280, 30);
        Require(sender != 0, "Virtual sender starts");
        using var cancelled = new CancellationTokenSource();
        int paused = 0;
        var producer = Task.Run(() =>
        {
            using var image = new Mat(1280, 720, MatType.CV_8UC3, Scalar.Black);
            while (!cancelled.IsCancellationRequested)
            {
                if (Volatile.Read(ref paused) != 0) CameraEngine.DrawSlate(image, "Camera paused");
                else
                {
                    image.SetTo(new Scalar(30, 40, 50));
                    Cv2.Rectangle(image, new Rect(0, 0, 360, 640), new Scalar(0, 0, 255), -1);
                    Cv2.Rectangle(image, new Rect(360, 640, 360, 640), new Scalar(255, 0, 0), -1);
                    Cv2.PutText(image, "PORTRAIT 720 x 1280", new OpenCvSharp.Point(70, 680), HersheyFonts.HersheySimplex, 1, Scalar.White, 2);
                }
                NativeCamera.scSendFrame(sender, image.Data);
            }
        });
        try
        {
            Thread.Sleep(150);
            using (var receiver = new VideoCapture(device!.Index, VideoCaptureAPIs.DSHOW))
            using (var received = new Mat())
            {
                Require(receiver.IsOpened(), "Virtual camera can be opened by a separate capture client");
                for (int i = 0; i < 10; i++) Require(receiver.Read(received), "Virtual stream delivers frames");
                Require(received.Width == 720 && received.Height == 1280, "Negotiated portrait resolution");
                var topLeft = received.At<Vec3b>(100, 100); var bottomRight = received.At<Vec3b>(1000, 600);
                Require(topLeft.Item2 > 240 && topLeft.Item0 < 10 && bottomRight.Item0 > 240 && bottomRight.Item2 < 10, "Colors and orientation survive virtual transport");
                Require(NativeCamera.scIsConnected(sender), "Consumer demand is detected");
                Cv2.ImWrite(Path.Combine(folder, "virtual-camera-test.png"), received);
                Volatile.Write(ref paused, 1);
                for (int i = 0; i < 15; i++) receiver.Read(received);
                Require(received.At<Vec3b>(100, 100).Item2 < 100, "Pause replaces previous video");
            }
            var timeout = Stopwatch.StartNew();
            while (NativeCamera.scIsConnected(sender) && timeout.ElapsedMilliseconds < 4000) Thread.Sleep(100);
            Require(!NativeCamera.scIsConnected(sender), "Consumer disconnection is detected");
            checks.Add(new { test = "virtual-roundtrip", passed = true, width = 720, height = 1280, orientation = "upright", privacySlate = true, demandDetection = true });
        }
        finally { cancelled.Cancel(); producer.Wait(); NativeCamera.scDeleteCamera(sender); }
    }
    static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
