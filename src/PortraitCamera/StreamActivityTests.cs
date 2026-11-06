namespace PortraitCamera;
internal static class StreamActivityTests
{
    public static void Run()
    {
        static void Check(bool ok, string why) { if (!ok) throw new InvalidOperationException(why); }
        var discovery = new StreamActivity();
        for (int i = 0; i < 100; i++)
            Check(!discovery.Observe(i / 10d, 0, 0, false) && !discovery.Active, "Discovery must remain idle");
        var brief = new StreamActivity();
        for (int i = 0; i < 100; i++)
            Check(!brief.Observe(i / 10d, 0, 5, false) && !brief.Active, "A short frame probe must not activate the camera");
        var repeated = new StreamActivity();
        for (int i = 0; i < 100; i++)
            Check(!repeated.Observe(i / 10d, 0, (ulong)(i / 10 + 1), false), "Repeated isolated frame probes must not open controls");
        foreach (bool legacy in new[] { false, true })
        {
            var stream = new StreamActivity(); int notifications = 0;
            for (int i = 0; i < 100; i++)
                if (stream.Observe(i / 30d, legacy ? (ulong)i : 0, legacy ? 0 : (ulong)i, false)) notifications++;
            Check(stream.Active && notifications == 1, "Sustained video opens controls exactly once");
            for (int i = 100; i < 130; i++)
                Check(!stream.Observe(i / 30d, legacy ? 99UL : 0, legacy ? 0 : 99UL, false), "Stopping does not open controls");
            Check(!stream.Active, "Video becomes idle after frames stop");
            for (int i = 130; i < 220; i++)
                Check(!stream.Observe(i / 30d, legacy ? (ulong)(i - 30) : 0, legacy ? 0 : (ulong)(i - 30), false), "A brief interruption must not reopen hidden controls");
            stream.Observe(11, legacy ? 189UL : 0, legacy ? 0 : 189UL, false);
            notifications = 0;
            for (int i = 0; i < 100; i++)
                if (stream.Observe(12 + i / 30d, legacy ? (ulong)(190 + i) : 0, legacy ? 0 : (ulong)(190 + i), false)) notifications++;
            Check(notifications == 1, "A separate later stream can open controls again");
        }
        var paused = new StreamActivity();
        for (int i = 0; i < 150; i++)
            Check(!paused.Observe(i / 30d, 0, (ulong)i, i < 100), "Paused video and resume of the same stream must not open controls");
    }
}
