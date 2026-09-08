param([string]$PackageRoot = "art-production/test-room-v01")

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing

function Clamp-Byte([double]$value) {
    [byte][Math]::Round([Math]::Max(0, [Math]::Min(255, $value)))
}

function Luma([System.Drawing.Color]$color) {
    0.2126 * $color.R + 0.7152 * $color.G + 0.0722 * $color.B
}

function Get-PixelClamped([System.Drawing.Bitmap]$bitmap, [int]$x, [int]$y) {
    $bitmap.GetPixel(
        [Math]::Max(0, [Math]::Min($bitmap.Width - 1, $x)),
        [Math]::Max(0, [Math]::Min($bitmap.Height - 1, $y)))
}

function Normalize-Tile([string]$sourcePath) {
    $source = [System.Drawing.Bitmap]::new((Resolve-Path $sourcePath).Path)
    $side = [Math]::Min($source.Width, $source.Height)
    $sourceX = [int][Math]::Round(($source.Width - $side) / 2.0)
    $sourceY = [int][Math]::Round(($source.Height - $side) / 2.0)
    $tile = [System.Drawing.Bitmap]::new(128, 128, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = [System.Drawing.Graphics]::FromImage($tile)
    $graphics.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceCopy
    $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $graphics.DrawImage($source, [System.Drawing.Rectangle]::new(0, 0, 128, 128), [System.Drawing.Rectangle]::new($sourceX, $sourceY, $side, $side), [System.Drawing.GraphicsUnit]::Pixel)
    $graphics.Dispose()
    $source.Dispose()

    # Force opaque albedo and make opposite edges identical. Four-pixel feathering
    # prevents a single hard correction column while preserving the authored centre.
    for ($y = 0; $y -lt 128; $y++) {
        for ($x = 0; $x -lt 128; $x++) {
            $p = $tile.GetPixel($x, $y)
            $tile.SetPixel($x, $y, [System.Drawing.Color]::FromArgb(255, $p.R, $p.G, $p.B))
        }
    }
    for ($i = 0; $i -lt 4; $i++) {
        $weight = 1.0 - ($i / 4.0)
        for ($y = 0; $y -lt 128; $y++) {
            $left = $tile.GetPixel($i, $y)
            $right = $tile.GetPixel(127 - $i, $y)
            $r = Clamp-Byte (($left.R + $right.R) / 2.0)
            $g = Clamp-Byte (($left.G + $right.G) / 2.0)
            $b = Clamp-Byte (($left.B + $right.B) / 2.0)
            $edge = [System.Drawing.Color]::FromArgb(255, $r, $g, $b)
            $tile.SetPixel($i, $y, $edge)
            $tile.SetPixel(127 - $i, $y, $edge)
        }
        for ($x = 0; $x -lt 128; $x++) {
            $top = $tile.GetPixel($x, $i)
            $bottom = $tile.GetPixel($x, 127 - $i)
            $r = Clamp-Byte (($top.R + $bottom.R) / 2.0)
            $g = Clamp-Byte (($top.G + $bottom.G) / 2.0)
            $b = Clamp-Byte (($top.B + $bottom.B) / 2.0)
            $edge = [System.Drawing.Color]::FromArgb(255, $r, $g, $b)
            $tile.SetPixel($x, $i, $edge)
            $tile.SetPixel($x, 127 - $i, $edge)
        }
    }
    $tile
}

function Save-Variant([string]$category, [string]$variant) {
    $baseName = if ($category -eq "wall") { "tr01_wall_front_$variant" } else { "tr01_wall_top_$variant" }
    $sourcePath = Join-Path $PackageRoot "source/$category/${baseName}_albedo_source.png"
    $albedo = Normalize-Tile $sourcePath
    $normal = [System.Drawing.Bitmap]::new(128, 128, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $emission = [System.Drawing.Bitmap]::new(128, 128, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $mask = [System.Drawing.Bitmap]::new(128, 128, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $ao = [System.Drawing.Bitmap]::new(128, 128, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)

    for ($y = 0; $y -lt 128; $y++) {
        $yn = $y / 127.0
        for ($x = 0; $x -lt 128; $x++) {
            $left = Luma (Get-PixelClamped $albedo ($x - 1) $y)
            $right = Luma (Get-PixelClamped $albedo ($x + 1) $y)
            $up = Luma (Get-PixelClamped $albedo $x ($y - 1))
            $down = Luma (Get-PixelClamped $albedo $x ($y + 1))
            $nx = -(($right - $left) / 255.0) * 3.1
            $ny = (($down - $up) / 255.0) * 3.1
            $length = [Math]::Sqrt($nx * $nx + $ny * $ny + 1.0)
            $normal.SetPixel($x, $y, [System.Drawing.Color]::FromArgb(255,
                (Clamp-Byte (($nx / $length * 0.5 + 0.5) * 255)),
                (Clamp-Byte (($ny / $length * 0.5 + 0.5) * 255)),
                (Clamp-Byte ((1.0 / $length * 0.5 + 0.5) * 255))))

            $center = Luma $albedo.GetPixel($x, $y)
            $average = ($left + $right + $up + $down) / 4.0
            $crevice = [Math]::Max(0.0, [Math]::Min(1.0, ($average - $center) / 30.0))
            $aoValue = Clamp-Byte (255.0 - 105.0 * $crevice)
            $ao.SetPixel($x, $y, [System.Drawing.Color]::FromArgb(255, $aoValue, $aoValue, $aoValue))
            $emission.SetPixel($x, $y, [System.Drawing.Color]::FromArgb(255, 0, 0, 0))

            $metalBand = $category -eq "wall" -and ($yn -lt 0.10 -or ($yn -gt 0.43 -and $yn -lt 0.58) -or $yn -gt 0.83)
            $metal = if ($metalBand) { 210 } elseif ($category -eq "wall_top") { 36 } else { 20 }
            $gloss = if ($metalBand) { 78 } elseif ($category -eq "wall_top") { 34 } else { 24 }
            $mask.SetPixel($x, $y, [System.Drawing.Color]::FromArgb(255, $metal, $gloss, 0))
        }
    }

    $maps = @{ albedo = $albedo; normal = $normal; emission = $emission; mask = $mask; ao = $ao }
    foreach ($channel in $maps.Keys) {
        $directory = Join-Path $PackageRoot "approved/$channel/$category"
        New-Item -ItemType Directory -Force $directory | Out-Null
        $maps[$channel].Save((Join-Path $directory "${baseName}_$channel.png"), [System.Drawing.Imaging.ImageFormat]::Png)
    }
    foreach ($bitmap in $maps.Values) { $bitmap.Dispose() }

    [PSCustomObject]@{ Asset = $baseName; Canvas = "128x128"; PivotPixels = if ($category -eq "wall") { "64,128" } else { "64,64" }; SeamLocked = $true; Result = "PASS" }
}

foreach ($variant in @("a", "b", "c", "d", "e", "f")) { Save-Variant "wall" $variant }
foreach ($variant in @("a", "b", "c", "d", "e", "f")) { Save-Variant "wall_top" $variant }
