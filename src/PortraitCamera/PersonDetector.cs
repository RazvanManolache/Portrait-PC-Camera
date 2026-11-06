using OpenCvSharp;
using System.IO;

namespace PortraitCamera;

internal sealed class PersonDetector : IDisposable
{
    readonly FaceDetectorYN face;
    readonly HOGDescriptor body = new();
    readonly Mat small = new();
    readonly Mat faces = new();
    readonly Mat square = new(320, 320, MatType.CV_8UC3);
    int attempts;
    public PersonDetector()
    {
        face = FaceDetectorYN.Create(Path.Combine(AppContext.BaseDirectory, "models", "face_detection_yunet_2023mar.onnx"), "", new Size(320, 320), .75f);
        body.SetSVMDetector(HOGDescriptor.GetDefaultPeopleDetector());
    }
    public Rect[] Detect(Mat frame)
    {
        double scale = Math.Min(1, 320.0 / Math.Max(frame.Width, frame.Height));
        Cv2.Resize(frame, small, new Size(Math.Max(1, (int)(frame.Width * scale)), Math.Max(1, (int)(frame.Height * scale))));
        square.SetTo(Scalar.Black);
        using (var region = new Mat(square, new Rect(0, 0, small.Width, small.Height))) small.CopyTo(region);
        face.Detect(square, faces);
        double sx = (double)frame.Width / small.Width, sy = (double)frame.Height / small.Height;
        var found = new List<Rect>();
        int rows = faces.Rows;
        for (int i = 0; i < rows; i++)
            found.Add(new Rect((int)(faces.At<float>(i, 0) * sx), (int)(faces.At<float>(i, 1) * sy),
                Math.Max(1, (int)(faces.At<float>(i, 2) * sx)), Math.Max(1, (int)(faces.At<float>(i, 3) * sy))));
        if (found.Count == 0 && ++attempts % 3 == 0 && small.Height >= 128 && small.Width >= 64)
        {
            var people = body.DetectMultiScale(small, 0, new Size(8, 8), new Size(8, 8), 1.08, 2);
            foreach (var r in people) found.Add(new Rect((int)(r.X * sx), (int)(r.Y * sy), (int)(r.Width * sx), (int)(r.Height * sy)));
        }
        return found.ToArray();
    }
    public void Dispose() { face.Dispose(); body.Dispose(); small.Dispose(); faces.Dispose(); square.Dispose(); }
}
