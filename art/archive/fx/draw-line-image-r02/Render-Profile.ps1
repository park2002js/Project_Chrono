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
                double border = Smooth(.26, .4, distance);
                double outside = Smooth(.6, .9, distance);
                double alpha = Math.Min(1, 1-Smooth(.58, .76, distance)
                    + .22*Math.Exp(-distance*distance/.45)*(1-Smooth(.85, .98, distance)));
                double red = 17+(7-17)*border;
                double green = 206+(48-206)*border;
                double blue = 233+(68-233)*border;
                bitmap.SetPixel(x, y, Color.FromArgb((int)Math.Round(255*alpha),
                    (int)Math.Round(red+(20-red)*outside),
                    (int)Math.Round(green+(141-green)*outside),
                    (int)Math.Round(blue+(164-blue)*outside)));
            }
            bitmap.Save(path, ImageFormat.Png);
        }
    }
}
'@
[DrawImageProfile]::Render((Join-Path $PSScriptRoot 'draw-line-profile.png'))
Write-Output 'Saved 96x64 RGBA Draw profile with saturated cyan and dark outline'
