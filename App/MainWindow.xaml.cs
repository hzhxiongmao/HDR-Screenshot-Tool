using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Drawing = System.Drawing;
using Drawing2D = System.Drawing.Drawing2D;
using DrawingImaging = System.Drawing.Imaging;

namespace HDRScreenshotTool;

public partial class MainWindow : Window
{
    private NativeCaptureService? _capture;
    private HwndSource? _hwndSource;
    private int _hotkeyId = 1;
    private bool _waitingForHotkey;
    private AppSettings _settings = null!;
    private Drawing.Icon? _trayIcon;
    private bool _forceClose;

    private const int WM_HOTKEY = 0x0312, WM_TRAY = 0x8000;
    private const int MOD_ALT = 0x0001, MOD_CTRL = 0x0002, MOD_SHIFT = 0x0004, MOD_WIN = 0x0008;
    private const int NIM_ADD = 0, NIM_DELETE = 2, NIF_ICON = 2, NIF_MESSAGE = 1, NIF_TIP = 4;
    private const int WM_LBUTTONUP = 0x0202, WM_LBUTTONDBLCLK = 0x0203, WM_RBUTTONUP = 0x0205;

    [DllImport("user32.dll")] static extern bool RegisterHotKey(IntPtr h, int id, int mod, int vk);
    [DllImport("user32.dll")] static extern bool UnregisterHotKey(IntPtr h, int id);
    [DllImport("shell32.dll")] static extern bool Shell_NotifyIcon(int msg, ref NOTIFYICONDATA d);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    struct NOTIFYICONDATA
    {
        public int cbSize; public IntPtr hWnd; public int uID, uFlags, uCallbackMessage; public IntPtr hIcon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string szTip;
    }

    public MainWindow()
    {
        InitializeComponent();
        _settings = AppSettings.Load();

        SldContrast.ValueChanged += (_, e) => { _settings.Contrast = e.NewValue / 100; LblContrast.Text = $"{_settings.Contrast:F2}"; _settings.Save(); };
        SldSaturation.ValueChanged += (_, e) => { _settings.Saturation = e.NewValue / 100; LblSaturation.Text = $"{_settings.Saturation:F2}"; _settings.Save(); };
        SldBrightness.ValueChanged += (_, e) => { _settings.Brightness = e.NewValue; LblBrightness.Text = $"{e.NewValue:F0}"; _settings.Save(); };
        SldGamma.ValueChanged += (_, e) => { _settings.Gamma = e.NewValue / 100; LblGamma.Text = $"{_settings.Gamma:F2}"; _settings.Save(); };

        SldContrast.Value = _settings.Contrast * 100;
        SldSaturation.Value = _settings.Saturation * 100;
        SldBrightness.Value = _settings.Brightness;
        SldGamma.Value = _settings.Gamma * 100;
        TxtHotkey.Text = _settings.Hotkey;
        ChkStartup.IsChecked = _settings.StartWithWindows;

        Loaded += (_, _) =>
        {
            var h = new WindowInteropHelper(this).Handle;
            _hwndSource = HwndSource.FromHwnd(h);
            _hwndSource!.AddHook(WndProc);
            CreateTrayIcon();
            ParseAndRegisterHotkey(_settings.Hotkey);
            try { _capture = new NativeCaptureService(); Title = _capture.IsInitialized ? "HDR Screenshot - WGC" : "HDR - GDI+"; TxtStatus.Text = _capture.GetDebugInfo(); }
            catch (Exception ex) { TxtStatus.Text = $"Init: {ex.Message}"; }
        };
    }

    private void CreateTrayIcon()
    {
        _trayIcon = TrayIconFactory.Create();
        var nid = new NOTIFYICONDATA
        {
            cbSize = Marshal.SizeOf<NOTIFYICONDATA>(),
            hWnd = _hwndSource!.Handle, uID = 1,
            uFlags = NIF_ICON | NIF_MESSAGE | NIF_TIP,
            uCallbackMessage = WM_TRAY,
            hIcon = _trayIcon.Handle,
            szTip = "HDR Screenshot Tool"
        };
        Shell_NotifyIcon(NIM_ADD, ref nid);
    }

    private IntPtr WndProc(IntPtr h, int m, IntPtr w, IntPtr l, ref bool hd)
    {
        if (m == WM_HOTKEY) { StartCapture(); hd = true; }
        if (m == WM_TRAY)
        {
            int evt = l.ToInt32() & 0xFFFF;
            if (evt == WM_LBUTTONUP || evt == WM_LBUTTONDBLCLK) { Show(); WindowState = WindowState.Normal; Activate(); }
            if (evt == WM_RBUTTONUP) ShowTrayMenu();
            hd = true;
        }
        return IntPtr.Zero;
    }

    private void ShowTrayMenu()
    {
        var menu = new ContextMenu();
        var cap = new MenuItem { Header = "Capture Region" }; cap.Click += (_, _) => StartCapture(); menu.Items.Add(cap);
        var show = new MenuItem { Header = "Show Window" }; show.Click += (_, _) => { Show(); WindowState = WindowState.Normal; Activate(); }; menu.Items.Add(show);
        menu.Items.Add(new Separator());
        var exit = new MenuItem { Header = "Exit" }; exit.Click += (_, _) => { _forceClose = true; Close(); }; menu.Items.Add(exit);
        menu.IsOpen = true;
    }

    private void Window_Closing(object sender, CancelEventArgs e) { if (!_forceClose) { e.Cancel = true; Hide(); } }

    private void Window_StateChanged(object sender, EventArgs e)
    {
        if (WindowState == WindowState.Minimized) Hide();
    }

    private void SetHotkey_Click(object sender, RoutedEventArgs e) { _waitingForHotkey = true; TxtHotkey.Text = "Press keys..."; TxtHotkey.Background = Brushes.LightYellow; LblHotkeyHint.Text = "Press key combo"; TxtHotkey.Focus(); }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        base.OnPreviewKeyDown(e);
        if (!_waitingForHotkey) return;
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftShift or Key.RightShift or Key.LeftAlt or Key.RightAlt or Key.LWin or Key.RWin) return;
        e.Handled = true; _waitingForHotkey = false;
        int mod = 0;
        if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl)) mod |= MOD_CTRL;
        if (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift)) mod |= MOD_SHIFT;
        if (Keyboard.IsKeyDown(Key.LeftAlt) || Keyboard.IsKeyDown(Key.RightAlt)) mod |= MOD_ALT;
        if (Keyboard.IsKeyDown(Key.LWin) || Keyboard.IsKeyDown(Key.RWin)) mod |= MOD_WIN;
        int vk = KeyInterop.VirtualKeyFromKey(key);
        var parts = new List<string>();
        if ((mod & MOD_CTRL) != 0) parts.Add("Ctrl");
        if ((mod & MOD_SHIFT) != 0) parts.Add("Shift");
        if ((mod & MOD_ALT) != 0) parts.Add("Alt");
        if ((mod & MOD_WIN) != 0) parts.Add("Win");
        parts.Add(key.ToString());
        TxtHotkey.Text = string.Join("+", parts); TxtHotkey.Background = Brushes.White; LblHotkeyHint.Text = "Hotkey set";
        _settings.Hotkey = TxtHotkey.Text; _settings.Save();
        ParseAndRegisterHotkey(TxtHotkey.Text);
    }

    private void ParseAndRegisterHotkey(string hotkeyStr)
    {
        if (_hwndSource == null) return;
        int mod = 0; var parts = hotkeyStr.Split('+').Select(p => p.Trim()).ToList();
        string keyStr = parts[^1]; parts.RemoveAt(parts.Count - 1);
        foreach (var p in parts)
        {
            if (p.Equals("Ctrl", StringComparison.OrdinalIgnoreCase)) mod |= MOD_CTRL;
            else if (p.Equals("Shift", StringComparison.OrdinalIgnoreCase)) mod |= MOD_SHIFT;
            else if (p.Equals("Alt", StringComparison.OrdinalIgnoreCase)) mod |= MOD_ALT;
            else if (p.Equals("Win", StringComparison.OrdinalIgnoreCase)) mod |= MOD_WIN;
        }
        if (!Enum.TryParse<Key>(keyStr, true, out var key)) return;
        int vk = KeyInterop.VirtualKeyFromKey(key);
        UnregisterHotKey(_hwndSource.Handle, _hotkeyId); _hotkeyId++;
        RegisterHotKey(_hwndSource.Handle, _hotkeyId, mod, vk);
    }

    private void ResetSettings_Click(object sender, RoutedEventArgs e) { _settings = new AppSettings(); _settings.Save(); SldContrast.Value = 100; SldSaturation.Value = 100; SldBrightness.Value = 0; SldGamma.Value = 100; }
    private void BtnCapture_Click(object sender, RoutedEventArgs e) => StartCapture();
    private void Minimize_Click(object sender, RoutedEventArgs e) => Hide();

    private void ChkStartup_Changed(object sender, RoutedEventArgs e)
    {
        _settings.StartWithWindows = ChkStartup.IsChecked == true;
        _settings.Save();
        SetStartup(_settings.StartWithWindows);
    }

    private static void SetStartup(bool enable)
    {
        var startupDir = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
        var shortcutPath = Path.Combine(startupDir, "HDRScreenshotTool.lnk");
        var exePath = Environment.ProcessPath!;
        try
        {
            if (enable)
            {
                var shellType = Type.GetTypeFromProgID("WScript.Shell")!;
                dynamic shell = Activator.CreateInstance(shellType)!;
                dynamic shortcut = shell.CreateShortcut(shortcutPath);
                shortcut.TargetPath = exePath;
                shortcut.WorkingDirectory = Path.GetDirectoryName(exePath)!;
                shortcut.Description = "HDR Screenshot Tool";
                shortcut.Save();
            }
            else { if (File.Exists(shortcutPath)) File.Delete(shortcutPath); }
        }
        catch { }
    }

    // ── Capture flow ──

    private void StartCapture()
    {
        if (CaptureOverlay.IsOpen) return;
        try
        {
            var overlay = new CaptureOverlay();
            overlay.CaptureRequested += OnCaptureRequested;
            overlay.Show();
            overlay.Activate();
        }
        catch (Exception ex) { MessageBox.Show(ex.Message, "Error"); }
    }

    private async void OnCaptureRequested(object? sender, CaptureEventArgs e)
    {
        var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "Screenshots");
        Directory.CreateDirectory(dir);
        var file = Path.Combine(dir, $"shot_{DateTime.Now:yyyyMMdd_HHmmss_fff}.png");

        try
        {
            TxtStatus.Text = "Capturing...";
            byte[] px; int w, h;
            if (_capture?.IsInitialized == true)
            {
                var r = await Task.Run(() => _capture.Capture(e.PhysicalRegion, e.MonitorHandle));
                px = r.pixels; w = r.w; h = r.h;
            }
            else
            {
                var r = await Task.Run(() => FbGdi(e.PhysicalRegion));
                px = r.pixels; w = r.w; h = r.h;
            }

            // Apply image adjustments
            Apply(px, w, h);

            // Render annotations
            if (e.Strokes.Count > 0)
            {
                double sx = e.PhysicalRegion.Width / e.WpfRegion.Width;
                double sy = e.PhysicalRegion.Height / e.WpfRegion.Height;
                RenderStrokes(px, w, h, e.Strokes, sx, sy);
            }

            // Create bitmap
            var bmp = new WriteableBitmap(w, h, 96, 96, PixelFormats.Bgra32, null);
            bmp.WritePixels(new Int32Rect(0, 0, w, h), px, w * 4, 0);

            if (e.Mode == CaptureMode.Save)
            {
                using var s = File.Create(file);
                new PngBitmapEncoder { Frames = { BitmapFrame.Create(bmp) } }.Save(s);
                Clipboard.SetImage(bmp);
                TxtStatus.Text = $"OK {w}x{h} saved";
            }
            else // Pin
            {
                // Also save to disk
                using var s = File.Create(file);
                new PngBitmapEncoder { Frames = { BitmapFrame.Create(bmp) } }.Save(s);

                var pinWin = new PinnedImageWindow(px, w, h);
                pinWin.Show();
                TxtStatus.Text = $"Pinned {w}x{h}";
            }
        }
        catch (Exception ex) { TxtStatus.Text = $"Error: {ex.Message}"; MessageBox.Show(ex.Message, "Error"); }
    }

    private static void RenderStrokes(byte[] pixels, int w, int h, List<StrokeData> strokes, double scaleX, double scaleY)
    {
        using var bmp = new Drawing.Bitmap(w, h, DrawingImaging.PixelFormat.Format32bppArgb);
        var data = bmp.LockBits(new Drawing.Rectangle(0, 0, w, h), DrawingImaging.ImageLockMode.WriteOnly, bmp.PixelFormat);
        Marshal.Copy(pixels, 0, data.Scan0, pixels.Length);
        bmp.UnlockBits(data);

        using var g = Drawing.Graphics.FromImage(bmp);
        g.SmoothingMode = Drawing2D.SmoothingMode.AntiAlias;

        foreach (var stroke in strokes)
        {
            if (stroke.Points.Count < 2) continue;
            uint c = stroke.Color;
            var penColor = Drawing.Color.FromArgb((int)(c >> 24), (int)((c >> 16) & 0xFF), (int)((c >> 8) & 0xFF), (int)(c & 0xFF));
            using var pen = new Drawing.Pen(penColor, (float)(stroke.Thickness * Math.Min(scaleX, scaleY)))
            {
                StartCap = Drawing2D.LineCap.Round, EndCap = Drawing2D.LineCap.Round, LineJoin = Drawing2D.LineJoin.Round
            };

            var pts = stroke.Points.Select(p => new Drawing.PointF((float)(p.X * scaleX), (float)(p.Y * scaleY))).ToArray();
            if (pts.Length == 2)
                g.DrawLine(pen, pts[0], pts[1]);
            else
                g.DrawLines(pen, pts);
        }

        data = bmp.LockBits(new Drawing.Rectangle(0, 0, w, h), DrawingImaging.ImageLockMode.ReadOnly, bmp.PixelFormat);
        Marshal.Copy(data.Scan0, pixels, 0, pixels.Length);
        bmp.UnlockBits(data);
    }

    // ── Image adjustments ──

    private void Apply(byte[] p, int w, int h)
    {
        double c = _settings.Contrast, sat = _settings.Saturation, b = _settings.Brightness, g = _settings.Gamma;
        if (Math.Abs(c - 1) < 0.001 && Math.Abs(sat - 1) < 0.001 && Math.Abs(b) < 0.001 && Math.Abs(g - 1) < 0.001) return;
        var L = new byte[256];
        for (int i = 0; i < 256; i++) { double v = Math.Pow(Math.Clamp(i / 255.0, 0, 1), g); v = Math.Clamp((v - 0.5) * c + 0.5 + b / 255, 0, 1); L[i] = (byte)(v * 255); }
        for (int i = 0; i < p.Length; i += 4) { byte Bb = L[p[i]], Gg = L[p[i + 1]], Rr = L[p[i + 2]];
            if (Math.Abs(sat - 1) > 0.001) { double gr = 0.299 * Rr + 0.587 * Gg + 0.114 * Bb; Rr = (byte)Math.Clamp(gr + (Rr - gr) * sat, 0, 255); Gg = (byte)Math.Clamp(gr + (Gg - gr) * sat, 0, 255); Bb = (byte)Math.Clamp(gr + (Bb - gr) * sat, 0, 255); }
            p[i] = Bb; p[i + 1] = Gg; p[i + 2] = Rr; }
    }

    private static (byte[] pixels, int w, int h) FbGdi(System.Windows.Rect r)
    {
        int x = (int)r.Left, y = (int)r.Top, w = (int)r.Width, h = (int)r.Height;
        using var bmp = new Drawing.Bitmap(w, h, DrawingImaging.PixelFormat.Format32bppArgb);
        using var gfx = Drawing.Graphics.FromImage(bmp);
        gfx.CopyFromScreen(x, y, 0, 0, new Drawing.Size(w, h), Drawing.CopyPixelOperation.SourceCopy);
        var d = bmp.LockBits(new Drawing.Rectangle(0, 0, w, h), DrawingImaging.ImageLockMode.ReadOnly, bmp.PixelFormat);
        var px = new byte[w * h * 4]; Marshal.Copy(d.Scan0, px, 0, px.Length); bmp.UnlockBits(d);
        return (px, w, h);
    }

    // ── Cleanup ──

    protected override void OnClosed(EventArgs e)
    {
        if (_trayIcon != null) { var nid = new NOTIFYICONDATA { cbSize = Marshal.SizeOf<NOTIFYICONDATA>(), hWnd = _hwndSource!.Handle, uID = 1 }; Shell_NotifyIcon(NIM_DELETE, ref nid); _trayIcon.Dispose(); }
        if (_hwndSource != null) UnregisterHotKey(_hwndSource.Handle, _hotkeyId);
        _capture?.Dispose();
        base.OnClosed(e);
    }
}
