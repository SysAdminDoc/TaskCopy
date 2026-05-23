# Generates a multi-resolution PNG-encoded ICO with the Catppuccin Mocha "T" glyph.
# Run from repo root: powershell -ExecutionPolicy Bypass -File tools\generate-icon.ps1

Add-Type -AssemblyName System.Drawing

function New-IconBitmap([int]$size) {
    $bmp = New-Object System.Drawing.Bitmap($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAlias
    $g.Clear([System.Drawing.Color]::Transparent)

    $bg = [System.Drawing.Color]::FromArgb(255, 30, 30, 46)
    $fg = [System.Drawing.Color]::FromArgb(255, 203, 166, 247)

    $bgBrush = New-Object System.Drawing.SolidBrush($bg)
    $rect = New-Object System.Drawing.RectangleF(0, 0, $size, $size)

    $r = [Math]::Max(2.0, [Math]::Round($size * 0.18))
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = $r * 2
    $path.AddArc($rect.Left, $rect.Top, $d, $d, 180, 90)
    $path.AddArc($rect.Right - $d, $rect.Top, $d, $d, 270, 90)
    $path.AddArc($rect.Right - $d, $rect.Bottom - $d, $d, $d, 0, 90)
    $path.AddArc($rect.Left, $rect.Bottom - $d, $d, $d, 90, 90)
    $path.CloseFigure()
    $g.FillPath($bgBrush, $path)

    $fontSize = [Math]::Max(8.0, [Math]::Round($size * 0.62))
    $font = New-Object System.Drawing.Font('Segoe UI', $fontSize, [System.Drawing.FontStyle]::Bold, [System.Drawing.GraphicsUnit]::Pixel)
    $fgBrush = New-Object System.Drawing.SolidBrush($fg)
    $sf = New-Object System.Drawing.StringFormat
    $sf.Alignment = [System.Drawing.StringAlignment]::Center
    $sf.LineAlignment = [System.Drawing.StringAlignment]::Center
    $textRect = New-Object System.Drawing.RectangleF(0, ($size * -0.03), $size, $size)
    $g.DrawString('T', $font, $fgBrush, $textRect, $sf)

    $bgBrush.Dispose(); $fgBrush.Dispose(); $font.Dispose(); $path.Dispose(); $g.Dispose()
    return $bmp
}

function Get-PngBytes($bmp) {
    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    return ,$ms.ToArray()
}

$sizes = @(16, 32, 48, 64, 128, 256)
$sizesList = New-Object System.Collections.Generic.List[int]
$payloads = New-Object System.Collections.Generic.List[byte[]]
foreach ($s in $sizes) {
    $bmp = New-IconBitmap $s
    $bytes = Get-PngBytes $bmp
    $bmp.Dispose()
    $sizesList.Add($s)
    $payloads.Add([byte[]]$bytes)
    Write-Host ("size={0} png={1} bytes" -f $s, $bytes.Length)
}

$ms = New-Object System.IO.MemoryStream
$bw = New-Object System.IO.BinaryWriter($ms)

# ICONDIR (6 bytes)
$bw.Write([UInt16]0)
$bw.Write([UInt16]1)
$bw.Write([UInt16]$sizesList.Count)

$offset = 6 + (16 * $sizesList.Count)
for ($i = 0; $i -lt $sizesList.Count; $i++) {
    $w = $sizesList[$i]
    $bytes = $payloads[$i]
    $byteW = if ($w -ge 256) { [Byte]0 } else { [Byte]$w }
    $bw.Write($byteW)               # width
    $bw.Write($byteW)               # height
    $bw.Write([Byte]0)              # color count
    $bw.Write([Byte]0)              # reserved
    $bw.Write([UInt16]1)            # planes
    $bw.Write([UInt16]32)           # bit count
    $bw.Write([UInt32]$bytes.Length)
    $bw.Write([UInt32]$offset)
    $offset += $bytes.Length
}

for ($i = 0; $i -lt $payloads.Count; $i++) {
    $bytes = $payloads[$i]
    $bw.Write($bytes, 0, $bytes.Length)
}
$bw.Flush()

$outDir = Join-Path $PSScriptRoot '..\src\TaskCopy\Assets'
$null = New-Item -ItemType Directory -Force -Path $outDir
$outPath = Join-Path $outDir 'app.ico'
[System.IO.File]::WriteAllBytes($outPath, $ms.ToArray())
Write-Host ("Wrote {0} bytes -> {1}" -f $ms.Length, $outPath)
