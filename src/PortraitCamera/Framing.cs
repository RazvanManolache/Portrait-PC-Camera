using OpenCvSharp;

namespace PortraitCamera;

// Stateful crop tracking in normalized coordinates, independent of capture resolution.
public sealed class Framing
{
    double center = .5;
    double target = .5;
    double lostSeconds;
    public void Reset() { center = target = .5; lostSeconds = 0; }
    public Rect Crop(int width, int height, double zoom, bool automatic, double manual,
        IReadOnlyList<Rect> subjects, double elapsed)
    {
        elapsed = Math.Clamp(elapsed, 0, .25);
        if (!automatic) { center = target = Math.Clamp(manual, 0, 1); lostSeconds = 0; }
        else if (subjects.Count > 0)
        {
            // Track the closest subject to the current framing to avoid jumping between people.
            var subject = subjects.OrderBy(r => Math.Abs((r.X + r.Width * .5) / width - target)).First();
            double next = (subject.X + subject.Width * .5) / width;
            if (Math.Abs(next - target) > .025) target = next;
            lostSeconds = 0;
        }
        else
        {
            lostSeconds += elapsed;
            if (lostSeconds > 3) target = .5;
        }
        if (automatic) center += (target - center) * (1 - Math.Exp(-elapsed * 3.5));
        double cropHeight = Math.Min(height, width * 16.0 / 9.0) / Math.Clamp(zoom, 1, 2);
        int h = Math.Clamp((int)cropHeight, 1, height);
        int w = Math.Clamp((int)(cropHeight * 9 / 16), 1, width);
        int x = Math.Clamp((int)(center * width - w * .5), 0, width - w);
        // A little headroom when zooming; full height at 1x.
        int y = Math.Clamp((int)((height - h) * .32), 0, height - h);
        return new Rect(x, y, w, h);
    }
}
