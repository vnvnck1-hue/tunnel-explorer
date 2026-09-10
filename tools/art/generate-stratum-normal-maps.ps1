param(
    [string]$ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$artRoot = Join-Path $ProjectRoot 'art-production/test-room-v01'
$approvedAlbedo = Join-Path $artRoot 'approved/albedo'
$approvedNormal = Join-Path $artRoot 'approved/normal'
$unityRoot = Join-Path $ProjectRoot 'unity/TunnelCrew/Assets/Art/Visual/ReferenceCalibrationV1'

foreach ($dir in @($approvedNormal, $unityRoot)) {
    New-Item -ItemType Directory -Force -Path $dir | Out-Null
}

$kinds = @(
    @{ Token = 'floor'; Prefixes = @('tr01_reference_floor_', 'tr01_stratum2_floor_', 'tr01_stratum3_floor_', 'tr01_abyss_floor_'); Strength = 2.2; WrapY = $true; BiasX = 0.0; BiasY = 0.0 },
    @{ Token = 'wall_top'; Prefixes = @('tr01_reference_wall_top_', 'tr01_stratum2_wall_top_', 'tr01_stratum3_wall_top_', 'tr01_abyss_wall_top_'); Strength = 3.0; WrapY = $true; BiasX = -0.06; BiasY = -0.04 },
    @{ Token = 'wall_top_rim'; Prefixes = @('tr01_reference_wall_top_rim_', 'tr01_stratum2_wall_top_rim_', 'tr01_stratum3_wall_top_rim_', 'tr01_abyss_wall_top_rim_'); Strength = 4.2; WrapY = $false; BiasX = -0.14; BiasY = -0.10 },
    @{ Token = 'wall_front'; Prefixes = @('tr01_reference_wall_front_', 'tr01_stratum2_wall_front_', 'tr01_stratum3_wall_front_', 'tr01_abyss_wall_front_'); Strength = 3.4; WrapY = $false; BiasX = -0.04; BiasY = -0.02 }
)

function Get-Pixel([System.Drawing.Bitmap]$Bitmap, [int]$X, [int]$Y, [bool]$WrapY) {
    $xx = (($X % $Bitmap.Width) + $Bitmap.Width) % $Bitmap.Width
    if ($WrapY) {
        $yy = (($Y % $Bitmap.Height) + $Bitmap.Height) % $Bitmap.Height
    } else {
        $yy = [Math]::Max(0, [Math]::Min($Bitmap.Height - 1, $Y))
    }
    return $Bitmap.GetPixel($xx, $yy)
}

function Get-Luma([System.Drawing.Color]$Color) {
    return (0.2126 * $Color.R + 0.7152 * $Color.G + 0.0722 * $Color.B) / 255.0
}

function Write-NormalMap([string]$InputPath, [string]$OutputPath, [double]$Strength, [bool]$WrapY, [double]$BiasX, [double]$BiasY) {
    $src = [System.Drawing.Bitmap]::new($InputPath)
    $dst = [System.Drawing.Bitmap]::new($src.Width, $src.Height, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    try {
        for ($y = 0; $y -lt $src.Height; $y++) {
            for ($x = 0; $x -lt $src.Width; $x++) {
                $left = Get-Luma (Get-Pixel $src ($x - 1) $y $WrapY)
                $right = Get-Luma (Get-Pixel $src ($x + 1) $y $WrapY)
                $up = Get-Luma (Get-Pixel $src $x ($y - 1) $WrapY)
                $down = Get-Luma (Get-Pixel $src $x ($y + 1) $WrapY)

                # Project-approved OpenGL +Y convention: derive only from the
                # approved albedo relief, then apply a restrained material bias.
                $nx = -(($right - $left) * $Strength) + $BiasX
                $ny = (($down - $up) * $Strength) + $BiasY
                $nz = 1.0
                $length = [Math]::Sqrt($nx * $nx + $ny * $ny + $nz * $nz)
                $r = [int][Math]::Round((($nx / $length) * 0.5 + 0.5) * 255.0)
                $g = [int][Math]::Round((($ny / $length) * 0.5 + 0.5) * 255.0)
                $b = [int][Math]::Round((($nz / $length) * 0.5 + 0.5) * 255.0)
                $a = (Get-Pixel $src $x $y $WrapY).A
                $dst.SetPixel($x, $y, [System.Drawing.Color]::FromArgb($a, $r, $g, $b))
            }
        }
        $dst.Save($OutputPath, [System.Drawing.Imaging.ImageFormat]::Png)
    } finally {
        $dst.Dispose()
        $src.Dispose()
    }
}

$written = 0
foreach ($kind in $kinds) {
    foreach ($prefix in $kind.Prefixes) {
        foreach ($variant in @('a', 'b', 'c')) {
            $inputName = "$prefix$variant`_albedo.png"
            $inputPath = Join-Path $approvedAlbedo $inputName
            if (-not (Test-Path -LiteralPath $inputPath)) {
                if ($prefix -eq 'tr01_reference_floor_') {
                    $fallback = Join-Path $artRoot "working/reference_calibration_v1_direct/$inputName"
                    if (Test-Path -LiteralPath $fallback) {
                        New-Item -ItemType Directory -Force -Path $approvedAlbedo | Out-Null
                        Copy-Item -LiteralPath $fallback -Destination $inputPath
                    }
                }
            }
            if (-not (Test-Path -LiteralPath $inputPath)) { throw "Missing approved albedo: $inputName" }
            $normalName = "$prefix$variant`_normal.png"
            $approvedPath = Join-Path $approvedNormal $normalName
            $unityPath = Join-Path $unityRoot $normalName
            Write-NormalMap $inputPath $approvedPath $kind.Strength $kind.WrapY $kind.BiasX $kind.BiasY
            Copy-Item -LiteralPath $approvedPath -Destination $unityPath -Force
            $written++
        }
    }
}

Write-Output "Generated $written deterministic albedo-derived normal maps."
