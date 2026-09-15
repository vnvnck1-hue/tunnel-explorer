param(
    [switch]$Apply
)

$ErrorActionPreference = 'Stop'
$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$planPath = Join-Path $repoRoot 'unity/TunnelCrew/AgentScripts/primary-match-bold-import-plan.json'
$deliveryRoot = Join-Path $repoRoot 'art-production/test-room-v01/working/primary-match-v2/delivery-candidates'
$projectRoot = Join-Path $repoRoot 'unity/TunnelCrew'
$allowedTargetRoot = [System.IO.Path]::GetFullPath((Join-Path $projectRoot 'Assets/_Project/Art/Environment/PrimaryMatchBoldCandidates'))

if (-not (Test-Path -LiteralPath $planPath -PathType Leaf)) {
    throw "Import plan is missing: $planPath"
}

$plan = Get-Content -Raw -LiteralPath $planPath | ConvertFrom-Json
if ($plan.status -ne 'dormant_candidate_plan_not_staged') {
    throw "Unexpected plan status: $($plan.status)"
}
if ($plan.summary.assetCount -ne 39 -or $plan.summary.channelFilesToStage -ne 195) {
    throw "Plan count mismatch: assets=$($plan.summary.assetCount), channels=$($plan.summary.channelFilesToStage)"
}

function Assert-Within([string]$Path, [string]$Root, [string]$Label) {
    $fullPath = [System.IO.Path]::GetFullPath($Path)
    $fullRoot = [System.IO.Path]::GetFullPath($Root).TrimEnd('\') + '\'
    if (-not $fullPath.StartsWith($fullRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "$Label escapes the allowed root: $fullPath"
    }
    return $fullPath
}

$operations = [System.Collections.Generic.List[object]]::new()
foreach ($asset in $plan.assets) {
    foreach ($channel in @('albedo', 'normal', 'ao', 'emission', 'mask')) {
        $sourceRel = $asset.sourceChannels.PSObject.Properties[$channel].Value
        $targetRel = $asset.targetAssetPaths.PSObject.Properties[$channel].Value
        $source = Assert-Within (Join-Path $deliveryRoot $sourceRel) $deliveryRoot 'Source'
        $target = Assert-Within (Join-Path $projectRoot $targetRel) $allowedTargetRoot 'Target'
        if (-not (Test-Path -LiteralPath $source -PathType Leaf)) {
            throw "Source channel is missing: $source"
        }
        $expectedHash = $asset.sourceChannelHashes.PSObject.Properties[$channel].Value
        $actualHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $source).Hash
        if ($actualHash -ne $expectedHash) {
            throw "Source hash mismatch: $source"
        }
        $operations.Add([pscustomobject]@{
            assetId = $asset.assetId
            channel = $channel
            source = $source
            target = $target
            sha256 = $actualHash
        })
    }
}

if (-not $Apply) {
    Write-Output "DRY RUN: $($plan.summary.assetCount) assets / $($operations.Count) channel files"
    Write-Output "Target: $allowedTargetRoot"
    Write-Output 'No files copied. Re-run with -Apply only after the TunnelCrew Editor is available for coordinated import.'
    exit 0
}

foreach ($operation in $operations) {
    $directory = Split-Path -Parent $operation.target
    [System.IO.Directory]::CreateDirectory($directory) | Out-Null
    Copy-Item -LiteralPath $operation.source -Destination $operation.target -Force
    $copiedHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $operation.target).Hash
    if ($copiedHash -ne $operation.sha256) {
        throw "Copied file hash mismatch: $($operation.target)"
    }
}

$receiptPath = Join-Path $projectRoot 'AgentScripts/primary-match-bold-stage-receipt.json'
$receipt = [ordered]@{
    status = 'staged_candidate_assets_not_import_verified'
    planId = $plan.planId
    targetRoot = $plan.targetRoot
    assetCount = $plan.summary.assetCount
    channelFiles = $operations.Count
    changesSceneOrPrefab = $false
    changesActiveRuntimeCatalog = $false
}
[System.IO.File]::WriteAllText(
    $receiptPath,
    ($receipt | ConvertTo-Json -Depth 4) + [System.Environment]::NewLine,
    [System.Text.UTF8Encoding]::new($false)
)
Write-Output "STAGED: $($plan.summary.assetCount) assets / $($operations.Count) channel files"
Write-Output "Receipt: $receiptPath"
