using System.IO;
using System.Windows;

namespace PortraitCamera;

public partial class App : System.Windows.Application
{
    Mutex? instance;
    EventWaitHandle? showRequest;
    RegisteredWaitHandle? showWait;
    EventWaitHandle? quitRequest;
    RegisteredWaitHandle? quitWait;
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        if (e.Args.Contains("--configure-startup")) { StartupIntegration.Apply(Settings.Load()); Shutdown(); return; }
        OpenCvSharp.Cv2.SetNumThreads(2);
        if (e.Args.Contains("--self-test")) { Shutdown(SelfTest.Run(e.Args)); return; }
        if (e.Args.Contains("--list-cameras"))
        {
            Directory.CreateDirectory(Settings.Folder);
            File.WriteAllText(Path.Combine(Settings.Folder, "cameras.json"), System.Text.Json.JsonSerializer.Serialize(NativeCamera.List(true)));
            Shutdown(); return;
        }
        instance = new Mutex(true, "Local\\PortraitCamera.App.89084f72", out bool first);
        showRequest = new EventWaitHandle(false, EventResetMode.AutoReset, "Local\\PortraitCamera.Show.89084f72");
        quitRequest = new EventWaitHandle(false, EventResetMode.AutoReset, "Local\\PortraitCamera.Quit.89084f72");
        if (e.Args.Contains("--quit")) { if (!first) quitRequest.Set(); Shutdown(); return; }
        if (!first) { showRequest.Set(); Shutdown(); return; }
        DispatcherUnhandledException += (_, args) =>
        {
            Directory.CreateDirectory(Settings.Folder);
            File.AppendAllText(Path.Combine(Settings.Folder, "errors.log"), args.Exception + Environment.NewLine);
            System.Windows.MessageBox.Show("Portrait Camera ran into a problem. Restart it from the Start menu.\n\n" + args.Exception.Message, "Portrait Camera");
            args.Handled = true; Shutdown(1);
        };
        var window = new MainWindow(); MainWindow = window;
        showWait = ThreadPool.RegisterWaitForSingleObject(showRequest, (_, _) => Dispatcher.BeginInvoke(window.ShowControls), null, -1, false);
        quitWait = ThreadPool.RegisterWaitForSingleObject(quitRequest, (_, _) => Dispatcher.BeginInvoke(window.Quit), null, -1, false);
        if (!e.Args.Contains("--background")) window.ShowControls();
    }
    protected override void OnExit(ExitEventArgs e)
    {
        quitWait?.Unregister(null); quitRequest?.Dispose(); showWait?.Unregister(null); showRequest?.Dispose(); instance?.Dispose(); base.OnExit(e);
    }
}
