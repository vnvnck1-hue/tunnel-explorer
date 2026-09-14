$ErrorActionPreference = 'Stop'

function Deny-UnityToolCall {
    param([Parameter(Mandatory = $true)][string]$Reason)

    [pscustomobject]@{
        hookSpecificOutput = [pscustomobject]@{
            hookEventName          = 'PreToolUse'
            permissionDecision     = 'deny'
            permissionDecisionReason = $Reason
        }
    } | ConvertTo-Json -Depth 5 -Compress
    exit 0
}

try {
    # Hook payloads are UTF-8, including Korean workspace paths. Decode before JSON parsing.
    [Console]::InputEncoding = New-Object System.Text.UTF8Encoding($false)
    $rawInput = [Console]::In.ReadToEnd()
    if ([string]::IsNullOrWhiteSpace($rawInput)) {
        Deny-UnityToolCall 'Unity MCP guard received no hook input.'
    }

    $hookInput = $rawInput | ConvertFrom-Json
    $toolName = [string]$hookInput.tool_name

    if ($toolName -notmatch '^mcp__unityMCP__') {
        exit 0
    }

    if ($toolName -eq 'mcp__unityMCP__set_active_instance') {
        Deny-UnityToolCall 'Do not use the shared active Unity instance. Pass unity_instance on every Unity MCP call.'
    }

    $statusDirectory = Join-Path $env:USERPROFILE '.unity-mcp'
    $tunnelStatus = Get-ChildItem -LiteralPath $statusDirectory -Filter 'unity-mcp-status-*.json' -File -ErrorAction SilentlyContinue |
        Sort-Object LastWriteTime -Descending |
        ForEach-Object {
            try {
                $status = Get-Content -Raw -LiteralPath $_.FullName | ConvertFrom-Json
                $projectPath = ([string]$status.project_path).Replace('\', '/').TrimEnd('/')
                if ($projectPath -match '(?i)^C:/Users/Loadcomplete/TunnelCrew(?:/Assets)?$') {
                    [pscustomobject]@{
                        File = $_
                        Hash = $_.BaseName -replace '^unity-mcp-status-', ''
                    }
                }
            }
            catch {
                # Ignore malformed or partially written status files and continue searching.
            }
        } |
        Select-Object -First 1

    if ($null -eq $tunnelStatus -or [string]::IsNullOrWhiteSpace($tunnelStatus.Hash)) {
        Deny-UnityToolCall 'TunnelCrew Unity MCP instance could not be resolved from the junction project path.'
    }

    $expectedInstance = "TunnelCrew@$($tunnelStatus.Hash)"
    $requestedInstance = [string]$hookInput.tool_input.unity_instance

    if ([string]::IsNullOrWhiteSpace($requestedInstance)) {
        Deny-UnityToolCall "Missing unity_instance. Retry with unity_instance '$expectedInstance'."
    }

    if ($requestedInstance -ne $expectedInstance) {
        Deny-UnityToolCall "Wrong Unity instance '$requestedInstance'. TunnelCrew requires '$expectedInstance'; SlimeForge must not be modified."
    }

    exit 0
}
catch {
    Deny-UnityToolCall "Unity MCP guard failed closed: $($_.Exception.Message)"
}
