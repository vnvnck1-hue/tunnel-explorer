param([string]$PackageRoot = "art-production/test-room-v01")

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing

$channels = @("albedo", "normal", "emission", "mask", "ao")
$assets = @(
    @{ Id = "TR01-DEC-BARREL-A"; Category = "decoration"; Name = "tr01_dec_barrel_a" },
    @{ Id = "TR01-DEC-HARDWARE-A"; Category = "decoration"; Name = "tr01_dec_hardware_a" },
    @{ Id = "TR01-DEC-BUCKET-A"; Category = "decoration"; Name = "tr01_dec_bucket_a" },
    @{ Id = "TR01-DEC-CABLE-SPOOL-A"; Category = "decoration"; Name = "tr01_dec_cable_spool_a" },
    @{ Id = "TR01-DEC-PAPERS-A"; Category = "decoration"; Name = "tr01_dec_papers_a" },
    @{ Id = "TR01-DEC-TOOLBOX-A"; Category = "decoration"; Name = "tr01_dec_toolbox_a" },
    @{ Id = "TR01-LIN-RAIL-BROKEN-A"; Category = "linear"; Name = "tr01_rail_broken_a" },
    @{ Id = "TR01-LIN-PIPE-ELBOW-A"; Category = "linear"; Name = "tr01_pipe_elbow_a" },
    @{ Id = "TR01-LIN-CABLE-JUNCTION-A"; Category = "linear"; Name = "tr01_cable_junction_a" }
)

function Convert-Alpha([byte]$alpha) {
    # The rejected background occupies alpha 1..159. Preserve only the narrow
    # authored contour band and remap it into a controlled antialias ramp.
    if ($alpha -le 159) { return [byte]0 }
    if ($alpha -ge 240) { return [byte]255 }
    return [byte][Math]::Round((($alpha - 159) / 81.0) * 255.0)
}

foreach ($asset in $assets) {
    $albedoPath = Join-Path $PackageRoot "approved/albedo/$($asset.Category)/$($asset.Name)_albedo.png"
    $albedo = [System.Drawing.Bitmap]::new((Resolve-Path $albedoPath).Path)
    if ($albedo.Width -ne 256 -or $albedo.Height -ne 256) {
        $albedo.Dispose()
        throw "$($asset.Id): expected 256x256 albedo"
    }

    $alphaMask = New-Object 'byte[,]' 256, 256
    $zero = 0
    $opaque = 0
    $partial = 0
    for ($y = 0; $y -lt 256; $y++) {
        for ($x = 0; $x -lt 256; $x++) {
            $a = Convert-Alpha $albedo.GetPixel($x, $y).A
            $alphaMask[$x, $y] = $a
            if ($a -eq 0) { $zero++ }
            elseif ($a -eq 255) { $opaque++ }
            else { $partial++ }
        }
    }
    $albedo.Dispose()

    foreach ($channel in $channels) {
        $path = Join-Path $PackageRoot "approved/$channel/$($asset.Category)/$($asset.Name)_$channel.png"
        $bitmap = [System.Drawing.Bitmap]::new((Resolve-Path $path).Path)
        if ($bitmap.Width -ne 256 -or $bitmap.Height -ne 256) {
            $bitmap.Dispose()
            throw "$($asset.Id)/${channel}: expected 256x256"
        }
        for ($y = 0; $y -lt 256; $y++) {
            for ($x = 0; $x -lt 256; $x++) {
                $p = $bitmap.GetPixel($x, $y)
                $bitmap.SetPixel($x, $y, [System.Drawing.Color]::FromArgb($alphaMask[$x, $y], $p.R, $p.G, $p.B))
            }
        }
        $temporaryPath = "$path.r17.tmp.png"
        $bitmap.Save($temporaryPath, [System.Drawing.Imaging.ImageFormat]::Png)
        $bitmap.Dispose()
        Move-Item -LiteralPath $temporaryPath -Destination $path -Force
    }

    $total = 256.0 * 256.0
    [PSCustomObject]@{
        AssetId = $asset.Id
        TransparentPercent = [Math]::Round(100.0 * $zero / $total, 3)
        OpaquePercent = [Math]::Round(100.0 * $opaque / $total, 3)
        PartialPercent = [Math]::Round(100.0 * $partial / $total, 3)
        Result = if (($partial / $total) -le 0.03) { "PASS" } else { "FAIL" }
    }
}
