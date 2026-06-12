using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace HDRScreenshotTool;

public partial class PinnedImageWindow : Window
{
    private double _scale = 1.0;
    private double _origW, _origH;
    private const double MinScale = 0.15;
    private const double MaxScale = 4.0;

    public PinnedImageWindow(byte[] pixels, int w, int h)
    {
        InitializeComponent();
        _origW = w; _origH = h;

        var bmp = new WriteableBitmap(w, h, 96, 96, PixelFormats.Bgra32, null);
        bmp.WritePixels(new Int32Rect(0, 0, w, h), pixels, w * 4, 0);
        ImgPinned.Source = bmp;

        // Cap initial size to 55% of primary screen
        var screen = SystemParameters.WorkArea;
        double maxW = screen.Width * 0.55;
        double maxH = screen.Height * 0.55;
        if (w > maxW || h > maxH)
        {
            double ratio = Math.Min(maxW / w, maxH / h);
            _scale = ratio;
            Width = w * ratio;
            Height = h * ratio;
        }
        else
        {
            Width = w;
            Height = h;
        }

        // Position at bottom-right of screen
        Left = screen.Right - Width - 40;
        Top = screen.Bottom - Height - 40;

        // Allow window to go very small
        MinWidth = 60;
        MinHeight = 40;
        MaxWidth = screen.Width;
        MaxHeight = screen.Height;
    }

    private void Image_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            // Double-click: reset to original size
            _scale = 1.0;
            Width = _origW;
            Height = _origH;
            return;
        }
        if (e.LeftButton == MouseButtonState.Pressed)
            DragMove();
    }

    private void Image_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (Keyboard.Modifiers != ModifierKeys.Control) return;
        double delta = e.Delta > 0 ? 1.1 : 1 / 1.1;
        _scale = Math.Clamp(_scale * delta, MinScale, MaxScale);
        Width = _origW * _scale;
        Height = _origH * _scale;
        e.Handled = true;
    }

    private void Image_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        var menu = new ContextMenu();
        var copy = new MenuItem { Header = "Copy to Clipboard" };
        copy.Click += (_, _) =>
        {
            if (ImgPinned.Source is BitmapSource bs)
                Clipboard.SetImage(bs);
        };
        menu.Items.Add(copy);
        var save = new MenuItem { Header = "Save As..." };
        save.Click += (_, _) =>
        {
            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "PNG Image|*.png|JPEG Image|*.jpg|Bitmap|*.bmp",
                DefaultExt = "png",
                FileName = $"pinned_{DateTime.Now:yyyyMMdd_HHmmss}.png"
            };
            if (dlg.ShowDialog() == true)
            {
                var encoder = GetEncoder(dlg.FileName);
                if (ImgPinned.Source is BitmapSource src)
                {
                    encoder.Frames.Add(BitmapFrame.Create(src));
                    using var fs = File.Create(dlg.FileName);
                    encoder.Save(fs);
                }
            }
        };
        menu.Items.Add(save);
        menu.Items.Add(new Separator());
        var close = new MenuItem { Header = "Close" };
        close.Click += (_, _) => Close();
        menu.Items.Add(close);
        menu.IsOpen = true;
        e.Handled = true;
    }

    private static BitmapEncoder GetEncoder(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        return ext switch
        {
            ".jpg" or ".jpeg" => new JpegBitmapEncoder(),
            ".bmp" => new BmpBitmapEncoder(),
            _ => new PngBitmapEncoder()
        };
    }

    private void Image_MouseEnter(object sender, MouseEventArgs e)
    {
        BtnClose.Visibility = Visibility.Visible;
    }

    private void Image_MouseLeave(object sender, MouseEventArgs e)
    {
        BtnClose.Visibility = Visibility.Collapsed;
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
