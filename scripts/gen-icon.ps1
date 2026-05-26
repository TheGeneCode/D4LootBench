Add-Type -AssemblyName System.Drawing

$script:sc = 1.0
function S([double]$v) { return [float]($v * $script:sc) }

function Pt([double]$x, [double]$y) {
    return [System.Drawing.PointF]::new([float]($x * $script:sc), [float]($y * $script:sc))
}

function New-RoundedRectPath([float]$x, [float]$y, [float]$w, [float]$h, [float]$r) {
    $p = New-Object System.Drawing.Drawing2D.GraphicsPath
    $p.AddArc($x,           $y,           2*$r, 2*$r, 180, 90)
    $p.AddArc($x+$w-2*$r,  $y,           2*$r, 2*$r, 270, 90)
    $p.AddArc($x+$w-2*$r,  $y+$h-2*$r,  2*$r, 2*$r,   0, 90)
    $p.AddArc($x,           $y+$h-2*$r,  2*$r, 2*$r,  90, 90)
    $p.CloseFigure()
    return $p
}

function Draw-D4LootBenchIcon([System.Drawing.Graphics]$g, [int]$sz) {
    $script:sc = $sz / 256.0

    $g.SmoothingMode      = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality

    # ── Background ──────────────────────────────────────────────────────────
    $bgPath = New-RoundedRectPath 0 0 $sz $sz (S 34)
    $bgBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 13, 6, 1))
    $g.FillPath($bgBrush, $bgPath)
    $borderPen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(255, 50, 23, 5), (S 5))
    $g.DrawPath($borderPen, $bgPath)

    # ── Corner bracket ornaments (≥ 48px) ───────────────────────────────────
    if ($sz -ge 48) {
        $ornPen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(190, 88, 44, 10), (S 2.5))
        $o  = S 26; $e = S 44
        $ri = [float]($sz - (S 26)); $bi = [float]($sz - (S 26))
        $ie = [float]($sz - (S 44))
        $g.DrawLine($ornPen, $o, $e, $o, $o);  $g.DrawLine($ornPen, $o, $o, $e, $o)
        $g.DrawLine($ornPen, $ri, $e, $ri, $o); $g.DrawLine($ornPen, $ri, $o, $ie, $o)
        $g.DrawLine($ornPen, $o, $bi, $o, $ie); $g.DrawLine($ornPen, $o, $bi, $e, $bi)
        $g.DrawLine($ornPen, $ri, $bi, $ri, $ie); $g.DrawLine($ornPen, $ri, $bi, $ie, $bi)
        $ornPen.Dispose()
    }

    # ── Funnel trapezoid ─────────────────────────────────────────────────────
    # base coords (256×256): top 46→210, narrows to 103→153 at y=148, neck to y=181
    $fPts = [System.Drawing.PointF[]]@(
        (Pt 46  52), (Pt 210 52), (Pt 153 148), (Pt 103 148)
    )

    $goldBrush = New-Object System.Drawing.Drawing2D.LinearGradientBrush `
        -ArgumentList @(
            [System.Drawing.PointF]::new((S 128), (S 52)),
            [System.Drawing.PointF]::new((S 128), (S 182)),
            [System.Drawing.Color]::FromArgb(255, 245, 204, 72),
            [System.Drawing.Color]::FromArgb(255, 108, 64, 10)
        )

    $g.FillPolygon($goldBrush, $fPts)
    $g.FillRectangle($goldBrush, (S 103), (S 147), (S 50), (S 35))

    # Top-edge highlight
    $hlPen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(110, 255, 236, 150), (S 2.5))
    $g.DrawLine($hlPen, (S 50), (S 54), (S 206), (S 54))
    $hlPen.Dispose()

    # ── Filter gap bars clipped to funnel (≥ 32px) ───────────────────────────
    if ($sz -ge 32) {
        $clipPath = New-Object System.Drawing.Drawing2D.GraphicsPath
        $clipPath.AddPolygon($fPts)
        $g.SetClip($clipPath)

        $gapBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 11, 5, 1))
        $gH = [float][Math]::Max(1.5, (S 9))
        $g.FillRectangle($gapBrush, (S 28), (S  80), (S 200), $gH)
        $g.FillRectangle($gapBrush, (S 28), (S 106), (S 200), $gH)
        if ($sz -ge 48) {
            $g.FillRectangle($gapBrush, (S 28), (S 132), (S 200), $gH)
        }
        $g.ResetClip()
        $gapBrush.Dispose()
        $clipPath.Dispose()
    }

    # ── Loot gem (≥ 48px) ────────────────────────────────────────────────────
    if ($sz -ge 48) {
        $dripPen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(170, 190, 120, 28), (S 3))
        $g.DrawLine($dripPen, (S 128), (S 182), (S 128), (S 198))
        $dripPen.Dispose()

        $gPts = [System.Drawing.PointF[]]@(
            (Pt 128 198), (Pt 147 217), (Pt 128 236), (Pt 109 217)
        )
        $gemBrush = New-Object System.Drawing.Drawing2D.LinearGradientBrush `
            -ArgumentList @(
                [System.Drawing.PointF]::new((S 109), (S 198)),
                [System.Drawing.PointF]::new((S 147), (S 236)),
                [System.Drawing.Color]::FromArgb(255, 255, 218, 80),
                [System.Drawing.Color]::FromArgb(255, 198, 56, 0)
            )
        $g.FillPolygon($gemBrush, $gPts)

        # Facet highlight
        $hPts = [System.Drawing.PointF[]]@(
            (Pt 128 198), (Pt 147 217), (Pt 128 210), (Pt 116 204)
        )
        $hlBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(128, 255, 245, 192))
        $g.FillPolygon($hlBrush, $hPts)

        $spR = [float](S 3)
        $sparkBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(180, 255, 252, 220))
        $g.FillEllipse($sparkBrush, [float]((S 128)-$spR), [float]((S 217)-$spR), $spR*2, $spR*2)

        $gemBrush.Dispose()
        $hlBrush.Dispose()
        $sparkBrush.Dispose()
    }

    $bgBrush.Dispose(); $bgPath.Dispose(); $borderPen.Dispose(); $goldBrush.Dispose()
}

# ── Render sizes ─────────────────────────────────────────────────────────────
$sizes   = @(16, 32, 48, 256)
$pngData = [System.Collections.Generic.Dictionary[int,byte[]]]::new()

foreach ($sz in $sizes) {
    $bmp = New-Object System.Drawing.Bitmap($sz, $sz, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g   = [System.Drawing.Graphics]::FromImage($bmp)
    $g.Clear([System.Drawing.Color]::Transparent)
    Draw-D4LootBenchIcon $g $sz
    $g.Dispose()
    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $pngData[$sz] = $ms.ToArray()
    $ms.Dispose(); $bmp.Dispose()
    Write-Host "  Rendered ${sz}x${sz} ($($pngData[$sz].Length) bytes)"
}

# ── Write ICO (PNG-in-ICO, Vista+ format) ────────────────────────────────────
$assetsDir = Join-Path $PSScriptRoot "..\src\D4LootBench.App\Assets"
if (-not (Test-Path $assetsDir)) { New-Item -ItemType Directory $assetsDir | Out-Null }

$out    = New-Object System.IO.MemoryStream
$writer = New-Object System.IO.BinaryWriter($out)

$writer.Write([uint16]0)
$writer.Write([uint16]1)
$writer.Write([uint16]$sizes.Count)

$offset = [uint32](6 + 16 * $sizes.Count)
foreach ($sz in $sizes) {
    $dim = if ($sz -eq 256) { [byte]0 } else { [byte]$sz }
    $writer.Write([byte]$dim)
    $writer.Write([byte]$dim)
    $writer.Write([byte]0)
    $writer.Write([byte]0)
    $writer.Write([uint16]1)
    $writer.Write([uint16]32)
    $writer.Write([uint32]$pngData[$sz].Length)
    $writer.Write([uint32]$offset)
    $offset += [uint32]$pngData[$sz].Length
}

foreach ($sz in $sizes) { $writer.Write($pngData[$sz]) }

$writer.Flush()
$iconPath = Join-Path $assetsDir "d4lootbench.ico"
[System.IO.File]::WriteAllBytes($iconPath, $out.ToArray())
$writer.Dispose(); $out.Dispose()

Write-Host "Icon written: $iconPath ($([System.IO.File]::ReadAllBytes($iconPath).Length) bytes)"
