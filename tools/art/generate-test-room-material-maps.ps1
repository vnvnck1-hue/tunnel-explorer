param(
    [string]$PackageRoot = "art-production/test-room-v01"
)

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing

$albedoRoot = Join-Path $PackageRoot "approved/albedo"
$normalRoot = Join-Path $PackageRoot "approved/normal"
$emissionRoot = Join-Path $PackageRoot "approved/emission"
$maskRoot = Join-Path $PackageRoot "approved/mask"
$aoRoot = Join-Path $PackageRoot "approved/ao"

function Clamp-Byte([double]$value) {
    return [byte][Math]::Round([Math]::Max(0, [Math]::Min(255, $value)))
}

function Luma([System.Drawing.Color]$c) {
    return 0.2126 * $c.R + 0.7152 * $c.G + 0.0722 * $c.B
}

function Pixel-Clamped([System.Drawing.Bitmap]$bmp, [int]$x, [int]$y) {
    $cx = [Math]::Max(0, [Math]::Min($bmp.Width - 1, $x))
    $cy = [Math]::Max(0, [Math]::Min($bmp.Height - 1, $y))
    return $bmp.GetPixel($cx, $cy)
}

function Save-Maps([System.IO.FileInfo]$file) {
    $relative = $file.FullName.Substring((Resolve-Path $albedoRoot).Path.Length).TrimStart('\', '/')
    $category = ($relative -split '[\\/]')[0]
    $baseName = [IO.Path]::GetFileNameWithoutExtension($file.Name) -replace '_albedo$', ''
    $subdir = [IO.Path]::GetDirectoryName($relative)

    $targets = @{
        normal = Join-Path (Join-Path $normalRoot $subdir) ($baseName + "_normal.png")
        emission = Join-Path (Join-Path $emissionRoot $subdir) ($baseName + "_emission.png")
        mask = Join-Path (Join-Path $maskRoot $subdir) ($baseName + "_mask.png")
        ao = Join-Path (Join-Path $aoRoot $subdir) ($baseName + "_ao.png")
    }
    foreach ($target in $targets.Values) {
        New-Item -ItemType Directory -Force ([IO.Path]::GetDirectoryName($target)) | Out-Null
    }

    $src = [System.Drawing.Bitmap]::new($file.FullName)
    $normal = [System.Drawing.Bitmap]::new($src.Width, $src.Height, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $emission = [System.Drawing.Bitmap]::new($src.Width, $src.Height, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $mask = [System.Drawing.Bitmap]::new($src.Width, $src.Height, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $ao = [System.Drawing.Bitmap]::new($src.Width, $src.Height, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)

    $normalStrength = if ($category -eq 'floor') { 2.2 } else { 3.4 }

    for ($y = 0; $y -lt $src.Height; $y++) {
        $yn = $y / [double]($src.Height - 1)
        for ($x = 0; $x -lt $src.Width; $x++) {
            $left = Luma (Pixel-Clamped $src ($x - 1) $y)
            $right = Luma (Pixel-Clamped $src ($x + 1) $y)
            $up = Luma (Pixel-Clamped $src $x ($y - 1))
            $down = Luma (Pixel-Clamped $src $x ($y + 1))

            $nx = -(($right - $left) / 255.0) * $normalStrength
            $ny = (($down - $up) / 255.0) * $normalStrength
            $nz = 1.0
            $length = [Math]::Sqrt($nx * $nx + $ny * $ny + $nz * $nz)
            $normal.SetPixel($x, $y, [System.Drawing.Color]::FromArgb(
                255,
                (Clamp-Byte (($nx / $length * 0.5 + 0.5) * 255)),
                (Clamp-Byte (($ny / $length * 0.5 + 0.5) * 255)),
                (Clamp-Byte (($nz / $length * 0.5 + 0.5) * 255))))

            $centerLuma = Luma $src.GetPixel($x, $y)
            $sum = 0.0
            $samples = 0
            for ($oy = -2; $oy -le 2; $oy += 2) {
                for ($ox = -2; $ox -le 2; $ox += 2) {
                    $sum += Luma (Pixel-Clamped $src ($x + $ox) ($y + $oy))
                    $samples++
                }
            }
            $localAverage = $sum / $samples
            $crevice = [Math]::Max(0.0, [Math]::Min(1.0, ($localAverage - $centerLuma) / 36.0))
            $aoValue = 255.0 - 105.0 * $crevice
            if ($category -eq 'wall' -and $yn -gt 0.88) { $aoValue *= 0.68 }
            $a = Clamp-Byte $aoValue
            $ao.SetPixel($x, $y, [System.Drawing.Color]::FromArgb(255, $a, $a, $a))

            $metal = 0
            $gloss = 20
            if ($category -eq 'wall') {
                if ($yn -lt 0.10 -or ($yn -gt 0.45 -and $yn -lt 0.58) -or $yn -gt 0.82) {
                    $metal = 210
                    $gloss = 78
                }
                else {
                    $metal = 18
                    $gloss = 24
                }
            }
            elseif ($category -eq 'wall_top_rim') {
                if ($yn -lt 0.06) { $metal = 220; $gloss = 88 }
                else { $metal = 28; $gloss = 30 }
            }
            elseif ($category -eq 'wall_top') {
                $metal = 28
                $gloss = 30
            }

            $mask.SetPixel($x, $y, [System.Drawing.Color]::FromArgb(255, $metal, $gloss, 0))
            $emission.SetPixel($x, $y, [System.Drawing.Color]::FromArgb(255, 0, 0, 0))
        }
    }

    $normal.Save($targets.normal, [System.Drawing.Imaging.ImageFormat]::Png)
    $emission.Save($targets.emission, [System.Drawing.Imaging.ImageFormat]::Png)
    $mask.Save($targets.mask, [System.Drawing.Imaging.ImageFormat]::Png)
    $ao.Save($targets.ao, [System.Drawing.Imaging.ImageFormat]::Png)

    $src.Dispose()
    $normal.Dispose()
    $emission.Dispose()
    $mask.Dispose()
    $ao.Dispose()

    [PSCustomObject]@{
        Asset = $baseName
        Category = $category
        Width = 128
        Height = 128
    }
}

if (-not (Test-Path $albedoRoot)) {
    throw "Albedo root not found: $albedoRoot"
}

Get-ChildItem $albedoRoot -Recurse -File -Filter '*.png' |
    Sort-Object FullName |
    ForEach-Object { Save-Maps $_ }

# EnvironmentKit.contactAo용 별도 RGBA 스프라이트. 벽과 맞닿는 북쪽 가장자리에서 시작해
# 셀 높이의 약 42% 안에서 사라지며, 좌우 끝은 이웃 셀과 연결되도록 동일한 알파를 유지한다.
$contactAoDir = Join-Path $albedoRoot "contact_ao"
New-Item -ItemType Directory -Force $contactAoDir | Out-Null
$contactAoPath = Join-Path $contactAoDir "tr01_contact_ao_a_albedo.png"
$contactAo = [System.Drawing.Bitmap]::new(128, 128, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
for ($y = 0; $y -lt 128; $y++) {
    $distance = $y / 54.0
    $falloff = [Math]::Max(0.0, 1.0 - $distance)
    $falloff = $falloff * $falloff * (3.0 - 2.0 * $falloff)
    $alpha = Clamp-Byte (150.0 * $falloff)
    for ($x = 0; $x -lt 128; $x++) {
        $contactAo.SetPixel($x, $y, [System.Drawing.Color]::FromArgb($alpha, 0, 0, 0))
    }
}
$contactAo.Save($contactAoPath, [System.Drawing.Imaging.ImageFormat]::Png)
$contactAo.Dispose()

[PSCustomObject]@{
    Asset = "tr01_contact_ao_a"
    Category = "contact_ao"
    Width = 128
    Height = 128
}
