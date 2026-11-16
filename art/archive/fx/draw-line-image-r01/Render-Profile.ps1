$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @'
using System;
using System.Drawing;
using System.Drawing.Imaging;

public static class DrawImageProfile
{
    static double Smooth(double low, double high, double value)
    {
        double t = Math.Max(0, Math.Min(1, (value-low)/(high-low)));
        return t*t*(3-2*t);
    }

    public static void Render(string path)
    {
        using (Bitmap bitmap = new Bitmap(96, 64, PixelFormat.Format32bppArgb))
        {
            for (int y = 0; y < 64; y++)
            for (int x = 0; x < 96; x++)
            {
                double distance = Math.Abs((y+0.5-32)/32);
                double core = 1-Smooth(.14, .24, distance);
                double edge = .78*(1-Smooth(.24, .43, distance));
                double glow = .28*Math.Exp(-distance*distance/.22)*(1-Smooth(.85, .98, distance));
                double alpha = Math.Max(core, Math.Max(edge, glow))*Smooth(0, 4, Math.Min(x, 95-x));
                double tint = Smooth(.12, .32, distance);
                bitmap.SetPixel(x, y, Color.FromArgb((int)Math.Round(255*alpha),
                    (int)Math.Round(233+(145-233)*tint),
                    (int)Math.Round(250+(219-250)*tint),
                    (int)Math.Round(255+(236-255)*tint)));
            }
            bitmap.Save(path, ImageFormat.Png);
        }
    }
}
'@
[DrawImageProfile]::Render((Join-Path $PSScriptRoot 'draw-line-profile.png'))
Write-Output 'Saved 96x64 RGBA Draw profile'
