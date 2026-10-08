# Writes a short-lived Azure DevOps token for local docker compose.
# The token is stored only in the gitignored env file. This script does not print it.
[CmdletBinding()]
param(
    [string]$RepoRoot,
    [string]$EnvFile,
    [string]$AzExecutable,
    [string]$InstallDirectory,
    [string]$ZipPackage,
    [string]$WingetExecutable,
    [switch]$SkipUserPathUpdate
)

$ErrorActionPreference = 'Stop'
$AzureDevOpsResource = '499b84ac-1321-427f-aa17-267ca6975798'
$AzureCliZipUri = 'https://aka.ms/installazurecliwindowszipx64'

function Get-FeedTokenRepoRoot {
    if ($RepoRoot) {
        return (Resolve-Path -LiteralPath $RepoRoot).Path
    }
    return (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
}

function Update-FeedTokenSessionPath {
    if ($SkipUserPathUpdate) { return }
    $machinePath = [Environment]::GetEnvironmentVariable('Path', 'Machine')
    $userPath = [Environment]::GetEnvironmentVariable('Path', 'User')
    $env:Path = "$userPath;$machinePath"
    $candidates = @(
        (Join-Path $env:LOCALAPPDATA 'AzureCLI\bin'),
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

    $installedFromZip = Install-FeedTokenAzFromZip
    if ($installedFromZip) { return $installedFromZip }

    return Install-FeedTokenAzWithWinget
}

function Add-FeedTokenUserPath {
    param([string]$Directory)
    $parts = @($env:PATH -split ';' | Where-Object { $_ })
    if ($parts -notcontains $Directory) {
        $env:PATH = "$Directory;$env:PATH"
    }
    if ($SkipUserPathUpdate) { return }
    $userPath = [Environment]::GetEnvironmentVariable('Path', 'User')
    $userParts = @()
    if ($userPath) { $userParts = @($userPath -split ';' | Where-Object { $_ }) }
    if ($userParts -notcontains $Directory) {
        if ($userPath) {
            [Environment]::SetEnvironmentVariable('Path', "$Directory;$userPath", 'User')
        } else {
            [Environment]::SetEnvironmentVariable('Path', $Directory, 'User')
        }
    }
}

function Install-FeedTokenAzFromZip {
    if (-not [Environment]::Is64BitOperatingSystem) {
        Write-Host "The per-user Azure CLI ZIP is 64-bit only, so it was not installed."
        return $null
    }
    $root = $InstallDirectory
    if (-not $root) { $root = Join-Path $env:LOCALAPPDATA 'AzureCLI' }
    $package = $ZipPackage
    $downloaded = $null
    try {
        if (-not $package) {
            $downloaded = Join-Path ([System.IO.Path]::GetTempPath()) ("azure-cli-" + [guid]::NewGuid().ToString('n') + ".zip")
            Write-Host "Azure CLI (az) is not installed. Downloading the per-user ZIP into $root."
            $previousProtocol = [Net.ServicePointManager]::SecurityProtocol
            [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
            $previousProgress = $ProgressPreference
            $ProgressPreference = 'SilentlyContinue'
            try {
                Invoke-WebRequest -Uri $AzureCliZipUri -OutFile $downloaded -UseBasicParsing
            } finally {
                $ProgressPreference = $previousProgress
                [Net.ServicePointManager]::SecurityProtocol = $previousProtocol
            }
            $package = $downloaded
        } elseif (-not (Test-Path -LiteralPath $package)) {
            Write-Host "The per-user Azure CLI ZIP was not found at $package."
            return $null
        } else {
            Write-Host "Azure CLI (az) is not installed. Installing the per-user ZIP into $root."
        }
        if (Test-Path -LiteralPath $root) {
            Remove-Item -LiteralPath $root -Recurse -Force
        }
        New-Item -ItemType Directory -Path $root -Force | Out-Null
        Expand-Archive -LiteralPath $package -DestinationPath $root -Force
        $azCmd = Get-ChildItem -LiteralPath $root -Filter az.cmd -Recurse -File | Select-Object -First 1
        if (-not $azCmd) {
            Write-Host "The per-user Azure CLI ZIP did not contain bin\az.cmd."
            return $null
        }
        Add-FeedTokenUserPath -Directory $azCmd.Directory.FullName
        return $azCmd.FullName
    } catch {
        Write-Host "The per-user Azure CLI ZIP could not be installed. $($_.Exception.Message)"
        return $null
    } finally {
        if ($downloaded -and (Test-Path -LiteralPath $downloaded)) {
            Remove-Item -LiteralPath $downloaded -Force -ErrorAction SilentlyContinue
        }
    }
}

function Install-FeedTokenAzWithWinget {
    $winget = $WingetExecutable
    if (-not $winget) {
        $found = Get-Command winget -ErrorAction SilentlyContinue
        if ($found) { $winget = $found.Source }
    }
    if (-not $winget) {
        Write-Host "The per-user Azure CLI ZIP is not available and winget is not available."
        Write-Host "Install Azure CLI from https://aka.ms/installazurecliwindows and then run Scripts/docker-compose.feed-token.ps1."
        exit 1
    }

    Write-Host "The per-user ZIP install did not produce az. Trying winget, which installs the machine-wide MSI and needs an administrator."
    # winget writes progress to the success stream. This function's caller
    # captures that stream, so the progress must not become part of the az path.
    & $winget install --exact --id Microsoft.AzureCLI --silent --accept-package-agreements --accept-source-agreements --disable-interactivity *>&1 | Out-Host
    $wingetCode = $LASTEXITCODE
    # 0x8A15002B: the package is already installed.
    if ($wingetCode -ne 0 -and $wingetCode -ne -1978335189) {
        Write-Host "winget could not install Azure CLI (exit $wingetCode). That installer needs an administrator. Install Azure CLI from https://aka.ms/installazurecliwindows and then run Scripts/docker-compose.feed-token.ps1."
        exit 1
    }

    Update-FeedTokenSessionPath
    $installed = Get-Command az -ErrorAction SilentlyContinue
    if ($installed -and $installed.Source) {
        return $installed.Source
    }

    Write-Host "winget finished, but az is still not on PATH. Open a new shell, or install Azure CLI from https://aka.ms/installazurecliwindows, then run Scripts/docker-compose.feed-token.ps1."
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
