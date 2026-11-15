$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @'
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

public static class DrawLineComparison
{
    const int Width = 1920, Height = 1080;
    const double Angle = Math.PI / 6;
    static readonly Color Background = Color.FromArgb(19, 24, 34);
    static readonly Color Core = Color.FromArgb(233, 250, 255);
    static readonly Color Edge = Color.FromArgb(145, 219, 236);

    static Color Mix(Color a, Color b, double t)
    {
        t = Math.Max(0, Math.Min(1, t));
        return Color.FromArgb((int)(a.R + (b.R-a.R)*t), (int)(a.G + (b.G-a.G)*t), (int)(a.B + (b.B-a.B)*t));
    }

    static void Line(Bitmap bitmap, Rectangle area, double x, double y, double length, double scale, bool pixel)
    {
        double dx = Math.Cos(Angle), dy = -Math.Sin(Angle);
        double grid = 4 * scale;
        for (int py = area.Top; py < area.Bottom; py++)
        for (int px = area.Left; px < area.Right; px++)
        {
            double sx = px + .5, sy = py + .5;
            if (pixel)
            {
                sx = Math.Floor(sx / grid) * grid + grid / 2;
                sy = Math.Floor(sy / grid) * grid + grid / 2;
            }
            double projection = Math.Max(0, Math.Min(length, (sx-x)*dx + (sy-y)*dy));
            double ox = sx-x-projection*dx, oy = sy-y-projection*dy;
            double distance = Math.Sqrt(ox*ox + oy*oy) / scale;
            if (pixel)
            {
                if (distance <= 3) bitmap.SetPixel(px, py, Core);
                else if (distance <= 5) bitmap.SetPixel(px, py, Edge);
            }
            else if (distance < 24)
            {
                Color color = Mix(Background, Edge, .35 * Math.Exp(-distance*distance/72));
                color = Mix(color, Edge, Math.Max(0, Math.Min(1, 5.5-distance)));
                color = Mix(color, Core, Math.Max(0, Math.Min(1, 3.5-distance)));
                bitmap.SetPixel(px, py, color);
            }
        }
    }

    static void CenterText(Graphics graphics, string value, float size, Color color, RectangleF rect, bool bold)
    {
        using (Font font = new Font("Malgun Gothic", size, bold ? FontStyle.Bold : FontStyle.Regular, GraphicsUnit.Pixel))
        using (Brush brush = new SolidBrush(color))
        using (StringFormat format = new StringFormat())
        {
            format.Alignment = StringAlignment.Center;
            format.LineAlignment = StringAlignment.Center;
            graphics.DrawString(value, font, brush, rect, format);
        }
    }

    public static void Render(string path)
    {
        double length = 800, rise = length*Math.Sin(Angle), run = length*Math.Cos(Angle);
        if (Math.Abs(Math.Atan2(rise, run)*180/Math.PI-30) > .000001)
            throw new InvalidOperationException("30 degree geometry check failed");
        using (Bitmap bitmap = new Bitmap(Width, Height, PixelFormat.Format24bppRgb))
        {
            using (Graphics graphics = Graphics.FromImage(bitmap)) graphics.Clear(Background);
            for (int panel = 0; panel < 2; panel++)
            {
                int offset = panel*960;
                Line(bitmap, new Rectangle(offset+60, 190, 840, 480), offset+130, 630, length, 1, panel == 0);
                Line(bitmap, new Rectangle(offset+300, 740, 360, 220), offset+100, 1080, 900, 4, panel == 0);
            }
            using (Graphics graphics = Graphics.FromImage(bitmap))
            using (Pen guide = new Pen(Color.FromArgb(77, 87, 103), 2))
            using (Pen border = new Pen(Color.FromArgb(57, 67, 84), 2))
            {
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
                graphics.DrawLine(border, 960, 120, 960, 1010);
                CenterText(graphics, "DRAW LINE  /  30 DEG", 44, Color.FromArgb(232, 238, 247), new RectangleF(0, 28, Width, 70), true);
                for (int panel = 0; panel < 2; panel++)
                {
                    int offset = panel*960;
                    CenterText(graphics, panel == 0 ? "A. PIXEL" : "B. IMAGE FX", 38, Color.FromArgb(218, 227, 239), new RectangleF(offset, 123, 960, 60), true);
                    graphics.DrawLine(guide, offset+130, 630, offset+855, 630);
                    graphics.DrawArc(guide, offset+66, 566, 128, 128, -30, 30);
                    CenterText(graphics, "30 deg", 24, Color.FromArgb(159, 174, 193), new RectangleF(offset+203, 582, 100, 45), false);
                    graphics.DrawRectangle(border, offset+300, 740, 360, 220);
                    CenterText(graphics, "EDGE x4", 22, Color.FromArgb(169, 184, 202), new RectangleF(offset, 968, 960, 40), true);
                    CenterText(graphics, panel == 0 ? "HARD PIXEL STEPS" : "SMOOTH ALPHA + SOFT GLOW", 22, Color.FromArgb(169, 184, 202), new RectangleF(offset, 675, 960, 42), false);
                }
                CenterText(graphics, "MATCHED LENGTH / CORE WIDTH / COLOR - STYLE PREVIEW", 19, Color.FromArgb(126, 145, 166), new RectangleF(0, 1020, Width, 40), false);
            }
            bitmap.Save(path, ImageFormat.Png);
        }
        using (Bitmap reopened = new Bitmap(path))
        {
            if (reopened.Width != Width || reopened.Height != Height)
                throw new InvalidOperationException("PNG dimensions check failed");
        }
    }
}
'@
[DrawLineComparison]::Render((Join-Path $PSScriptRoot 'draw-line-30deg-comparison.png'))
Write-Output 'Saved comparison PNG: 1920x1080, geometric angle 30 degrees'
