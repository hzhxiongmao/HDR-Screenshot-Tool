using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace HDRScreenshotTool;

public partial class PinnedImageWindow : Window
{
    private double _scale = 1.0;
    private double _baseW, _baseH;
    private const double MinScale = 0.15;
    private const double MaxScale = 4.0;

    [DllImport("user32.dll")] static extern short GetKeyState(int key);
    private const int VK_CONTROL = 0x11;

    public PinnedImageWindow(byte[] pixels, int pixelW, int pixelH, double displayW, double displayH, double posX, double posY)
    {
        InitializeComponent();

        var bmp = new WriteableBitmap(pixelW, pixelH, 96, 96, PixelFormats.Bgra32, null);
        bmp.WritePixels(new Int32Rect(0, 0, pixelW, pixelH), pixels, pixelW * 4, 0);
        ImgPinned.Source = bmp;

        _baseW = displayW;
        _baseH = displayH;

        var screen = SystemParameters.WorkArea;
        double maxW = screen.Width * 0.7;
        double maxH = screen.Height * 0.7;
        if (_baseW > maxW || _baseH > maxH)
        {
            double ratio = Math.Min(maxW / _baseW, maxH / _baseH);
            _scale = ratio;
        }

        Width = _baseW * _scale;
        Height = _baseH * _scale;

        Left = posX;
        Top = posY;

        MinWidth = 60;
        MinHeight = 40;
        MaxWidth = screen.Width;
        MaxHeight = screen.Height;

        Loaded += (_, _) => { Focus(); Activate(); };
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        base.OnPreviewKeyDown(e);
        if (e.Key == Key.Escape)
        {
            Close();
            e.Handled = true;
        }
        if (e.Key == Key.C && Keyboard.Modifiers == ModifierKeys.Control)
        {
            if (ImgPinned.Source is BitmapSource bs)
                Clipboard.SetImage(bs);
            e.Handled = true;
        }
    }

    private void Grid_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        Focus();
        if (e.ClickCount == 2)
        {
            _scale = 1.0;
            Width = _baseW;
            Height = _baseH;
            return;
        }
        if (e.LeftButton == MouseButtonState.Pressed)
            DragMove();
    }

    private void Grid_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        bool ctrl = (GetKeyState(VK_CONTROL) & 0x8000) != 0;
        if (!ctrl) return;

        double delta = e.Delta > 0 ? 1.1 : 1 / 1.1;
        _scale = Math.Clamp(_scale * delta, MinScale, MaxScale);
        Width = _baseW * _scale;
        Height = _baseH * _scale;
        e.Handled = true;
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void BtnClose_MouseEnter(object sender, MouseEventArgs e)
    {
        BtnClose.Background = new SolidColorBrush(Color.FromRgb(0xCC, 0x33, 0x33));
        BtnClose.Foreground = Brushes.White;
    }

    private void BtnClose_MouseLeave(object sender, MouseEventArgs e)
    {
        BtnClose.Background = new SolidColorBrush(Color.FromArgb(0x99, 0x33, 0x33, 0x33));
        BtnClose.Foreground = new SolidColorBrush(Color.FromArgb(0xCC, 0xFF, 0xFF, 0xFF));
    }
}
