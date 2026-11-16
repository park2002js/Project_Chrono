Add-Type -AssemblyName System.Drawing
$outDir = $PSScriptRoot
$width = 2400
$height = 1200
$bitmap = New-Object Drawing.Bitmap($width, $height, ([Drawing.Imaging.PixelFormat]::Format32bppArgb))
$graphics = [Drawing.Graphics]::FromImage($bitmap)
$graphics.Clear([Drawing.Color]::Transparent)
$graphics.SmoothingMode = [Drawing.Drawing2D.SmoothingMode]::AntiAlias
$graphics.TextRenderingHint = [Drawing.Text.TextRenderingHint]::AntiAliasGridFit
$pen = New-Object Drawing.Pen([Drawing.Color]::White, 5)
$pen.StartCap = [Drawing.Drawing2D.LineCap]::Round
$pen.EndCap = [Drawing.Drawing2D.LineCap]::Round
$brush = [Drawing.Brushes]::White
$svg = [Collections.Generic.List[string]]::new()
$svg.Add('<svg xmlns="http://www.w3.org/2000/svg" width="2400" height="1200" viewBox="0 0 2400 1200">')
$svg.Add('<g fill="none" stroke="white" stroke-width="5" stroke-linecap="round" stroke-linejoin="round">')
function Draw-Line([float]$x1,[float]$y1,[float]$x2,[float]$y2) {
    $graphics.DrawLine($pen,$x1,$y1,$x2,$y2)
    $svg.Add("<path d=`"M $x1 $y1 L $x2 $y2`"/>")
}
function Draw-RoundRect([float]$x,[float]$y,[float]$w,[float]$h,[float]$radius) {
    $path = [Drawing.Drawing2D.GraphicsPath]::new()
    $diameter = 2 * $radius
    $path.AddArc($x,$y,$diameter,$diameter,180,90)
    $path.AddArc(($x+$w-$diameter),$y,$diameter,$diameter,270,90)
    $path.AddArc(($x+$w-$diameter),($y+$h-$diameter),$diameter,$diameter,0,90)
    $path.AddArc($x,($y+$h-$diameter),$diameter,$diameter,90,90)
    $path.CloseFigure()
    $graphics.DrawPath($pen,$path)
    $path.Dispose()
    $svg.Add("<rect x=`"$x`" y=`"$y`" width=`"$w`" height=`"$h`" rx=`"$radius`"/>")
}
function Draw-Text([string]$text,[float]$x,[float]$y,[float]$size,[string]$align='center') {
    $font = [Drawing.Font]::new('Malgun Gothic',$size,[Drawing.FontStyle]::Regular,[Drawing.GraphicsUnit]::Pixel)
    $format = [Drawing.StringFormat]::new()
    $format.Alignment = switch($align) {'left' {[Drawing.StringAlignment]::Near}; 'right' {[Drawing.StringAlignment]::Far}; default {[Drawing.StringAlignment]::Center}}
    $format.LineAlignment = [Drawing.StringAlignment]::Center
    $graphics.DrawString($text,$font,$brush,[Drawing.PointF]::new($x,$y),$format)
    $anchor = switch($align) {'left' {'start'}; 'right' {'end'}; default {'middle'}}
    $safe = [Security.SecurityElement]::Escape($text)
    $svg.Add("<text x=`"$x`" y=`"$y`" fill=`"white`" stroke=`"none`" text-anchor=`"$anchor`" dominant-baseline=`"central`" font-family=`"Malgun Gothic, sans-serif`" font-size=`"$size`">$safe</text>")
    $font.Dispose()
    $format.Dispose()
}
function Draw-Key([string]$letter,[float]$x,[float]$y,[float]$w=110,[float]$h=110) {
    Draw-RoundRect $x $y $w $h 13
    Draw-Text $letter ($x+$w/2) ($y+$h/2-2) 55
}

# Required keys retain their keyboard positions; omitted keys leave empty space
Draw-Key 'W' 660 300
Draw-Key 'R' 990 300
Draw-Key 'A' 590 440
Draw-Key 'S' 720 440
Draw-Key 'D' 850 440
Draw-Key 'F' 980 440
Draw-Key 'SHIFT' 450 600 260 110
Draw-Key 'SPACE' 730 760 640 110

# Straight callouts lead directly from keys to their labels
Draw-Line 715 300 715 250
Draw-Line 715 250 470 250
Draw-Text '이동 / —' 470 200 44
Draw-Line 1045 300 1045 225
Draw-Text '— / 점 초기화' 1045 180 44
Draw-Line 1090 495 1180 495
Draw-Text '공간참 / 선 확정' 1215 495 44 'left'
Draw-Line 450 655 410 655
Draw-Text '선 그리기 / 취소' 380 655 44 'right'
Draw-Line 1050 870 1050 960
Draw-Text '대시 / —' 1050 1010 44

# Mouse body: transparent left/right buttons and a small wheel
$mouse = [Drawing.Drawing2D.GraphicsPath]::new()
$mouse.AddBezier(1900,525,1900,331,2150,331,2150,525)
$mouse.AddLine(2150,525,2150,655)
$mouse.AddBezier(2150,655,2150,875,1900,875,1900,655)
$mouse.CloseFigure()
$graphics.DrawPath($pen,$mouse)
$mouse.Dispose()
$svg.Add('<path d="M1900 525 C1900 331 2150 331 2150 525 L2150 655 C2150 875 1900 875 1900 655 Z"/>')
Draw-Line 2025 380 2025 445
Draw-RoundRect 2011 445 28 65 14
Draw-Line 2025 510 2025 570
Draw-Line 1900 570 2150 570
Draw-Line 1960 490 1960 315
Draw-Text '공격 / 점 찍기' 1960 265 44

# Reading order for the two states
Draw-Text '일반 상태 / 선 그리기 상태' 1200 1130 32
$svg.Add('</g></svg>')
[IO.File]::WriteAllLines((Join-Path $outDir 'controls-guide-white.svg'),$svg,[Text.UTF8Encoding]::new($false))
$pngPath = Join-Path $outDir 'controls-guide-transparent.png'
$bitmap.Save($pngPath,[Drawing.Imaging.ImageFormat]::Png)

# A separate dark preview does not alter the transparent deliverable
$preview = [Drawing.Bitmap]::new($width,$height)
$pg = [Drawing.Graphics]::FromImage($preview)
$pg.Clear([Drawing.Color]::FromArgb(12,12,14))
$pg.DrawImageUnscaled($bitmap,0,0)
$preview.Save((Join-Path $outDir 'preview-on-dark.png'),[Drawing.Imaging.ImageFormat]::Png)
$pg.Dispose()
$preview.Dispose()

# Check real alpha and absence of non-white foreground pixels
$transparent = 0
$visible = 0
$nonWhite = 0
for($y=0;$y -lt $height;$y++) {
    for($x=0;$x -lt $width;$x++) {
        $pixel = $bitmap.GetPixel($x,$y)
        if($pixel.A -eq 0) {$transparent++} else {
            $visible++
            if($pixel.R -ne 255 -or $pixel.G -ne 255 -or $pixel.B -ne 255) {$nonWhite++}
        }
    }
}
$result = [ordered]@{width=$width;height=$height;transparentPixels=$transparent;visiblePixels=$visible;nonWhiteVisiblePixels=$nonWhite;cornerAlpha=$bitmap.GetPixel(0,0).A;keyInteriorAlpha=$bitmap.GetPixel(670,320).A;mouseInteriorAlpha=$bitmap.GetPixel(1940,620).A}
$result | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $outDir 'verification.json') -Encoding UTF8
$result | ConvertTo-Json
$graphics.Dispose()
$bitmap.Dispose()
$pen.Dispose()
