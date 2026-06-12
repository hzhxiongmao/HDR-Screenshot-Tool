using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace HDRScreenshotTool;

public enum CaptureMode { Save, Pin }

public class CaptureEventArgs : EventArgs
{
    public Rect PhysicalRegion { get; }
    public Rect WpfRegion { get; }
    public IntPtr MonitorHandle { get; }
    public List<StrokeData> Strokes { get; }
    public CaptureMode Mode { get; }
    public CaptureEventArgs(Rect physical, Rect wpf, IntPtr monitor, List<StrokeData> strokes, CaptureMode mode)
    { PhysicalRegion = physical; WpfRegion = wpf; MonitorHandle = monitor; Strokes = strokes; Mode = mode; }
}

public class StrokeData
{
    public List<Point> Points { get; set; } = new();
    public double Thickness { get; set; } = 3;
    public uint Color { get; set; } = 0xFFFF0000;
}

public partial class CaptureOverlay : Window
{
    public static bool IsOpen { get; private set; }
    public event EventHandler<CaptureEventArgs>? CaptureRequested;

    private double _dpiX = 1, _dpiY = 1;
    private bool _selecting, _isEditing, _isDrawing;
    private double _selX, _selY, _selW, _selH;
    private int _handleIdx = -1;
    private Point _handleDragStart;
    private Rect _handleDragOrigRect;

    // Visuals
    private Path? _overlay;
    private System.Windows.Shapes.Rectangle? _selRectVis;
    private TextBlock? _sizeLabel;
    private readonly Rectangle[] _handles = new Rectangle[8];
    private Border? _toolbar;
    private Canvas? _drawCanvas;
    private Button? _btnDraw;

    // Drawing
    private readonly List<StrokeData> _strokes = new();
    private StrokeData? _currentStroke;
    private Polyline? _currentPolyline;

    private static readonly SolidColorBrush OverlayBrush = new(Color.FromArgb(140, 0, 0, 0));
    private static readonly SolidColorBrush SelStrokeBrush = new(Color.FromRgb(0, 120, 215));
    private static readonly SolidColorBrush SelFillBrush = new(Color.FromArgb(20, 0, 120, 215));
    private static readonly SolidColorBrush HandleFillBrush = new(Colors.White);
    private static readonly SolidColorBrush HandleStrokeBrush = new(Color.FromRgb(0, 120, 215));
    private static readonly SolidColorBrush ToolbarBgBrush = new(Color.FromRgb(0x2B, 0x2B, 0x2B));
    private static readonly SolidColorBrush ToolbarBtnBgBrush = new(Color.FromRgb(0x3A, 0x3A, 0x3A));
    private static readonly SolidColorBrush ToolbarBtnFgBrush = new(Colors.White);
    private static readonly SolidColorBrush SizeLabelBgBrush = new(Color.FromArgb(200, 0, 0, 0));
    private static readonly SolidColorBrush DrawBrush = new(Color.FromRgb(0xFF, 0x30, 0x30));

    private const double HANDLE_SIZE = 8;
    private const double MIN_SEL = 10;

    [DllImport("user32.dll")] private static extern IntPtr MonitorFromPoint(NativePoint pt, uint flags);
    struct NativePoint { public int X, Y; }

    public CaptureOverlay()
    {
        InitializeComponent();
        var source = PresentationSource.FromVisual(Application.Current.MainWindow ?? this);
        if (source != null) { var m = source.CompositionTarget.TransformToDevice; _dpiX = m.M11; _dpiY = m.M22; }
        Left = SystemParameters.VirtualScreenLeft; Top = SystemParameters.VirtualScreenTop;
        Width = SystemParameters.VirtualScreenWidth; Height = SystemParameters.VirtualScreenHeight;
        Cursor = Cursors.Cross;
        MouseLeftButtonDown += OnDown; MouseMove += OnMove; MouseLeftButtonUp += OnUp; KeyDown += OnKey;
        CreateEditUI();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        IsOpen = true;
        _overlay = new Path { Fill = OverlayBrush, Data = new RectangleGeometry(new Rect(0, 0, Width, Height)) };
        OverlayCanvas.Children.Add(_overlay);
        Panel.SetZIndex(_overlay, 0);
    }

    // ── Create edit-mode UI (hidden initially) ──

    private void CreateEditUI()
    {
        // 8 resize handles
        for (int i = 0; i < 8; i++)
        {
            var h = new Rectangle
            {
                Width = HANDLE_SIZE, Height = HANDLE_SIZE,
                Fill = HandleFillBrush, Stroke = HandleStrokeBrush, StrokeThickness = 1,
                Visibility = Visibility.Collapsed, IsHitTestVisible = true
            };
            h.MouseLeftButtonDown += HandleDown; h.MouseMove += HandleMove; h.MouseLeftButtonUp += HandleUp;
            _handles[i] = h;
            OverlayCanvas.Children.Add(h);
            Panel.SetZIndex(h, 4);
        }

        // Toolbar
        var stack = new StackPanel { Orientation = Orientation.Horizontal };
        _btnDraw = MakeBtn("✎");  // pencil
        _btnDraw.Click += (_, _) => ToggleDraw();
        var btnPin = MakeBtn("📌"); // pin
        btnPin.Click += (_, _) => Commit(CaptureMode.Pin);
        var btnSave = MakeBtn("✔"); // checkmark
        btnSave.Click += (_, _) => Commit(CaptureMode.Save);
        var btnCancel = MakeBtn("✘"); // X
        btnCancel.Click += (_, _) => CloseOverlay();
        stack.Children.Add(_btnDraw);
        stack.Children.Add(new Rectangle { Width = 1, Height = 20, Fill = new SolidColorBrush(Color.FromRgb(0x55, 0x55, 0x55)), Margin = new Thickness(2, 0, 2, 0) });
        stack.Children.Add(btnPin);
        stack.Children.Add(btnSave);
        stack.Children.Add(btnCancel);

        _toolbar = new Border
        {
            Background = ToolbarBgBrush, CornerRadius = new CornerRadius(6),
            Child = stack, Padding = new Thickness(4, 4, 4, 4),
            Visibility = Visibility.Collapsed
        };
        OverlayCanvas.Children.Add(_toolbar);
        Panel.SetZIndex(_toolbar, 5);

        // Draw canvas
        _drawCanvas = new Canvas { Background = Brushes.Transparent, Visibility = Visibility.Collapsed, IsHitTestVisible = false };
        _drawCanvas.MouseLeftButtonDown += DrawDown;
        _drawCanvas.MouseMove += DrawMove;
        _drawCanvas.MouseLeftButtonUp += DrawUp;
        OverlayCanvas.Children.Add(_drawCanvas);
        Panel.SetZIndex(_drawCanvas, 1);
    }

    private static Button MakeBtn(string text)
    {
        return new Button
        {
            Content = text, Width = 32, Height = 28,
            FontSize = 14, FontFamily = new FontFamily("Segoe UI Symbol"),
            Foreground = ToolbarBtnFgBrush, Background = Brushes.Transparent,
            BorderThickness = new Thickness(0), Cursor = Cursors.Hand,
            Margin = new Thickness(1, 0, 1, 0)
        };
    }

    // ── Selection phase ──

    private void OnDown(object sender, MouseButtonEventArgs e)
    {
        if (_isEditing) return;
        var p = e.GetPosition(this);
        _start = p; _selecting = true;
        _selRectVis = new System.Windows.Shapes.Rectangle
        {
            Stroke = SelStrokeBrush, StrokeThickness = 2,
            StrokeDashArray = new DoubleCollection { 4, 2 },
            Fill = SelFillBrush, IsHitTestVisible = false
        };
        OverlayCanvas.Children.Add(_selRectVis); Panel.SetZIndex(_selRectVis, 2);
        _sizeLabel = new TextBlock
        {
            Foreground = Brushes.White, Background = SizeLabelBgBrush,
            FontSize = 13, FontFamily = new FontFamily("Consolas"),
            Padding = new Thickness(6, 3, 6, 3), IsHitTestVisible = false
        };
        OverlayCanvas.Children.Add(_sizeLabel); Panel.SetZIndex(_sizeLabel, 3);
        CaptureMouse();
    }

    private Point _start;

    private void OnMove(object sender, MouseEventArgs e)
    {
        if (_handleIdx >= 0) { HandleMove(sender, e); return; }
        if (!_selecting || _selRectVis == null || _sizeLabel == null) return;
        var p = e.GetPosition(this);
        var x = Math.Min(_start.X, p.X); var y = Math.Min(_start.Y, p.Y);
        var w = Math.Abs(p.X - _start.X); var h = Math.Abs(p.Y - _start.Y);
        Canvas.SetLeft(_selRectVis, x); Canvas.SetTop(_selRectVis, y);
        _selRectVis.Width = w; _selRectVis.Height = h;
        UpdateOverlay(x, y, w, h);
        _sizeLabel.Text = $"{(int)(w * _dpiX)} x {(int)(h * _dpiY)}";
        Canvas.SetLeft(_sizeLabel, x + 4); Canvas.SetTop(_sizeLabel, Math.Max(0, y - 24));
    }

    private void OnUp(object sender, MouseButtonEventArgs e)
    {
        if (_handleIdx >= 0) { HandleUp(sender, e); return; }
        if (!_selecting) return;
        _selecting = false; ReleaseMouseCapture();
        var p = e.GetPosition(this);
        _selX = Math.Min(_start.X, p.X); _selY = Math.Min(_start.Y, p.Y);
        _selW = Math.Abs(p.X - _start.X); _selH = Math.Abs(p.Y - _start.Y);

        if (_selW < MIN_SEL || _selH < MIN_SEL)
        {
            CloseOverlay(); return;
        }

        EnterEditMode();
    }

    private void EnterEditMode()
    {
        _isEditing = true;
        Cursor = Cursors.Arrow;
        _sizeLabel!.Visibility = Visibility.Collapsed;
        if (_selRectVis != null) { _selRectVis.StrokeDashArray = null; _selRectVis.StrokeThickness = 1.5; }
        UpdateOverlay(_selX, _selY, _selW, _selH);
        UpdateDrawCanvas();
        PositionHandles();
        PositionToolbar();
    }

    // ── Editing: handles ──

    private void PositionHandles()
    {
        double hw = HANDLE_SIZE / 2;
        var positions = new (double x, double y, Cursor cursor)[]
        {
            (_selX - hw, _selY - hw, Cursors.SizeNWSE),
            (_selX + _selW / 2 - hw, _selY - hw, Cursors.SizeNS),
            (_selX + _selW - hw, _selY - hw, Cursors.SizeNESW),
            (_selX + _selW - hw, _selY + _selH / 2 - hw, Cursors.SizeWE),
            (_selX + _selW - hw, _selY + _selH - hw, Cursors.SizeNWSE),
            (_selX + _selW / 2 - hw, _selY + _selH - hw, Cursors.SizeNS),
            (_selX - hw, _selY + _selH - hw, Cursors.SizeNESW),
            (_selX - hw, _selY + _selH / 2 - hw, Cursors.SizeWE),
        };
        for (int i = 0; i < 8; i++)
        {
            _handles[i].Visibility = Visibility.Visible;
            _handles[i].Cursor = positions[i].cursor;
            Canvas.SetLeft(_handles[i], positions[i].x);
            Canvas.SetTop(_handles[i], positions[i].y);
        }
    }

    private void HandleDown(object sender, MouseButtonEventArgs e)
    {
        for (int i = 0; i < 8; i++)
        {
            if (ReferenceEquals(sender, _handles[i])) { _handleIdx = i; break; }
        }
        _handleDragStart = e.GetPosition(this);
        _handleDragOrigRect = new Rect(_selX, _selY, _selW, _selH);
        if (sender is UIElement el) el.CaptureMouse();
        e.Handled = true;
    }

    private void HandleMove(object sender, MouseEventArgs e)
    {
        if (_handleIdx < 0) return;
        var p = e.GetPosition(this);
        double dx = p.X - _handleDragStart.X;
        double dy = p.Y - _handleDragStart.Y;
        double ox = _handleDragOrigRect.X, oy = _handleDragOrigRect.Y;
        double ow = _handleDragOrigRect.Width, oh = _handleDragOrigRect.Height;

        double nx = ox, ny = oy, nw = ow, nh = oh;

        switch (_handleIdx)
        {
            case 0: nx = ox + dx; ny = oy + dy; nw = ow - dx; nh = oh - dy; break;
            case 1: ny = oy + dy; nh = oh - dy; break;
            case 2: ny = oy + dy; nw = ow + dx; nh = oh - dy; break;
            case 3: nw = ow + dx; break;
            case 4: nw = ow + dx; nh = oh + dy; break;
            case 5: nh = oh + dy; break;
            case 6: nx = ox + dx; nw = ow - dx; nh = oh + dy; break;
            case 7: nx = ox + dx; nw = ow - dx; break;
        }

        // Constrain to screen
        if (nx < 0) { nw += nx; nx = 0; }
        if (ny < 0) { nh += ny; ny = 0; }
        if (nx + nw > Width) nw = Width - nx;
        if (ny + nh > Height) nh = Height - ny;

        // Minimum size
        if (nw < MIN_SEL) { if (_handleIdx is 0 or 6 or 7) nx = _selX; nw = MIN_SEL; }
        if (nh < MIN_SEL) { if (_handleIdx is 0 or 1 or 2) ny = _selY; nh = MIN_SEL; }

        _selX = nx; _selY = ny; _selW = nw; _selH = nh;

        // Update visuals
        if (_selRectVis != null)
        {
            Canvas.SetLeft(_selRectVis, _selX); Canvas.SetTop(_selRectVis, _selY);
            _selRectVis.Width = _selW; _selRectVis.Height = _selH;
        }
        UpdateOverlay(_selX, _selY, _selW, _selH);
        PositionHandles();
        PositionToolbar();
        if (_sizeLabel != null)
        {
            _sizeLabel.Text = $"{(int)(_selW * _dpiX)} x {(int)(_selH * _dpiY)}";
            Canvas.SetLeft(_sizeLabel, _selX + 4); Canvas.SetTop(_sizeLabel, Math.Max(0, _selY - 24));
        }
        UpdateDrawCanvas();
    }

    private void HandleUp(object sender, MouseButtonEventArgs e)
    {
        _handleIdx = -1;
        if (sender is UIElement el) el.ReleaseMouseCapture();
        e.Handled = true;
    }

    // ── Editing: toolbar ──

    private void PositionToolbar()
    {
        if (_toolbar == null) return;
        _toolbar.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        double tw = _toolbar.DesiredSize.Width;
        double th = _toolbar.DesiredSize.Height;
        double tx = _selX + _selW - tw; // right-aligned
        double gap = 6;
        double ty;

        // Prefer below, fallback above
        if (_selY + _selH + gap + th <= Height)
            ty = _selY + _selH + gap;
        else if (_selY - gap - th >= 0)
            ty = _selY - gap - th;
        else
            ty = Math.Max(0, Math.Min(Height - th, _selY + _selH + gap));

        // Clamp X
        if (tx < 0) tx = _selX;
        if (tx + tw > Width) tx = Width - tw - 4;

        _toolbar.Visibility = Visibility.Visible;
        Canvas.SetLeft(_toolbar, tx);
        Canvas.SetTop(_toolbar, ty);
    }

    // ── Drawing ──

    private void ToggleDraw()
    {
        _isDrawing = !_isDrawing;
        if (_isDrawing)
        {
            Cursor = Cursors.Pen;
            _drawCanvas!.IsHitTestVisible = true;
            _drawCanvas.Visibility = Visibility.Visible;
            _drawCanvas.Background = Brushes.Transparent;
            if (_btnDraw != null) _btnDraw.Background = new SolidColorBrush(Color.FromRgb(0x55, 0x55, 0x55));
            // hide handles while drawing
            foreach (var h in _handles) h.Visibility = Visibility.Collapsed;
        }
        else
        {
            Cursor = Cursors.Arrow;
            _drawCanvas!.IsHitTestVisible = false;
            _drawCanvas.Background = Brushes.Transparent;
            if (_btnDraw != null) _btnDraw.Background = Brushes.Transparent;
            PositionHandles();
        }
    }

    private void UpdateDrawCanvas()
    {
        if (_drawCanvas == null) return;
        Canvas.SetLeft(_drawCanvas, _selX);
        Canvas.SetTop(_drawCanvas, _selY);
        _drawCanvas.Width = _selW;
        _drawCanvas.Height = _selH;
        _drawCanvas.Clip = new RectangleGeometry(new Rect(0, 0, _selW, _selH));
    }

    private void DrawDown(object sender, MouseButtonEventArgs e)
    {
        if (!_isDrawing) return;
        var p = e.GetPosition(_drawCanvas);
        _currentStroke = new StrokeData { Thickness = 3, Color = 0xFFFF0000 };
        _currentStroke.Points.Add(p);
        _currentPolyline = new Polyline
        {
            Stroke = DrawBrush, StrokeThickness = 3,
            StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round,
            StrokeLineJoin = PenLineJoin.Round, IsHitTestVisible = false
        };
        _currentPolyline.Points.Add(p);
        _drawCanvas!.Children.Add(_currentPolyline);
        _drawCanvas.CaptureMouse();
        e.Handled = true;
    }

    private void DrawMove(object sender, MouseEventArgs e)
    {
        if (!_isDrawing || _currentStroke == null || _currentPolyline == null) return;
        var p = e.GetPosition(_drawCanvas);
        // clamp to canvas bounds
        p.X = Math.Clamp(p.X, 0, _selW);
        p.Y = Math.Clamp(p.Y, 0, _selH);
        _currentStroke.Points.Add(p);
        _currentPolyline.Points.Add(p);
        e.Handled = true;
    }

    private void DrawUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isDrawing || _currentStroke == null) return;
        _strokes.Add(_currentStroke);
        _currentStroke = null; _currentPolyline = null;
        _drawCanvas!.ReleaseMouseCapture();
        e.Handled = true;
    }

    // ── Commit ──

    private void Commit(CaptureMode mode)
    {
        var phyX = _selX * _dpiX;
        var phyY = _selY * _dpiY;
        var phyW = _selW * _dpiX;
        var phyH = _selH * _dpiY;
        var physRect = new Rect(phyX, phyY, phyW, phyH);

        var pt = new NativePoint { X = (int)((_selX + _selW / 2) * _dpiX), Y = (int)((_selY + _selH / 2) * _dpiY) };
        var hMon = MonitorFromPoint(pt, 2);

        IsOpen = false;
        Close();

        var wpfRect = new Rect(_selX, _selY, _selW, _selH);
        CaptureRequested?.Invoke(this, new CaptureEventArgs(physRect, wpfRect, hMon, new List<StrokeData>(_strokes), mode));
    }

    // ── Helpers ──

    private void UpdateOverlay(double x, double y, double w, double h)
    {
        if (_overlay == null) return;
        var hole = new Rect(x, y, w, h);
        _overlay.Data = new CombinedGeometry(GeometryCombineMode.Exclude,
            new RectangleGeometry(new Rect(0, 0, Width, Height)), new RectangleGeometry(hole));
    }

    private void OnKey(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            if (_isDrawing) { ToggleDraw(); return; }
            CloseOverlay();
        }
        if (e.Key == Key.Enter && _isEditing) Commit(CaptureMode.Save);
    }

    private void CloseOverlay() { IsOpen = false; Close(); }

    protected override void OnClosed(EventArgs e) { IsOpen = false; base.OnClosed(e); }
}
