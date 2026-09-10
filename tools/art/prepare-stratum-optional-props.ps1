param(
    [string]$ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path,
    [string]$GeneratedRoot = 'C:/Users/vnvnc/.codex/generated_images/01a08645-4b6e-7140-b5f6-d115649ebb12'
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$artRoot = Join-Path $ProjectRoot 'art-production/test-room-v01'
$sourceRoot = Join-Path $artRoot 'source/stratum_kits_v1'
$workingRoot = Join-Path $artRoot 'working/stratum_kits_v1'
$approvedAlbedo = Join-Path $artRoot 'approved/albedo'
$approvedEmission = Join-Path $artRoot 'approved/emission'
$unityRoot = Join-Path $ProjectRoot 'unity/TunnelCrew/Assets/Art/Visual/ReferenceCalibrationV1'
foreach ($dir in @($sourceRoot, $workingRoot, $approvedAlbedo, $approvedEmission, $unityRoot)) {
    New-Item -ItemType Directory -Force -Path $dir | Out-Null
}

$jobs = @(
    @{ Source = 'exec-a99c74d0-39f9-4ff7-8232-9e8f14114d89.png'; Name = 'tr01_reference_ore_front_a'; Width = 128; Height = 128; Emission = 'ore' },
    @{ Source = 'exec-fba027fa-4842-4ba6-9c6d-c61c54e57173.png'; Name = 'tr01_reference_ore_front_b'; Width = 128; Height = 128; Emission = 'ore' },
    @{ Source = 'exec-1fe32a4a-7f3a-42c9-9e8a-9eac9de35ffe.png'; Name = 'tr01_reference_torch_a'; Width = 96; Height = 128; Emission = 'torch' },
    @{ Source = 'exec-dec24edb-6497-4e76-9beb-dc1b4f3423dd.png'; Name = 'tr01_reference_floor_edge_a'; Width = 128; Height = 128; Emission = $null }
)

function Resize-Png([string]$InputPath, [string]$OutputPath, [int]$Width, [int]$Height) {
    $src = [System.Drawing.Image]::FromFile($InputPath)
    $dst = [System.Drawing.Bitmap]::new($Width, $Height, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    try {
        $g = [System.Drawing.Graphics]::FromImage($dst)
        try {
            $g.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceCopy
            $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
            $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::Half
            $g.DrawImage($src, 0, 0, $Width, $Height)
        } finally { $g.Dispose() }
        $dst.Save($OutputPath, [System.Drawing.Imaging.ImageFormat]::Png)
    } finally { $dst.Dispose(); $src.Dispose() }
}

function Write-Emission([string]$AlbedoPath, [string]$EmissionPath, [string]$Mode) {
    $src = [System.Drawing.Bitmap]::new($AlbedoPath)
    $dst = [System.Drawing.Bitmap]::new($src.Width, $src.Height, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    try {
        for ($y = 0; $y -lt $src.Height; $y++) {
            for ($x = 0; $x -lt $src.Width; $x++) {
                $c = $src.GetPixel($x, $y)
                $emit = $false
                if ($Mode -eq 'ore') {
                    $emit = (($c.R -gt 95 -and $c.B -gt 85 -and $c.R -gt ($c.G * 1.18) -and $c.B -gt ($c.G * 1.08)) -or
                             ($c.G -gt 100 -and $c.B -gt 115 -and $c.G -gt ($c.R * 1.12) -and $c.B -gt ($c.R * 1.12)))
                    if ($c.R -gt $c.G * 1.18 -and $c.B -gt $c.G * 1.08) { $color = [System.Drawing.Color]::FromArgb(245, 255, 35, 220) }
                    else { $color = [System.Drawing.Color]::FromArgb(235, 35, 220, 255) }
                } else {
                    $emit = ($c.R -gt 135 -and $c.G -gt 55 -and $c.R -gt ($c.G * 1.15) -and $c.G -gt ($c.B * 1.30))
                    $color = [System.Drawing.Color]::FromArgb(245, 255, 115, 20)
                }
                if ($emit -and $c.A -gt 0) { $dst.SetPixel($x, $y, [System.Drawing.Color]::FromArgb([Math]::Min($c.A, $color.A), $color.R, $color.G, $color.B)) }
                else { $dst.SetPixel($x, $y, [System.Drawing.Color]::FromArgb(0, 0, 0, 0)) }
            }
        }
        $dst.Save($EmissionPath, [System.Drawing.Imaging.ImageFormat]::Png)
    } finally { $dst.Dispose(); $src.Dispose() }
}

foreach ($job in $jobs) {
    $sourcePath = Join-Path $GeneratedRoot $job.Source
    if (-not (Test-Path -LiteralPath $sourcePath)) { throw "Missing generated source: $sourcePath" }
    $sourceOut = Join-Path $sourceRoot "$($job.Name)_source.png"
    Copy-Item -LiteralPath $sourcePath -Destination $sourceOut -Force

    $albedoName = "$($job.Name)_albedo.png"
    $workingPath = Join-Path $workingRoot $albedoName
    $approvedPath = Join-Path $approvedAlbedo $albedoName
    $unityPath = Join-Path $unityRoot $albedoName
    Resize-Png $sourcePath $workingPath $job.Width $job.Height
    Copy-Item -LiteralPath $workingPath -Destination $approvedPath -Force
    Copy-Item -LiteralPath $workingPath -Destination $unityPath -Force

    if ($job.Emission) {
        $emissionName = "$($job.Name)_emission.png"
        $emissionPath = Join-Path $approvedEmission $emissionName
        $unityEmissionPath = Join-Path $unityRoot $emissionName
        Write-Emission $approvedPath $emissionPath $job.Emission
        Copy-Item -LiteralPath $emissionPath -Destination $unityEmissionPath -Force
    }
}

Write-Output "Prepared $($jobs.Count) optional albedo props and emission maps for ore/torch."
