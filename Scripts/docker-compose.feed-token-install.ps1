# Adds one idempotent line to the PowerShell profile and installs the docker compose wrapper.
[CmdletBinding()]
param(
    [string]$ProfilePath,
    [string]$InstallDir
)

$ErrorActionPreference = 'Stop'
$marker = 'link-cloud-feed-token'
$profileLine = '. "$env:USERPROFILE\.link-cloud\docker-compose.feed-token-profile.ps1" # link-cloud-feed-token'

if (-not $ProfilePath) {
    $ProfilePath = $PROFILE
}
if (-not $InstallDir) {
    $InstallDir = Join-Path $env:USERPROFILE '.link-cloud'
}

$source = Join-Path $PSScriptRoot 'docker-compose.feed-token-profile.ps1'
if (-not (Test-Path -LiteralPath $source)) {
    Write-Host "Missing $source."
    exit 1
}

if (-not (Test-Path -LiteralPath $InstallDir)) {
    New-Item -ItemType Directory -Path $InstallDir -Force | Out-Null
}
Copy-Item -LiteralPath $source -Destination (Join-Path $InstallDir 'docker-compose.feed-token-profile.ps1') -Force

$profileDir = Split-Path -Parent $ProfilePath
if ($profileDir -and -not (Test-Path -LiteralPath $profileDir)) {
    New-Item -ItemType Directory -Path $profileDir -Force | Out-Null
}
# UTF-8 with a BOM is readable by Windows PowerShell and PowerShell 7.
# AppendAllText would write UTF-8 bytes onto a UTF-16 profile and break the new line.
$utf8Bom = New-Object System.Text.UTF8Encoding $true
if (-not (Test-Path -LiteralPath $ProfilePath)) {
    [System.IO.File]::WriteAllText($ProfilePath, $profileLine + [Environment]::NewLine, $utf8Bom)
    Write-Host "Added the profile line to $ProfilePath."
    Write-Host "Open a new PowerShell window in the repo. docker compose will fetch a short-lived Azure DevOps token when it needs one."
    exit 0
}

$existing = [System.IO.File]::ReadAllText($ProfilePath)
if ($existing -notmatch [regex]::Escape($marker)) {
    $prefix = ''
    if ($existing.Length -gt 0 -and -not $existing.EndsWith("`n")) {
        $prefix = [Environment]::NewLine
    }
    $updated = $existing + $prefix + $profileLine + [Environment]::NewLine
    [System.IO.File]::WriteAllText($ProfilePath, $updated, $utf8Bom)
    Write-Host "Added the profile line to $ProfilePath."
} else {
    Write-Host "The profile line is already in $ProfilePath."
}

Write-Host "Open a new PowerShell window in the repo. docker compose will fetch a short-lived Azure DevOps token when it needs one."
exit 0
