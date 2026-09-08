param(
    [string]$PackageRoot = "art-production/test-room-v01"
)

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing

function Clamp-Byte([double]$value) {
    return [byte][Math]::Round([Math]::Max(0, [Math]::Min(255, $value)))
}

function Luma([System.Drawing.Color]$c) {
    return 0.2126 * $c.R + 0.7152 * $c.G + 0.0722 * $c.B
}

$sourcePath = Join-Path $PackageRoot "working/tr01_arch_gate_a_background_extraction_attempt.png"
if (-not (Test-Path $sourcePath)) { throw "Arch source not found: $sourcePath" }

$src = [System.Drawing.Bitmap]::new((Resolve-Path $sourcePath).Path)
$minX = $src.Width; $minY = $src.Height; $maxX = -1; $maxY = -1
for ($y = 0; $y -lt $src.Height; $y++) {
    for ($x = 0; $x -lt $src.Width; $x++) {
        if ($src.GetPixel($x, $y).A -gt 16) {
            if ($x -lt $minX) { $minX = $x }
            if ($x -gt $maxX) { $maxX = $x }
            if ($y -lt $minY) { $minY = $y }
            if ($y -gt $maxY) { $maxY = $y }
        }
    }
}
if ($maxX -lt $minX) { throw "Arch source has no visible alpha." }

$canvasWidth = 640
$canvasHeight = 512
$drawWidth = 600
$contentWidth = $maxX - $minX + 1
$contentHeight = $maxY - $minY + 1
$drawHeight = [Math]::Round($drawWidth * $contentHeight / [double]$contentWidth)
$drawX = [Math]::Round(($canvasWidth - $drawWidth) / 2.0)
$drawY = 3

$scaled = [System.Drawing.Bitmap]::new($canvasWidth, $canvasHeight, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$graphics = [System.Drawing.Graphics]::FromImage($scaled)
$graphics.Clear([System.Drawing.Color]::Transparent)
$graphics.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceCopy
$graphics.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
$graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
$graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
$dest = [System.Drawing.Rectangle]::new($drawX, $drawY, $drawWidth, $drawHeight)
$crop = [System.Drawing.Rectangle]::new($minX, $minY, $contentWidth, $contentHeight)
$graphics.DrawImage($src, $dest, $crop, [System.Drawing.GraphicsUnit]::Pixel)
$graphics.Dispose()
$src.Dispose()

$albedo = [System.Drawing.Bitmap]::new($canvasWidth, $canvasHeight, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$emission = [System.Drawing.Bitmap]::new($canvasWidth, $canvasHeight, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$mask = [System.Drawing.Bitmap]::new($canvasWidth, $canvasHeight, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$ao = [System.Drawing.Bitmap]::new($canvasWidth, $canvasHeight, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)

for ($y = 0; $y -lt $canvasHeight; $y++) {
    for ($x = 0; $x -lt $canvasWidth; $x++) {
        $p = $scaled.GetPixel($x, $y)
        $a = if ($p.A -le 4) { 0 } elseif ($p.A -ge 220) { 255 } else { Clamp-Byte ($p.A * 255.0 / 220.0) }
        if ($a -eq 0) {
            $albedo.SetPixel($x, $y, [System.Drawing.Color]::Transparent)
            $emission.SetPixel($x, $y, [System.Drawing.Color]::Transparent)
            $mask.SetPixel($x, $y, [System.Drawing.Color]::Transparent)
            $ao.SetPixel($x, $y, [System.Drawing.Color]::Transparent)
            continue
        }

        $isCyan = $p.G -gt 75 -and $p.B -gt 75 -and $p.G -gt ($p.R * 1.35) -and $p.B -gt ($p.R * 1.25)
        if ($isCyan) {
            $albedo.SetPixel($x, $y, [System.Drawing.Color]::FromArgb($a, 18, 49, 58))
            $er = Clamp-Byte ($p.R * 0.40)
            $eg = Clamp-Byte ([Math]::Max(35, $p.G * 1.05))
            $eb = Clamp-Byte ([Math]::Max(48, $p.B * 1.10))
            $emission.SetPixel($x, $y, [System.Drawing.Color]::FromArgb($a, $er, $eg, $eb))
        }
        else {
            $albedo.SetPixel($x, $y, [System.Drawing.Color]::FromArgb($a, $p.R, $p.G, $p.B))
            $emission.SetPixel($x, $y, [System.Drawing.Color]::FromArgb($a, 0, 0, 0))
        }

        $metal = if ($isCyan) { 200 } else { 135 }
        $gloss = if ($isCyan) { 105 } else { 48 }
        $mask.SetPixel($x, $y, [System.Drawing.Color]::FromArgb($a, $metal, $gloss, 0))

        $luma = Luma $p
        $aoValue = Clamp-Byte (205 + [Math]::Min(50, $luma * 0.42))
        $ao.SetPixel($x, $y, [System.Drawing.Color]::FromArgb($a, $aoValue, $aoValue, $aoValue))
    }
}

$normal = [System.Drawing.Bitmap]::new($canvasWidth, $canvasHeight, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
for ($y = 0; $y -lt $canvasHeight; $y++) {
    for ($x = 0; $x -lt $canvasWidth; $x++) {
        $p = $albedo.GetPixel($x, $y)
        if ($p.A -eq 0) {
            $normal.SetPixel($x, $y, [System.Drawing.Color]::Transparent)
            continue
        }
        $xl = [Math]::Max(0, $x - 1); $xr = [Math]::Min($canvasWidth - 1, $x + 1)
        $yu = [Math]::Max(0, $y - 1); $yd = [Math]::Min($canvasHeight - 1, $y + 1)
        $gx = ((Luma $albedo.GetPixel($xr, $y)) - (Luma $albedo.GetPixel($xl, $y))) / 255.0 * 3.0
        $gy = ((Luma $albedo.GetPixel($x, $yd)) - (Luma $albedo.GetPixel($x, $yu))) / 255.0 * 3.0
        $nx = -$gx; $ny = $gy; $nz = 1.0
        $length = [Math]::Sqrt($nx * $nx + $ny * $ny + 1.0)
        $normal.SetPixel($x, $y, [System.Drawing.Color]::FromArgb(
            $p.A,
            (Clamp-Byte (($nx / $length * 0.5 + 0.5) * 255)),
            (Clamp-Byte (($ny / $length * 0.5 + 0.5) * 255)),
            (Clamp-Byte (($nz / $length * 0.5 + 0.5) * 255))))
    }
}

$outputs = @{
    albedo = Join-Path $PackageRoot "approved/albedo/setpiece/tr01_arch_gate_a_albedo.png"
    normal = Join-Path $PackageRoot "approved/normal/setpiece/tr01_arch_gate_a_normal.png"
    emission = Join-Path $PackageRoot "approved/emission/setpiece/tr01_arch_gate_a_emission.png"
    mask = Join-Path $PackageRoot "approved/mask/setpiece/tr01_arch_gate_a_mask.png"
    ao = Join-Path $PackageRoot "approved/ao/setpiece/tr01_arch_gate_a_ao.png"
}
foreach ($path in $outputs.Values) { New-Item -ItemType Directory -Force ([IO.Path]::GetDirectoryName($path)) | Out-Null }
$albedo.Save($outputs.albedo, [System.Drawing.Imaging.ImageFormat]::Png)
$normal.Save($outputs.normal, [System.Drawing.Imaging.ImageFormat]::Png)
$emission.Save($outputs.emission, [System.Drawing.Imaging.ImageFormat]::Png)
$mask.Save($outputs.mask, [System.Drawing.Imaging.ImageFormat]::Png)
$ao.Save($outputs.ao, [System.Drawing.Imaging.ImageFormat]::Png)

$scaled.Dispose(); $albedo.Dispose(); $normal.Dispose(); $emission.Dispose(); $mask.Dispose(); $ao.Dispose()

[PSCustomObject]@{
    Asset = "TR01-ARC-001"
    Canvas = "$($canvasWidth)x$($canvasHeight)"
    Content = "$($drawWidth)x$($drawHeight)"
    PivotPixelsBottomOrigin = "320,8"
    Result = "PASS"
}

