using Microsoft.Win32;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using Forms = System.Windows.Forms;

namespace PortraitCamera;

public partial class MainWindow : Window
{
    readonly CameraEngine engine = new();
    readonly Forms.NotifyIcon tray;
    Settings settings;
    bool loaded, quitting, previewQueued;
    readonly DispatcherTimer saveTimer;
    Uri? releasePage;
    bool checkingUpdates;
    public MainWindow()
    {
        InitializeComponent();
        settings = Settings.Load();
        settings = settings with { OutputWidth = Settings.ValidWidth(settings.OutputWidth) };
        VersionLabel.Text = Updates.VersionLabel;
        ResolutionPicker.SelectedIndex = settings.OutputWidth == 360 ? 0 : settings.OutputWidth == 1080 ? 2 : 1;
        UpdatesToggle.IsChecked = settings.CheckForUpdates;
        AutoFrameToggle.IsChecked = settings.AutoFrame; MirrorToggle.IsChecked = settings.Mirror;
        RotationPicker.SelectedIndex = settings.Rotation / 90; ZoomSlider.Value = Math.Clamp(settings.Zoom, 1, 2);
        PositionSlider.Value = Math.Clamp(settings.Position, 0, 1); PopupToggle.IsChecked = settings.ShowOnCall;
        TopToggle.IsChecked = settings.KeepOnTop; StartupToggle.IsChecked = settings.StartWithWindows;
        Topmost = settings.KeepOnTop; PositionSlider.IsEnabled = !settings.AutoFrame;
        RefreshCameras();
        saveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
        saveTimer.Tick += (_, _) => { saveTimer.Stop(); settings.Save(); };
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("Open Portrait Camera", null, (_, _) => Dispatcher.Invoke(ShowControls));
        menu.Items.Add("Pause / resume camera", null, (_, _) => Dispatcher.Invoke(TogglePause));
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Quit", null, (_, _) => Dispatcher.Invoke(Quit));
        tray = new Forms.NotifyIcon { Text = "Portrait Camera · ready", Icon = new System.Drawing.Icon(Path.Combine(AppContext.BaseDirectory, "portrait.ico")), Visible = true, ContextMenuStrip = menu };
        tray.DoubleClick += (_, _) => Dispatcher.Invoke(ShowControls);
        engine.Frame += bitmap =>
        {
            if (previewQueued || quitting) return;
            previewQueued = true;
            Dispatcher.BeginInvoke(() => { PreviewImage.Source = bitmap; previewQueued = false; });
        };
        engine.Status += status => Dispatcher.BeginInvoke(() =>
        {
            if (quitting) return;
            StatusLabel.Text = status.Registered ? status.Message : "Preview only · run the installer to add Portrait Camera to calling apps.";
            LiveBadge.Text = status.Paused ? "PAUSED" : status.Connected ? "IN USE" : status.Capturing ? "PREVIEW" : "READY";
            TrackingLabel.Text = status.Paused ? "Your video is hidden" : !settings.AutoFrame ? "Manual framing" : status.People > 0 ? "Following you" : "Centered · looking for you";
            tray.Text = status.Paused ? "Portrait Camera · paused" : status.Connected ? "Portrait Camera · in use" : "Portrait Camera · ready";
        });
        engine.CallStarted += () => Dispatcher.BeginInvoke(() => { if (settings.ShowOnCall && !quitting) ShowControls(false); });
        IsVisibleChanged += (_, _) => engine.Preview = IsVisible;
        Closing += OnClosing;
        SourceInitialized += (_, _) => { var value = 1; DwmSetWindowAttribute(new WindowInteropHelper(this).Handle, 20, ref value, sizeof(int)); };
        loaded = true;
        UpdateStartup();
        engine.Options = settings;
        engine.Start();
        if (settings.CheckForUpdates) _ = CheckUpdates(false);
    }
    [System.Runtime.InteropServices.DllImport("dwmapi.dll")]
    static extern int DwmSetWindowAttribute(nint hwnd, int attr, ref int value, int size);
    public void ShowControls() => ShowControls(true);
    void ShowControls(bool activate)
    {
        ShowActivated = activate;
        Show(); WindowState = WindowState.Normal;
        if (activate) Activate();
        engine.Preview = true;
    }
    void RefreshCameras()
    {
        bool before = loaded; loaded = false;
        try
        {
            var cameras = NativeCamera.List();
            CameraPicker.ItemsSource = cameras;
            var selected = cameras.FirstOrDefault(c => c.Id == settings.CameraId);
            if (selected == null && string.IsNullOrEmpty(settings.CameraId))
                selected = cameras.FirstOrDefault(c => c.Id.Contains("usb#", StringComparison.OrdinalIgnoreCase)) ?? cameras.FirstOrDefault();
            CameraPicker.SelectedItem = selected;
            if (selected != null) settings = settings with { CameraId = selected.Id };
            else CameraPicker.Text = "Choose a camera";
        }
        finally { loaded = before; }
    }
    void CameraChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (!loaded || CameraPicker.SelectedItem is not CameraDevice device) return;
        settings = settings with { CameraId = device.Id }; Apply();
    }
    void OptionsChanged(object sender, RoutedEventArgs e)
    {
        if (!loaded) return;
        bool wasStartup = settings.StartWithWindows;
        settings = settings with
        {
            AutoFrame = AutoFrameToggle.IsChecked == true, Mirror = MirrorToggle.IsChecked == true,
            Rotation = Math.Max(0, RotationPicker.SelectedIndex) * 90, Zoom = ZoomSlider.Value, Position = PositionSlider.Value,
            ShowOnCall = PopupToggle.IsChecked == true, KeepOnTop = TopToggle.IsChecked == true,
            StartWithWindows = StartupToggle.IsChecked == true, CheckForUpdates = UpdatesToggle.IsChecked == true
        };
        Topmost = settings.KeepOnTop; PositionSlider.IsEnabled = !settings.AutoFrame;
        if (wasStartup != settings.StartWithWindows) UpdateStartup();
        Apply();
    }
    void Apply() { engine.Options = settings; saveTimer.Stop(); saveTimer.Start(); }
    void UpdateStartup()
    {
        StartupIntegration.Apply(settings);
    }
    void ResolutionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (!loaded) return;
        settings = settings with { OutputWidth = ResolutionPicker.SelectedIndex == 0 ? 360 : ResolutionPicker.SelectedIndex == 2 ? 1080 : 720 };
        StatusLabel.Text = "Changing resolution… re-select the camera in your calling app.";
        Apply();
    }
    async void UpdateClicked(object sender, RoutedEventArgs e)
    {
        if (releasePage != null)
        {
            try { Updates.OpenRelease(releasePage); }
            catch { UpdateLabel.Text = "Couldn’t open your browser. Visit GitHub releases."; }
        }
        else await CheckUpdates(true);
    }
    async Task CheckUpdates(bool manual)
    {
        if (checkingUpdates) return;
        var stamp = Path.Combine(Settings.Folder, "last-update-check.txt");
        if (!manual)
        {
            try { if (DateTimeOffset.TryParse(File.ReadAllText(stamp), out var last) && DateTimeOffset.UtcNow - last < TimeSpan.FromDays(1)) return; }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
        checkingUpdates = true; UpdateButton.IsEnabled = false; UpdateLabel.Text = "Checking GitHub…";
        var result = await Updates.CheckAsync();
        if (quitting) return;
        releasePage = result.ReleasePage;
        UpdateLabel.Text = result.Message;
        UpdateButton.Content = releasePage != null ? "View update" : "Check updates";
        UpdateButton.IsEnabled = true; checkingUpdates = false;
        try { Directory.CreateDirectory(Settings.Folder); File.WriteAllText(stamp, DateTimeOffset.UtcNow.ToString("O")); }
        catch (IOException) { } catch (UnauthorizedAccessException) { }
    }
    void RefreshClicked(object sender, RoutedEventArgs e) { RefreshCameras(); Apply(); }
    void PauseClicked(object sender, RoutedEventArgs e) => TogglePause();
    void TogglePause() { engine.Paused = !engine.Paused; PauseButton.Content = engine.Paused ? "Resume camera" : "Pause camera"; }
    void HideClicked(object sender, RoutedEventArgs e) => Hide();
    void OnClosing(object? sender, CancelEventArgs e) { if (!quitting) { e.Cancel = true; Hide(); } }
    internal void Quit()
    {
        quitting = true; saveTimer.Stop(); settings.Save(); tray.Visible = false; tray.Dispose();
        engine.Dispose(); System.Windows.Application.Current.Shutdown();
    }
}
