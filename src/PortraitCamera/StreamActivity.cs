namespace PortraitCamera;

// Discovery may keep a filter alive indefinitely. Only delivered frames count.
internal sealed class StreamActivity
{
    ulong legacy, windows, frames;
    double lastFrame = double.NegativeInfinity, started;
    bool notified;
    public bool Active { get; private set; }
    public bool Observe(double now, ulong legacyFrames, ulong windowsFrames, bool paused)
    {
        ulong delta = (legacyFrames >= legacy ? legacyFrames - legacy : 0)
            + (windowsFrames >= windows ? windowsFrames - windows : 0);
        legacy = legacyFrames; windows = windowsFrames;
        if (now - lastFrame >= 3) notified = false;
        if (delta > 0)
        {
            if (now - lastFrame > .75) { started = now; frames = 0; }
            frames += delta; lastFrame = now;
            if (paused) notified = true;
        }
        Active = now - lastFrame <= .75 && now - started >= 1.5 && frames >= 20;
        if (!Active || notified || paused) return false;
        notified = true;
        return true;
    }
}
