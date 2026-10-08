# Writes a short-lived Azure DevOps token for local docker compose.
# The token is stored only in the gitignored env file. This script does not print it.
[CmdletBinding()]
param(
    [string]$RepoRoot,
    [string]$EnvFile,
    [string]$AzExecutable
)

$ErrorActionPreference = 'Stop'
$AzureDevOpsResource = '499b84ac-1321-427f-aa17-267ca6975798'

function Get-FeedTokenRepoRoot {
    if ($RepoRoot) {
        return (Resolve-Path -LiteralPath $RepoRoot).Path
    }
    return (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
}

function Update-FeedTokenSessionPath {
    $machinePath = [Environment]::GetEnvironmentVariable('Path', 'Machine')
    $userPath = [Environment]::GetEnvironmentVariable('Path', 'User')
    $env:Path = "$userPath;$machinePath"
    $candidates = @(
        (Join-Path $env:LOCALAPPDATA 'Programs\Azure CLI\wbin'),
        (Join-Path $env:LOCALAPPDATA 'Microsoft\WindowsApps'),
        (Join-Path ${env:ProgramFiles} 'Microsoft SDKs\Azure\CLI2\wbin')
    )
    if (${env:ProgramFiles(x86)}) {
        $candidates += (Join-Path ${env:ProgramFiles(x86)} 'Microsoft SDKs\Azure\CLI2\wbin')
    }
    foreach ($dir in $candidates) {
        if (Test-Path -LiteralPath (Join-Path $dir 'az.cmd')) {
            if ($env:Path -notlike "*${dir}*") {
                $env:Path = "$dir;$env:Path"
            }
        }
    }
}

function Find-AzExecutable {
    if ($AzExecutable) {
        if (-not (Test-Path -LiteralPath $AzExecutable)) {
            Write-Host "Azure CLI was not found at $AzExecutable."
            exit 1
        }
        return (Resolve-Path -LiteralPath $AzExecutable).Path
    }

    $existing = Get-Command az -ErrorAction SilentlyContinue
    if ($existing -and $existing.Source) {
        return $existing.Source
    }

    $winget = Get-Command winget -ErrorAction SilentlyContinue
    if (-not $winget) {
        Write-Host "Azure CLI (az) is not installed and winget is not available, so it cannot be installed for the current user."
        Write-Host "Install Azure CLI yourself (no admin) from https://aka.ms/installazurecliwindows and then run Scripts/docker-compose.feed-token.ps1."
        exit 1
    }

    Write-Host "Azure CLI (az) is not installed. Installing it for the current user with winget."
    # winget writes progress to the success stream. This function's caller
    # captures that stream, so the progress must not become part of the az path.
    & winget install --id Microsoft.AzureCLI --scope user --silent --accept-package-agreements --accept-source-agreements --disable-interactivity *>&1 | Out-Host
    $wingetCode = $LASTEXITCODE
    # 0x8A15002B: the package is already installed.
    if ($wingetCode -ne 0 -and $wingetCode -ne -1978335189) {
        Write-Host "winget could not install Azure CLI (exit $wingetCode). Install it for the current user from https://aka.ms/installazurecliwindows and then run Scripts/docker-compose.feed-token.ps1."
        exit 1
    }

    Update-FeedTokenSessionPath
    $installed = Get-Command az -ErrorAction SilentlyContinue
    if ($installed -and $installed.Source) {
        return $installed.Source
    }

    Write-Host "winget finished, but az is still not on PATH. Open a new shell, or install Azure CLI for the current user from https://aka.ms/installazurecliwindows, then run Scripts/docker-compose.feed-token.ps1."
    exit 1
}

function Set-FeedTokenAcl {
    param([Parameter(Mandatory = $true)][string]$Path)
    $security = New-Object System.Security.AccessControl.FileSecurity
    $security.SetAccessRuleProtection($true, $false)
    $identity = [System.Security.Principal.WindowsIdentity]::GetCurrent()
    $rule = New-Object System.Security.AccessControl.FileSystemAccessRule(
        $identity.User,
        [System.Security.AccessControl.FileSystemRights]::FullControl,
        [System.Security.AccessControl.InheritanceFlags]::None,
        [System.Security.AccessControl.PropagationFlags]::None,
        [System.Security.AccessControl.AccessControlType]::Allow)
    $security.AddAccessRule($rule)
    Set-Acl -LiteralPath $Path -AclObject $security
}

$root = Get-FeedTokenRepoRoot
if (-not $EnvFile) {
    $EnvFile = Join-Path $root '.azure-artifacts.env'
}

$az = Find-AzExecutable

& $az account show --output none
if ($LASTEXITCODE -ne 0) {
    Write-Host "No Azure CLI session. Starting device-code sign-in. Complete it in a browser, or cancel and this script will stop."
    & $az login --use-device-code --allow-no-subscriptions
    if ($LASTEXITCODE -ne 0) {
        Write-Host "Azure sign-in did not complete. Run 'az login' and then Scripts/docker-compose.feed-token.ps1."
        exit 1
    }
}

$raw = & $az account get-access-token --resource $AzureDevOpsResource --output json
if ($LASTEXITCODE -ne 0 -or -not $raw) {
    Write-Host "az account get-access-token failed. Run 'az login' and then Scripts/docker-compose.feed-token.ps1."
    exit 1
}

try {
    $parsed = ($raw | Out-String | ConvertFrom-Json)
} catch {
    Write-Host "az account get-access-token did not return JSON. Run 'az login' and then Scripts/docker-compose.feed-token.ps1."
    exit 1
}

$token = [string]$parsed.accessToken
$expiresRaw = $parsed.expires_on
if (-not $token -or $null -eq $expiresRaw -or "$expiresRaw" -eq '') {
    Write-Host "az account get-access-token did not return a usable token expiry. Run 'az login' and then Scripts/docker-compose.feed-token.ps1."
    exit 1
}

$expires = [int64]0
if (-not [int64]::TryParse("$expiresRaw", [ref]$expires) -or $expires -le 0) {
    Write-Host "az account get-access-token returned an expiry that is not a unix epoch. Run 'az login' and then Scripts/docker-compose.feed-token.ps1."
    exit 1
}

$dir = Split-Path -Parent $EnvFile
if ($dir -and -not (Test-Path -LiteralPath $dir)) {
    New-Item -ItemType Directory -Path $dir -Force | Out-Null
}

$utf8 = New-Object System.Text.UTF8Encoding $false
$body = "AZURE_ARTIFACTS_PAT=$token`nAZURE_ARTIFACTS_PAT_EXPIRES_ON=$expires`n"
try {
    if (-not (Test-Path -LiteralPath $EnvFile)) {
        [System.IO.File]::WriteAllText($EnvFile, '', $utf8)
    }
    Set-FeedTokenAcl -Path $EnvFile
    [System.IO.File]::WriteAllText($EnvFile, $body, $utf8)
} catch {
    $kept = $false
    if (Test-Path -LiteralPath $EnvFile) {
        try {
            Remove-Item -LiteralPath $EnvFile -Force -ErrorAction Stop
        } catch {
            $kept = $true
        }
    }
    if ($kept) {
        Write-Host "The token file could not be limited to the current user, and it could not be removed. Delete $EnvFile and run Scripts/docker-compose.feed-token.ps1 again."
    } else {
        Write-Host "The token file could not be limited to the current user, so it was not kept."
    }
    exit 1
} finally {
    Remove-Variable token, parsed, raw, body, expiresRaw -ErrorAction SilentlyContinue
}

exit 0
