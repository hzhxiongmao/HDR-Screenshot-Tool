using System.Drawing;
using System.Drawing.Drawing2D;

namespace HDRScreenshotTool;

internal static class TrayIconFactory
{
    public static Icon Create()
    {
        int s = 32;
        using var bmp = new Bitmap(s, s);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Color.Transparent);

        int pad = 4, inner = s - pad * 2;
        using var penBorder = new Pen(Color.FromArgb(255, 255, 255, 255), 2.5f);
        using var penCross = new Pen(Color.FromArgb(255, 255, 255, 255), 2f);
        using var brushFill = new SolidBrush(Color.FromArgb(60, 255, 255, 255));
        using var brushLens = new SolidBrush(Color.FromArgb(40, 255, 255, 255));

        // Camera body
        var bodyRect = new Rectangle(pad, pad + 5, inner, inner - 6);
        g.FillRectangle(brushFill, bodyRect);
        g.DrawRectangle(penBorder, bodyRect);

        // Lens ring
        int lensR = inner / 3;
        var lensRect = new Rectangle(pad + inner / 2 - lensR, pad + 7 + inner / 4 - lensR, lensR * 2, lensR * 2);
        g.DrawEllipse(penBorder, lensRect);

        // Inner lens
        int innerR = lensR * 2 / 3;
        var innerLens = new Rectangle(lensRect.X + lensR - innerR, lensRect.Y + lensR - innerR, innerR * 2, innerR * 2);
        g.FillEllipse(brushLens, innerLens);
        g.DrawEllipse(penBorder, innerLens);

        // Viewfinder / crosshair at top
        int vw = 7, vh = 5;
        var viewRect = new Rectangle(pad + inner / 2 - vw / 2, pad + 1, vw, vh);
        g.FillRectangle(Brushes.White, viewRect);

        // Flash dot
        int dotX = bodyRect.Right - 6, dotY = bodyRect.Top + 4;
        g.FillEllipse(Brushes.White, dotX, dotY, 3, 3);

        return Icon.FromHandle(bmp.GetHicon());
    }
}
