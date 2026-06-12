using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace HDRScreenshotTool;

public class RegionCapturedEventArgs : EventArgs
{
    public Rect PhysicalRegion { get; }
    public IntPtr MonitorHandle { get; }
    public RegionCapturedEventArgs(Rect region, IntPtr monitor) { PhysicalRegion = region; MonitorHandle = monitor; }
}

public partial class CaptureOverlay : Window
{
    public static bool IsOpen { get; private set; }
    public event EventHandler<RegionCapturedEventArgs>? RegionCaptured;

    private Point _start;
    private bool _selecting;
    private System.Windows.Shapes.Rectangle? _selRect;
    private Path? _overlay;
    private TextBlock? _sizeLabel;
    private double _dpiX = 1, _dpiY = 1;

    public CaptureOverlay()
    {
        InitializeComponent();
        var source = PresentationSource.FromVisual(Application.Current.MainWindow ?? this);
        if (source != null) { var m = source.CompositionTarget.TransformToDevice; _dpiX = m.M11; _dpiY = m.M22; }
        Left = SystemParameters.VirtualScreenLeft; Top = SystemParameters.VirtualScreenTop;
        Width = SystemParameters.VirtualScreenWidth; Height = SystemParameters.VirtualScreenHeight;
        MouseLeftButtonDown += OnDown; MouseMove += OnMove; MouseLeftButtonUp += OnUp; KeyDown += OnKey;
    }

    protected override void OnSourceInitialized(EventArgs e) { base.OnSourceInitialized(e); IsOpen = true; DrawBg(); }

    private void DrawBg()
    {
        _overlay = new Path { Fill = new SolidColorBrush(Color.FromArgb(140, 0, 0, 0)), Data = new RectangleGeometry(new Rect(0, 0, Width, Height)) };
        OverlayCanvas.Children.Add(_overlay);
        Panel.SetZIndex(_overlay, 0);
    }

    private void OnDown(object sender, MouseButtonEventArgs e)
    {
        _start = e.GetPosition(this); _selecting = true;
        _selRect = new System.Windows.Shapes.Rectangle { Stroke = new SolidColorBrush(Color.FromRgb(0, 120, 215)), StrokeThickness = 2, StrokeDashArray = new DoubleCollection { 4, 2 }, Fill = new SolidColorBrush(Color.FromArgb(20, 0, 120, 215)), IsHitTestVisible = false };
        OverlayCanvas.Children.Add(_selRect); Panel.SetZIndex(_selRect, 1);
        _sizeLabel = new TextBlock { Foreground = Brushes.White, Background = new SolidColorBrush(Color.FromArgb(200, 0, 0, 0)), FontSize = 13, FontFamily = new FontFamily("Consolas"), Padding = new Thickness(6, 3, 6, 3), IsHitTestVisible = false };
        OverlayCanvas.Children.Add(_sizeLabel); Panel.SetZIndex(_sizeLabel, 2);
        CaptureMouse();
    }

    private void OnMove(object sender, MouseEventArgs e)
    {
        if (!_selecting || _selRect == null || _sizeLabel == null) return;
        var p = e.GetPosition(this);
        var x = Math.Min(_start.X, p.X); var y = Math.Min(_start.Y, p.Y);
        var w = Math.Abs(p.X - _start.X); var h = Math.Abs(p.Y - _start.Y);
        Canvas.SetLeft(_selRect, x); Canvas.SetTop(_selRect, y); _selRect.Width = w; _selRect.Height = h;
        OverlayCanvas.Children.Remove(_overlay);
        var hole = new Rect(x, y, w, h);
        _overlay = new Path { Fill = new SolidColorBrush(Color.FromArgb(140, 0, 0, 0)), Data = new CombinedGeometry(GeometryCombineMode.Exclude, new RectangleGeometry(new Rect(0, 0, Width, Height)), new RectangleGeometry(hole)) };
        OverlayCanvas.Children.Insert(0, _overlay); Panel.SetZIndex(_overlay, 0);
        _sizeLabel.Text = $"{(int)(w * _dpiX)} x {(int)(h * _dpiY)}";
        Canvas.SetLeft(_sizeLabel, x + 4); Canvas.SetTop(_sizeLabel, Math.Max(0, y - 24));
    }

    private void OnUp(object sender, MouseButtonEventArgs e)
    {
        if (!_selecting) return; _selecting = false; ReleaseMouseCapture();
        var p = e.GetPosition(this);
        var x = Math.Min(_start.X, p.X); var y = Math.Min(_start.Y, p.Y);
        var w = Math.Abs(p.X - _start.X); var h = Math.Abs(p.Y - _start.Y);
        CloseOverlay();
        if (w > 5 && h > 5)
        {
            var phy = new Rect(x * _dpiX, y * _dpiY, w * _dpiX, h * _dpiY);
            var pt = new NativePoint { X = (int)((x + w / 2) * _dpiX), Y = (int)((y + h / 2) * _dpiY) };
            var hMon = MonitorFromPoint(pt, 2);
            RegionCaptured?.Invoke(this, new RegionCapturedEventArgs(phy, hMon));
        }
    }

    private void OnKey(object sender, KeyEventArgs e) { if (e.Key == Key.Escape) CloseOverlay(); }
    private void CloseOverlay() { IsOpen = false; Close(); }

    [DllImport("user32.dll")] private static extern IntPtr MonitorFromPoint(NativePoint pt, uint flags);
    struct NativePoint { public int X, Y; }
}
