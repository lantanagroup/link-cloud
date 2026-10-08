# Shell functions for local docker compose feed tokens.
# `docker compose` inside this repo refreshes .azure-artifacts.env when the
# token is missing or has less than 10 minutes left, then runs the real
# docker executable with AZURE_ARTIFACTS_PAT set only for that process.

function Get-LinkCloudFeedTokenMissingMessage {
    $lines = @(
        'Azure token missing. Run this one-time setup: powershell -NoProfile -ExecutionPolicy Bypass -File ./Scripts/docker-compose.feed-token-install.ps1'
        'PowerShell profile line: . "$env:USERPROFILE\.link-cloud\docker-compose.feed-token-profile.ps1" # link-cloud-feed-token'
        'Git Bash: bash ./Scripts/docker-compose.feed-token-install.sh'
        'Git Bash profile line: . "$HOME/.link-cloud/docker-compose.feed-token.sh" # link-cloud-feed-token'
    )
    return ($lines -join [Environment]::NewLine)
}

function Write-LinkCloudMessage {
    param([string]$Message)
    $hooks = $global:LinkCloudFeedTokenHooks
    if ($hooks -and $hooks.ContainsKey('Messages') -and $null -ne $hooks['Messages']) {
        [void]$hooks['Messages'].Add($Message)
    }
    Write-Host $Message
}

function Get-LinkCloudNowEpoch {
    $hooks = $global:LinkCloudFeedTokenHooks
    if ($hooks -and $hooks.ContainsKey('NowEpoch')) {
        return [int64]$hooks['NowEpoch']
    }
    return [DateTimeOffset]::UtcNow.ToUnixTimeSeconds()
}

function Find-LinkCloudRepoRoot {
    $hooks = $global:LinkCloudFeedTokenHooks
    if ($hooks -and $hooks.ContainsKey('RepoRoot')) {
        $value = [string]$hooks['RepoRoot']
        if ([string]::IsNullOrEmpty($value)) {
            return $null
        }
        return $value
    }

    $dir = (Get-Location).ProviderPath
    while ($dir) {
        $marker = Join-Path $dir 'Scripts\docker-compose.feed-token.ps1'
        $composeFile = Join-Path $dir 'docker-compose.yml'
        if ((Test-Path -LiteralPath $marker) -and (Test-Path -LiteralPath $composeFile)) {
            return $dir
        }
        $parent = Split-Path -Parent $dir
        if (-not $parent -or $parent -eq $dir) {
            break
        }
        $dir = $parent
    }
    return $null
}

function Read-LinkCloudFeedTokenFile {
    param([Parameter(Mandatory = $true)][string]$Path)
    $token = ''
    $expires = [int64]0
    $expiresPrefix = 'AZURE_ARTIFACTS_PAT_EXPIRES_ON='
    $tokenPrefix = 'AZURE_ARTIFACTS_PAT='
    foreach ($line in [System.IO.File]::ReadAllLines($Path)) {
        if ($line.StartsWith($expiresPrefix)) {
            $parsed = [int64]0
            [void][int64]::TryParse($line.Substring($expiresPrefix.Length), [ref]$parsed)
            $expires = $parsed
        } elseif ($line.StartsWith($tokenPrefix)) {
            $token = $line.Substring($tokenPrefix.Length)
        }
    }
    return [pscustomobject]@{
        Token = $token
        Expires = $expires
    }
}

function Resolve-LinkCloudDockerExecutable {
    $hooks = $global:LinkCloudFeedTokenHooks
    if ($hooks -and $hooks.ContainsKey('DockerExecutable') -and $hooks['DockerExecutable']) {
        return [string]$hooks['DockerExecutable']
    }
    $cmd = Get-Command docker.exe -CommandType Application -ErrorAction SilentlyContinue | Select-Object -First 1
    if (-not $cmd) {
        $cmd = Get-Command docker -CommandType Application -ErrorAction SilentlyContinue | Select-Object -First 1
    }
    if (-not $cmd -or -not $cmd.Source) {
        return $null
    }
    return [string]$cmd.Source
}

function Invoke-LinkCloudDockerProcess {
    param([string[]]$ArgumentList)
    if (-not $ArgumentList) {
        $ArgumentList = @()
    }
    $hooks = $global:LinkCloudFeedTokenHooks
    if ($hooks -and $hooks.ContainsKey('Docker')) {
        if ($MyInvocation.ExpectingInput) {
            $code = $input | & $hooks['Docker'] $ArgumentList
        } else {
            $code = & $hooks['Docker'] $ArgumentList
        }
        if ($null -eq $code) { $code = 0 }
        $global:LASTEXITCODE = [int]$code
        return
    }
    $exe = Resolve-LinkCloudDockerExecutable
    if (-not $exe) {
        Write-LinkCloudMessage "docker was not found on PATH."
        $global:LASTEXITCODE = 1
        return
    }
    if ($MyInvocation.ExpectingInput) {
        $input | & $exe @ArgumentList
    } else {
        & $exe @ArgumentList
    }
    $global:LASTEXITCODE = $LASTEXITCODE
}

function Invoke-LinkCloudFeedTokenFetch {
    param([Parameter(Mandatory = $true)][string]$Root)
    $hooks = $global:LinkCloudFeedTokenHooks
    if ($hooks -and $hooks.ContainsKey('Fetch')) {
        $code = & $hooks['Fetch'] $Root
        if ($null -eq $code) { $code = 0 }
        $global:LASTEXITCODE = [int]$code
        return [int]$code
    }
    $fetch = Join-Path $Root 'Scripts\docker-compose.feed-token.ps1'
    if (-not (Test-Path -LiteralPath $fetch)) {
        Write-LinkCloudMessage "The feed token script was not found at $fetch."
        $global:LASTEXITCODE = 1
        return 1
    }
    # az login writes account JSON to the success stream. Capturing this
    # function would otherwise return that JSON plus the exit code, and a
    # later comparison against 0 would treat a successful fetch as failure.
    & $fetch -RepoRoot $Root *>&1 | Out-Host
    $code = $LASTEXITCODE
    if ($null -eq $code) { $code = 0 }
    $global:LASTEXITCODE = [int]$code
    return [int]$code
}

function Test-LinkCloudFeedTokenFresh {
    param(
        $Read,
        [int64]$Now
    )
    if ($null -eq $Read) { return $false }
    if ([string]::IsNullOrEmpty([string]$Read.Token)) { return $false }
    if ([int64]$Read.Expires -le 0) { return $false }
    return (([int64]$Read.Expires - $Now) -ge 600)
}

function Test-LinkCloudFeedTokenUnexpired {
    param(
        $Read,
        [int64]$Now
    )
    if ($null -eq $Read) { return $false }
    if ([string]::IsNullOrEmpty([string]$Read.Token)) { return $false }
    if ([int64]$Read.Expires -le 0) { return $false }
    # A refresh can return the cached token while it is still valid. That
    # result is usable. The 10 minute check only decides whether to try.
    return (([int64]$Read.Expires - $Now) -gt 0)
}

function Invoke-LinkCloudCompose {
    param(
        [Parameter(Mandatory = $true)][string]$RepoRoot,
        [string[]]$ComposeArguments,
        [string[]]$DockerArguments
    )
    if (-not $ComposeArguments) {
        $ComposeArguments = @()
    }
    $envFile = Join-Path $RepoRoot '.azure-artifacts.env'
    $now = Get-LinkCloudNowEpoch
    $read = $null
    if (Test-Path -LiteralPath $envFile) {
        $read = Read-LinkCloudFeedTokenFile -Path $envFile
    }
    $fresh = Test-LinkCloudFeedTokenFresh -Read $read -Now $now
    if (-not $fresh) {
        $fetchCode = Invoke-LinkCloudFeedTokenFetch -Root $RepoRoot
        $read = $null
        if ($fetchCode -eq 0 -and (Test-Path -LiteralPath $envFile)) {
            $read = Read-LinkCloudFeedTokenFile -Path $envFile
            $now = Get-LinkCloudNowEpoch
        }
        $fresh = ($fetchCode -eq 0) -and (Test-LinkCloudFeedTokenUnexpired -Read $read -Now $now)
        if (-not $fresh) {
            Write-LinkCloudMessage (Get-LinkCloudFeedTokenMissingMessage)
            $global:LASTEXITCODE = 1
            return
        }
    }

    $token = [string]$read.Token
    $previousSet = Test-Path Env:AZURE_ARTIFACTS_PAT
    $previous = $env:AZURE_ARTIFACTS_PAT
    $savedCode = 1
    try {
        $env:AZURE_ARTIFACTS_PAT = $token
        if ($PSBoundParameters.ContainsKey('DockerArguments')) {
            $dockerArgs = @($DockerArguments)
        } else {
            $dockerArgs = @('compose') + @($ComposeArguments)
        }
        if ($MyInvocation.ExpectingInput) {
            $input | Invoke-LinkCloudDockerProcess -ArgumentList $dockerArgs
        } else {
            Invoke-LinkCloudDockerProcess -ArgumentList $dockerArgs
        }
        $savedCode = $global:LASTEXITCODE
    } finally {
        if ($previousSet) {
            $env:AZURE_ARTIFACTS_PAT = $previous
        } else {
            Remove-Item Env:AZURE_ARTIFACTS_PAT -ErrorAction SilentlyContinue
        }
        $global:LASTEXITCODE = $savedCode
        Remove-Variable token, previous -ErrorAction SilentlyContinue
    }
}

function global:compose {
    $root = Find-LinkCloudRepoRoot
    if (-not $root) {
        Write-LinkCloudMessage "compose is only available inside the link-cloud repo."
        $global:LASTEXITCODE = 1
        return
    }
    if ($MyInvocation.ExpectingInput) {
        $input | Invoke-LinkCloudCompose -RepoRoot $root -ComposeArguments @($args)
    } else {
        Invoke-LinkCloudCompose -RepoRoot $root -ComposeArguments @($args)
    }
}

function Test-LinkCloudComposeCommand {
    param([string[]]$Arguments)
    $needsValue = @{
        '--context' = $true
        '--config' = $true
        '--host' = $true
        '--log-level' = $true
        '--tlscacert' = $true
        '--tlscert' = $true
        '--tlskey' = $true
        '-c' = $true
        '-H' = $true
        '-l' = $true
    }
    $expectValue = $false
    foreach ($arg in $Arguments) {
        if ($expectValue) {
            $expectValue = $false
            continue
        }
        if ($arg -eq 'compose') { return $true }
        if ($arg -eq '--') { return $false }
        if ($arg -match '^--[^=]+=') { continue }
        if ($needsValue.ContainsKey($arg)) {
            $expectValue = $true
            continue
        }
        if ($arg -match '^-(c|H|l).+') { continue }
        if ($arg.StartsWith('-')) { continue }
        return $false
    }
    return $false
}

function global:docker {
    $hooks = $global:LinkCloudFeedTokenHooks
    $skipReload = $hooks -and $hooks.ContainsKey('SkipReload') -and $hooks['SkipReload']
    if (-not $skipReload) {
        $reloadRoot = Find-LinkCloudRepoRoot
        if ($reloadRoot) {
            $repoProfile = Join-Path $reloadRoot 'Scripts\docker-compose.feed-token-profile.ps1'
            $current = $PSCommandPath
            if ($current -and (Test-Path -LiteralPath $repoProfile)) {
                $repoFull = (Resolve-Path -LiteralPath $repoProfile).Path
                $currentFull = (Resolve-Path -LiteralPath $current).Path
                if ($repoFull -ne $currentFull) {
                    . $repoProfile
                    $reloaded = Get-Command -Name docker -CommandType Function
                    if ($MyInvocation.ExpectingInput) {
                        $input | & $reloaded @args
                    } else {
                        & $reloaded @args
                    }
                    return
                }
            }
        }
    }

    $root = Find-LinkCloudRepoRoot
    if ($root -and (Test-LinkCloudComposeCommand -Arguments @($args))) {
        if ($MyInvocation.ExpectingInput) {
            $input | Invoke-LinkCloudCompose -RepoRoot $root -DockerArguments @($args)
        } else {
            Invoke-LinkCloudCompose -RepoRoot $root -DockerArguments @($args)
        }
        return
    }
    if ($MyInvocation.ExpectingInput) {
        $input | Invoke-LinkCloudDockerProcess -ArgumentList @($args)
    } else {
        Invoke-LinkCloudDockerProcess -ArgumentList @($args)
    }
}
