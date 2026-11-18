# Renders the monogram logo (same geometry as Assets/icon.svg) to icon.png (512) and a multi-size icon.ico.
# Run after changing the logo: powershell -File .claude/scripts/make-icon.ps1
Add-Type -AssemblyName System.Drawing
$assets = Join-Path $PSScriptRoot '..\..\src\Presentation\CodescoveryCaptureManager.App\Assets'

function Add-RoundedRect($path, [single]$x, [single]$y, [single]$w, [single]$h, [single]$r) {
  $d = 2 * $r
  $path.AddArc($x, $y, $d, $d, 180, 90)
  $path.AddArc($x + $w - $d, $y, $d, $d, 270, 90)
  $path.AddArc($x + $w - $d, $y + $h - $d, $d, $d, 0, 90)
  $path.AddArc($x, $y + $h - $d, $d, $d, 90, 90)
  $path.CloseFigure()
}

function New-Logo([int]$size) {
  $bitmap = New-Object Drawing.Bitmap $size, $size, ([Drawing.Imaging.PixelFormat]::Format32bppArgb)
  $g = [Drawing.Graphics]::FromImage($bitmap)
  $g.SmoothingMode = 'AntiAlias'; $g.PixelOffsetMode = 'HighQuality'
  $g.ScaleTransform($size / 512, $size / 512)

  $tile = New-Object Drawing.Drawing2D.GraphicsPath
  Add-RoundedRect $tile 0 0 512 512 116
  $g.FillPath((New-Object Drawing.SolidBrush ([Drawing.ColorTranslator]::FromHtml('#171A14'))), $tile)

  $letter = New-Object Drawing.Drawing2D.GraphicsPath
  $letter.AddLine(372, 150, 196, 150)
  $letter.AddArc(140, 150, 112, 112, 270, -90)
  $letter.AddLine(140, 206, 140, 306)
  $letter.AddArc(140, 250, 112, 112, 180, -90)
  $letter.AddLine(196, 362, 372, 362)
  $green = New-Object Drawing.Drawing2D.LinearGradientBrush (
    (New-Object Drawing.PointF 114, 124), (New-Object Drawing.PointF 398, 388),
    [Drawing.ColorTranslator]::FromHtml('#A3D85A'), [Drawing.ColorTranslator]::FromHtml('#6FA82E'))
  $pen = New-Object Drawing.Pen $green, 52
  $pen.StartCap = 'Round'; $pen.EndCap = 'Round'; $pen.LineJoin = 'Round'
  $g.DrawPath($pen, $letter)

  $window = New-Object Drawing.Drawing2D.GraphicsPath
  Add-RoundedRect $window 236 216 150 80 20
  $g.FillPath([Drawing.Brushes]::White, $window)
  $g.Dispose()
  $bitmap
}

function Get-PngBytes($bitmap) {
  $stream = New-Object IO.MemoryStream
  $bitmap.Save($stream, [Drawing.Imaging.ImageFormat]::Png)
  , $stream.ToArray()
}

(New-Logo 512).Save((Join-Path $assets 'icon.png'), [Drawing.Imaging.ImageFormat]::Png)

# ICO container with PNG-compressed entries (Vista+).
$sizes = 16, 20, 24, 32, 40, 48, 64, 128, 256
$images = $sizes | ForEach-Object { , (Get-PngBytes (New-Logo $_)) }
$out = New-Object IO.MemoryStream
$writer = New-Object IO.BinaryWriter $out
$writer.Write([uint16]0); $writer.Write([uint16]1); $writer.Write([uint16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
  $dimension = if ($sizes[$i] -ge 256) { 0 } else { $sizes[$i] }
  $writer.Write([byte]$dimension); $writer.Write([byte]$dimension); $writer.Write([byte]0); $writer.Write([byte]0)
  $writer.Write([uint16]1); $writer.Write([uint16]32)
  $writer.Write([uint32]$images[$i].Length); $writer.Write([uint32]$offset)
  $offset += $images[$i].Length
}
foreach ($image in $images) { $writer.Write($image) }
$writer.Flush()
[IO.File]::WriteAllBytes((Join-Path $assets 'icon.ico'), $out.ToArray())
"icon.png + icon.ico written ($($sizes -join ', '))"
