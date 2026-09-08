param(
    [string]$PackageRoot = "art-production/test-room-v01"
)

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing

$manifestPath = Join-Path $PackageRoot "metadata/manifest.json"
if (-not (Test-Path $manifestPath)) { throw "Manifest not found: $manifestPath" }
$manifest = Get-Content -Raw $manifestPath | ConvertFrom-Json
$errors = [System.Collections.Generic.List[string]]::new()
$approvedCount = 0
$fileCount = 0
$defaultChannels = @('albedo', 'normal', 'emission', 'mask', 'ao')

foreach ($asset in $manifest.assets) {
    if ($asset.status -ne 'approved') { continue }
    $approvedCount++
    $emissionMode = if ($asset.PSObject.Properties.Name -contains 'emissionMode') { $asset.emissionMode } else { 'none' }
    $requiresAlphaAudit = ($asset.PSObject.Properties.Name -contains 'alphaInspection') -and
        ($asset.alphaInspection.PSObject.Properties.Name -contains 'partialFraction')
    $alphaSignatures = [System.Collections.Generic.List[string]]::new()
    $measuredPartialFraction = $null

    $required = if ($asset.requiredChannels) { @($asset.requiredChannels) } else { $defaultChannels }
    foreach ($channel in $required) {
        $relative = $asset.channels.$channel
        if (-not $relative) {
            $errors.Add("$($asset.assetId): missing channel entry '$channel'")
            continue
        }
        if ($relative -notmatch '^approved/') {
            $errors.Add("$($asset.assetId): approved asset points outside approved/: $relative")
            continue
        }
        if ([IO.Path]::GetFileName($relative) -notmatch '^tr01_[a-z0-9_]+_(albedo|normal|emission|mask|ao)\.png$') {
            $errors.Add("$($asset.assetId): invalid file name: $relative")
        }

        $path = Join-Path $PackageRoot $relative
        if (-not (Test-Path $path)) {
            $errors.Add("$($asset.assetId): file not found: $relative")
            continue
        }
        $fileCount++
        $bmp = [System.Drawing.Bitmap]::new((Resolve-Path $path).Path)
        if ($bmp.Width -ne $asset.dimensionsPixels[0] -or $bmp.Height -ne $asset.dimensionsPixels[1]) {
            $errors.Add("$($asset.assetId): $channel size $($bmp.Width)x$($bmp.Height), expected $($asset.dimensionsPixels[0])x$($asset.dimensionsPixels[1])")
        }

        if ($requiresAlphaAudit) {
            $alphaBytes = New-Object byte[] ($bmp.Width * $bmp.Height)
            $partialPixels = 0
            $index = 0
            for ($alphaY = 0; $alphaY -lt $bmp.Height; $alphaY++) {
                for ($alphaX = 0; $alphaX -lt $bmp.Width; $alphaX++) {
                    $alpha = $bmp.GetPixel($alphaX, $alphaY).A
                    $alphaBytes[$index++] = $alpha
                    if ($alpha -gt 0 -and $alpha -lt 255) { $partialPixels++ }
                }
            }
            $alphaSignatures.Add([Convert]::ToBase64String($alphaBytes))
            if ($channel -eq 'albedo') { $measuredPartialFraction = $partialPixels / [double]($bmp.Width * $bmp.Height) }
        }

        if ($channel -eq 'emission') {
            $nonBlack = $false
            for ($y = 0; $y -lt $bmp.Height -and -not $nonBlack; $y += 8) {
                for ($x = 0; $x -lt $bmp.Width; $x += 8) {
                    $p = $bmp.GetPixel($x, $y)
                    if ($p.A -gt 0 -and ($p.R -ne 0 -or $p.G -ne 0 -or $p.B -ne 0)) { $nonBlack = $true; break }
                }
            }
            if ($emissionMode -eq 'none' -and $nonBlack) { $errors.Add("$($asset.assetId): non-emissive asset has non-black emission") }
            if ($emissionMode -ne 'none' -and -not $nonBlack) { $errors.Add("$($asset.assetId): emissive asset has an empty emission channel") }
        }
        $bmp.Dispose()
    }

    if ($requiresAlphaAudit) {
        if (($alphaSignatures | Select-Object -Unique).Count -ne 1) {
            $errors.Add("$($asset.assetId): channel alpha masks are not identical")
        }
        if ($measuredPartialFraction -gt 0.03) {
            $errors.Add("$($asset.assetId): partial alpha fraction $measuredPartialFraction exceeds 0.03")
        }
        if ([Math]::Abs($measuredPartialFraction - [double]$asset.alphaInspection.partialFraction) -gt 0.000001) {
            $errors.Add("$($asset.assetId): measured partial alpha $measuredPartialFraction disagrees with manifest $($asset.alphaInspection.partialFraction)")
        }
    }

    $pivotX = $asset.pivotNormalized[0] * $asset.dimensionsPixels[0]
    $pivotY = $asset.pivotNormalized[1] * $asset.dimensionsPixels[1]
    if ($pivotX -ne [Math]::Round($pivotX) -or $pivotY -ne [Math]::Round($pivotY)) {
        $errors.Add("$($asset.assetId): pivot is not on integer pixels")
    }
    if (-not $asset.pivotPixels -or $asset.pivotPixels.Count -ne 2) {
        $errors.Add("$($asset.assetId): missing pivotPixels [x,y] in image-top-left coordinates")
    }
    else {
        $explicitX = [double]$asset.pivotPixels[0]
        $explicitY = [double]$asset.pivotPixels[1]
        if ($explicitX -ne [Math]::Round($explicitX) -or $explicitY -ne [Math]::Round($explicitY)) {
            $errors.Add("$($asset.assetId): pivotPixels must use integer coordinates")
        }
        $expectedX = $pivotX
        $expectedY = $asset.dimensionsPixels[1] - $pivotY
        if ($explicitX -ne $expectedX -or $explicitY -ne $expectedY) {
            $errors.Add("$($asset.assetId): pivotPixels [$explicitX,$explicitY] disagrees with pivotNormalized; expected [$expectedX,$expectedY]")
        }
    }
}

$contactPath = Join-Path $PackageRoot "approved/albedo/contact_ao/tr01_contact_ao_a_albedo.png"
if (Test-Path $contactPath) {
    $contact = [System.Drawing.Bitmap]::new((Resolve-Path $contactPath).Path)
    if ($contact.GetPixel(64, 0).A -ne 150 -or $contact.GetPixel(64, 127).A -ne 0) {
        $errors.Add("TR01-AO-CONTACT-A: alpha endpoints changed")
    }
    $contact.Dispose()
}

if ($errors.Count -gt 0) {
    $errors | ForEach-Object { Write-Error $_ }
    throw "Art package validation failed with $($errors.Count) error(s)."
}

[PSCustomObject]@{
    ManifestRevision = $manifest.revision
    ApprovedAssets = $approvedCount
    ValidatedFiles = $fileCount
    DeliveryPixelsPerCell = $manifest.deliveryPixelsPerCell
    Result = "PASS"
}
