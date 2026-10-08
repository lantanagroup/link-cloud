# Adds one idempotent line to the PowerShell profile and installs the docker compose wrapper.
[CmdletBinding()]
param(
    [string]$ProfilePath,
    [string]$InstallDir
)

$ErrorActionPreference = 'Stop'
$marker = 'link-cloud-feed-token'
$defaultDir = Join-Path $env:USERPROFILE '.link-cloud'

function Get-ProfileText {
    param([string]$Path)
    $bytes = [System.IO.File]::ReadAllBytes($Path)
    if ($bytes.Length -eq 0) { return '' }
    $encoding = $null
    if ($bytes.Length -ge 2 -and $bytes[0] -eq 0xFF -and $bytes[1] -eq 0xFE) {
        $encoding = New-Object System.Text.UnicodeEncoding $false, $true
    } elseif ($bytes.Length -ge 2 -and $bytes[0] -eq 0xFE -and $bytes[1] -eq 0xFF) {
        $encoding = New-Object System.Text.UnicodeEncoding $true, $true
    } else {
        $utf8 = New-Object System.Text.UTF8Encoding $false, $true
        try {
            $text = $utf8.GetString($bytes)
            if ($text.Length -gt 0 -and [int][char]$text[0] -eq 0xFEFF) { return $text.Substring(1) }
            return $text
        } catch {
            $encoding = [System.Text.Encoding]::Default
        }
    }
    $text = $encoding.GetString($bytes)
    if ($text.Length -gt 0 -and [int][char]$text[0] -eq 0xFEFF) { return $text.Substring(1) }
    return $text
}

function Write-ProfilePolicyWarning {
    # Ignore Process. The installer is often started with -ExecutionPolicy Bypass.
    $effective = 'Undefined'
    foreach ($entry in (Get-ExecutionPolicy -List)) {
        $scope = $entry.Scope.ToString()
        if ($scope -eq 'Process') { continue }
        $value = $entry.ExecutionPolicy.ToString()
        if ($value -eq 'Undefined') { continue }
        $effective = $value
        break
    }
    if ($effective -eq 'Restricted' -or $effective -eq 'AllSigned') {
        Write-Host "PowerShell execution policy is $effective, so a new shell will not load this profile. Run Get-ExecutionPolicy -List. If group policy has not locked it, run Set-ExecutionPolicy -Scope CurrentUser RemoteSigned."
    }
}

if (-not $ProfilePath) {
    $ProfilePath = $PROFILE
}
if (-not $InstallDir) {
    $InstallDir = $defaultDir
}

$installFull = [System.IO.Path]::GetFullPath($InstallDir)
$defaultFull = [System.IO.Path]::GetFullPath($defaultDir)
if ([string]::Equals($installFull.TrimEnd('\'), $defaultFull.TrimEnd('\'), [System.StringComparison]::OrdinalIgnoreCase)) {
    $profileLine = '. "$env:USERPROFILE\.link-cloud\docker-compose.feed-token-profile.ps1" # link-cloud-feed-token'
} else {
    $scriptPath = Join-Path $installFull 'docker-compose.feed-token-profile.ps1'
    $escaped = $scriptPath.Replace("'", "''")
    $profileLine = ". '$escaped' # link-cloud-feed-token"
}

$source = Join-Path $PSScriptRoot 'docker-compose.feed-token-profile.ps1'
if (-not (Test-Path -LiteralPath $source)) {
    Write-Host "Missing $source."
    exit 1
}
Write-ProfilePolicyWarning

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

# A BOM selects UTF-16 or UTF-8. BOM-less bytes that are valid UTF-8 stay UTF-8.
# Anything else is this process's ANSI code page. Windows PowerShell Get-Content
# would read a BOM-less UTF-8 profile as ANSI and rewrite the characters wrong.
$existing = Get-ProfileText -Path $ProfilePath
if ($null -eq $existing) { $existing = '' }
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
