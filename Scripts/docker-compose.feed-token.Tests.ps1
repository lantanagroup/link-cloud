# Wrapper tests. They mock az and docker. They do not need a network or a real token.
$ErrorActionPreference = 'Stop'
$passed = 0
$failed = 0
$sentinel = 'unit-test-feed-token-sentinel'

function Write-Result {
    param([bool]$Ok, [string]$Name)
    if ($Ok) {
        $script:passed++
        Write-Output "PASS $Name"
    } else {
        $script:failed++
        Write-Output "FAIL $Name"
    }
}

function Reset-Hooks {
    $global:LinkCloudFeedTokenHooks = @{
        SkipReload = $true
        Messages = (New-Object System.Collections.Generic.List[string])
        FetchCalls = 0
        DockerCalls = (New-Object System.Collections.Generic.List[object])
        DockerSaw = 'absent'
    }
    if (Test-Path Env:AZURE_ARTIFACTS_PAT) {
        Remove-Item Env:AZURE_ARTIFACTS_PAT
    }
}

function New-TempRepo {
    $dir = Join-Path ([System.IO.Path]::GetTempPath()) ("feed-token-test-" + [guid]::NewGuid().ToString('n'))
    New-Item -ItemType Directory -Path $dir | Out-Null
    return $dir
}

function Write-TokenFile {
    param([string]$Repo, [string]$Token, [int64]$Expires)
    $path = Join-Path $Repo '.azure-artifacts.env'
    $utf8 = New-Object System.Text.UTF8Encoding $false
    [System.IO.File]::WriteAllText($path, "AZURE_ARTIFACTS_PAT=$Token`nAZURE_ARTIFACTS_PAT_EXPIRES_ON=$Expires`n", $utf8)
}

function Set-DockerHook {
    param([int]$ExitCode = 0)
    $global:LinkCloudFeedTokenHooks['Docker'] = {
        param($ArgumentList)
        $hooks = $global:LinkCloudFeedTokenHooks
        $hooks['DockerCalls'].Add(@($ArgumentList))
        if (Test-Path Env:AZURE_ARTIFACTS_PAT) {
            if ($env:AZURE_ARTIFACTS_PAT -eq $sentinel) { $hooks['DockerSaw'] = 'match' }
            else { $hooks['DockerSaw'] = 'other' }
        } else {
            $hooks['DockerSaw'] = 'absent'
        }
        return $ExitCode
    }.GetNewClosure()
}

function Set-FetchHook {
    param([int]$ExitCode = 0, [int64]$Expires = 0, [switch]$WriteFile)
    $global:LinkCloudFeedTokenHooks['Fetch'] = {
        param($Root)
        $hooks = $global:LinkCloudFeedTokenHooks
        $hooks['FetchCalls'] = [int]$hooks['FetchCalls'] + 1
        if ($WriteFile) {
            $path = Join-Path $Root '.azure-artifacts.env'
            $utf8 = New-Object System.Text.UTF8Encoding $false
            [System.IO.File]::WriteAllText($path, "AZURE_ARTIFACTS_PAT=$sentinel`nAZURE_ARTIFACTS_PAT_EXPIRES_ON=$Expires`n", $utf8)
        }
        return $ExitCode
    }.GetNewClosure()
}

$profile = Join-Path $PSScriptRoot 'docker-compose.feed-token-profile.ps1'
. $profile

$repo = New-TempRepo
try {
    $now = [int64]1700000000
    Reset-Hooks
    $global:LinkCloudFeedTokenHooks['RepoRoot'] = $repo
    $global:LinkCloudFeedTokenHooks['NowEpoch'] = $now

    # Missing env file, fetch fails: message and docker is not started.
    Reset-Hooks
    $global:LinkCloudFeedTokenHooks['RepoRoot'] = $repo
    $global:LinkCloudFeedTokenHooks['NowEpoch'] = $now
    Set-DockerHook -ExitCode 0
    Set-FetchHook -ExitCode 1
    Invoke-LinkCloudCompose -RepoRoot $repo -ComposeArguments @('up')
    $missing = ($global:LinkCloudFeedTokenHooks['Messages'] -join "`n")
    Write-Result ($LASTEXITCODE -eq 1) 'missing file fetch failure sets exit 1'
    Write-Result ($global:LinkCloudFeedTokenHooks['DockerCalls'].Count -eq 0) 'missing file fetch failure does not call docker'
    Write-Result ($missing.Contains('Azure token missing. Run this one-time setup: powershell -NoProfile -ExecutionPolicy Bypass -File ./Scripts/docker-compose.feed-token-install.ps1')) 'missing message has the PowerShell setup line'
    Write-Result ($missing.Contains('PowerShell profile line: . "$env:USERPROFILE\.link-cloud\docker-compose.feed-token-profile.ps1" # link-cloud-feed-token')) 'missing message has the PowerShell profile line'
    Write-Result ($missing.Contains('Git Bash: bash ./Scripts/docker-compose.feed-token-install.sh')) 'missing message has the Git Bash setup line'
    Write-Result ($missing.Contains('Git Bash profile line: . "$HOME/.link-cloud/docker-compose.feed-token.sh" # link-cloud-feed-token')) 'missing message has the Git Bash profile line'
    Write-Result (-not $missing.Contains($sentinel)) 'missing message does not contain the token'

    # Expired token refreshes.
    Reset-Hooks
    $global:LinkCloudFeedTokenHooks['RepoRoot'] = $repo
    $global:LinkCloudFeedTokenHooks['NowEpoch'] = $now
    Write-TokenFile -Repo $repo -Token $sentinel -Expires ($now - 5)
    Set-DockerHook -ExitCode 0
    Set-FetchHook -ExitCode 0 -Expires ($now + 3600) -WriteFile
    Invoke-LinkCloudCompose -RepoRoot $repo -ComposeArguments @('ps')
    Write-Result ($global:LinkCloudFeedTokenHooks['FetchCalls'] -eq 1) 'expired token calls fetch'
    Write-Result ($global:LinkCloudFeedTokenHooks['DockerCalls'].Count -eq 1) 'expired token still runs docker after refresh'
    Write-Result ($global:LinkCloudFeedTokenHooks['DockerSaw'] -eq 'match') 'refreshed token is passed to docker'

    # Near expiry (under 10 minutes) refreshes.
    Reset-Hooks
    $global:LinkCloudFeedTokenHooks['RepoRoot'] = $repo
    $global:LinkCloudFeedTokenHooks['NowEpoch'] = $now
    Write-TokenFile -Repo $repo -Token $sentinel -Expires ($now + 599)
    Set-DockerHook -ExitCode 0
    Set-FetchHook -ExitCode 0 -Expires ($now + 3600) -WriteFile
    Invoke-LinkCloudCompose -RepoRoot $repo -ComposeArguments @('ps')
    Write-Result ($global:LinkCloudFeedTokenHooks['FetchCalls'] -eq 1) 'token with under 10 minutes left calls fetch'

    # Valid token does not fetch.
    Reset-Hooks
    $global:LinkCloudFeedTokenHooks['RepoRoot'] = $repo
    $global:LinkCloudFeedTokenHooks['NowEpoch'] = $now
    Write-TokenFile -Repo $repo -Token $sentinel -Expires ($now + 3600)
    Set-DockerHook -ExitCode 9
    Set-FetchHook -ExitCode 1
    Invoke-LinkCloudCompose -RepoRoot $repo -ComposeArguments @('up', 'my service')
    $calls = @($global:LinkCloudFeedTokenHooks['DockerCalls'][0])
    Write-Result ($global:LinkCloudFeedTokenHooks['FetchCalls'] -eq 0) 'valid token does not call fetch'
    Write-Result ($LASTEXITCODE -eq 9) 'docker exit code is propagated'
    Write-Result ($calls.Count -eq 3 -and $calls[0] -eq 'compose' -and $calls[1] -eq 'up' -and $calls[2] -eq 'my service') 'args with spaces are passed through unchanged'
    Write-Result ($global:LinkCloudFeedTokenHooks['DockerSaw'] -eq 'match') 'valid token is visible to the docker child'
    Write-Result (-not (Test-Path Env:AZURE_ARTIFACTS_PAT)) 'token is removed from the parent session'
    Write-Result ($global:LinkCloudFeedTokenHooks['DockerCalls'].Count -eq 1) 'docker is invoked once'

    # Exactly 10 minutes left stays valid.
    Reset-Hooks
    $global:LinkCloudFeedTokenHooks['RepoRoot'] = $repo
    $global:LinkCloudFeedTokenHooks['NowEpoch'] = $now
    Write-TokenFile -Repo $repo -Token $sentinel -Expires ($now + 600)
    Set-DockerHook -ExitCode 0
    Set-FetchHook -ExitCode 1
    Invoke-LinkCloudCompose -RepoRoot $repo -ComposeArguments @('ps')
    Write-Result ($global:LinkCloudFeedTokenHooks['FetchCalls'] -eq 0) 'token with 10 minutes left does not refresh'

    # Outside the repo, compose is passed through and docker is not given a token.
    Reset-Hooks
    $global:LinkCloudFeedTokenHooks['RepoRoot'] = ''
    $global:LinkCloudFeedTokenHooks['NowEpoch'] = $now
    Set-DockerHook -ExitCode 0
    Set-FetchHook -ExitCode 1
    docker compose ps
    $outside = @($global:LinkCloudFeedTokenHooks['DockerCalls'][0])
    Write-Result ($global:LinkCloudFeedTokenHooks['FetchCalls'] -eq 0) 'outside the repo does not fetch'
    Write-Result ($outside.Count -eq 2 -and $outside[0] -eq 'compose' -and $outside[1] -eq 'ps') 'outside the repo passes compose through'
    Write-Result ($global:LinkCloudFeedTokenHooks['DockerSaw'] -eq 'absent') 'outside the repo does not set the token'

    # Non-compose subcommand.
    Reset-Hooks
    $global:LinkCloudFeedTokenHooks['RepoRoot'] = $repo
    Set-DockerHook -ExitCode 4
    Set-FetchHook -ExitCode 1
    docker version
    $version = @($global:LinkCloudFeedTokenHooks['DockerCalls'][0])
    Write-Result ($global:LinkCloudFeedTokenHooks['FetchCalls'] -eq 0) 'non-compose command does not fetch'
    Write-Result ($version.Count -eq 1 -and $version[0] -eq 'version') 'non-compose command is passed through'
    Write-Result ($LASTEXITCODE -eq 4) 'non-compose exit code is propagated'
    Write-Result ($global:LinkCloudFeedTokenHooks['DockerSaw'] -eq 'absent') 'non-compose command does not set the token'

    # Message text matches the image restore helper.
    $restore = [System.IO.File]::ReadAllText((Join-Path $PSScriptRoot 'docker-compose.restore-feed.sh'))
    $expectedMessage = Get-LinkCloudFeedTokenMissingMessage
    $restoreHasMessage = $true
    foreach ($line in ($expectedMessage -split "`r?`n")) {
        if (-not $restore.Contains($line)) { $restoreHasMessage = $false }
    }
    Write-Result $restoreHasMessage 'restore helper prints the same missing-token message'

    $dockerfiles = @(
        (Join-Path $PSScriptRoot '..\DotNet\Automation.UI\Dockerfile'),
        (Join-Path $PSScriptRoot '..\DotNet\MockFhirServer\Dockerfile')
    )
    $dockerfilesClean = $true
    foreach ($dockerfile in $dockerfiles) {
        $text = [System.IO.File]::ReadAllText($dockerfile)
        if ($text.Contains('FEED_ACCESSTOKEN') -or $text.Contains('--password') -or -not $text.Contains('id=nuget,sharing=locked') -or -not $text.Contains('id=feed_accesstoken')) {
            $dockerfilesClean = $false
        }
    }
    Write-Result $dockerfilesClean 'feed Dockerfiles use only the secret mount and the nuget cache'

    $composeText = [System.IO.File]::ReadAllText((Join-Path $PSScriptRoot '..\docker-compose.yml'))
    Write-Result ((-not $composeText.Contains('FEED_ACCESSTOKEN')) -and $composeText.Contains('feed_accesstoken:') -and $composeText.Contains('environment: AZURE_ARTIFACTS_PAT')) 'compose keeps the secret and drops the build arg'

    $ignoreLines = @(Get-Content -LiteralPath (Join-Path $PSScriptRoot '..\.dockerignore'))
    $excludeAt = 0
    $includeAt = 0
    for ($i = 0; $i -lt $ignoreLines.Count; $i++) {
        $trimmed = $ignoreLines[$i].Trim()
        if ($trimmed -eq '**/docker-compose*') { $excludeAt = $i }
        if ($trimmed -eq '!Scripts/docker-compose.restore-feed.sh') { $includeAt = $i }
    }
    Write-Result ($includeAt -gt $excludeAt) 'dockerignore keeps the restore helper after the docker-compose exclusion'
    $tokenIgnored = $false
    foreach ($ignoreLine in $ignoreLines) {
        if ($ignoreLine.Trim() -eq '.azure-artifacts.env') { $tokenIgnored = $true }
    }
    Write-Result $tokenIgnored 'dockerignore excludes the local token file'

    # Installer is idempotent and does not touch the real profile.
    $profilePath = Join-Path $repo 'profile.ps1'
    $installDir = Join-Path $repo 'install-dir'
    $installer = Join-Path $PSScriptRoot 'docker-compose.feed-token-install.ps1'
    & $installer -ProfilePath $profilePath -InstallDir $installDir | Out-Null
    & $installer -ProfilePath $profilePath -InstallDir $installDir | Out-Null
    $profileText = [System.IO.File]::ReadAllText($profilePath)
    $markerCount = ([regex]::Matches($profileText, 'link-cloud-feed-token')).Count
    Write-Result ($markerCount -eq 1) 'installer adds the profile line once'
    Write-Result ($profileText.Contains('. "$env:USERPROFILE\.link-cloud\docker-compose.feed-token-profile.ps1" # link-cloud-feed-token')) 'installer writes the profile snippet'
    Write-Result (Test-Path -LiteralPath (Join-Path $installDir 'docker-compose.feed-token-profile.ps1')) 'installer copies the profile script'

    # Fetch script with a mock az. The sentinel must not appear in the child output.
    $mockDir = Join-Path $repo 'mock-az'
    New-Item -ItemType Directory -Path $mockDir | Out-Null
    $mock = Join-Path $mockDir 'az.cmd'
    $mockBody = @"
@echo off
if /I "%~1"=="account" (
  if /I "%~2"=="show" exit /b 0
  if /I "%~2"=="get-access-token" (
    echo {"accessToken":"$sentinel","expires_on":"1893456000","tokenType":"Bearer"}
    exit /b 0
  )
)
exit /b 1
"@
    [System.IO.File]::WriteAllText($mock, $mockBody)
    $spaceRoot = Join-Path $repo 'space dir'
    New-Item -ItemType Directory -Path $spaceRoot | Out-Null
    $envFile = Join-Path $spaceRoot '.azure-artifacts.env'
    if (Test-Path $envFile) { Remove-Item $envFile }
    $stdoutLog = Join-Path $repo 'fetch-stdout.txt'
    $stderrLog = Join-Path $repo 'fetch-stderr.txt'
    $fetch = Join-Path $PSScriptRoot 'docker-compose.feed-token.ps1'
    $mockInSpace = Join-Path $spaceRoot 'az.cmd'
    Copy-Item -LiteralPath $mock -Destination $mockInSpace
    $argLine = '-NoProfile -ExecutionPolicy Bypass -File "{0}" -RepoRoot "{1}" -EnvFile "{2}" -AzExecutable "{3}"' -f $fetch, $spaceRoot, $envFile, $mockInSpace
    $proc = Start-Process -FilePath powershell.exe -ArgumentList $argLine -Wait -PassThru -RedirectStandardOutput $stdoutLog -RedirectStandardError $stderrLog -WindowStyle Hidden
    $stdoutText = [System.IO.File]::ReadAllText($stdoutLog)
    $stderrText = [System.IO.File]::ReadAllText($stderrLog)
    $combined = $stdoutText + $stderrText
    Write-Result ($proc.ExitCode -eq 0) 'fetch script succeeds with mock az'
    Write-Result (-not $combined.Contains($sentinel)) 'fetch script output does not contain the token'
    $written = [System.IO.File]::ReadAllText($envFile)
    $fileHasToken = $written.Contains("AZURE_ARTIFACTS_PAT=$sentinel") -and $written.Contains('AZURE_ARTIFACTS_PAT_EXPIRES_ON=1893456000')
    Write-Result $fileHasToken 'fetch script writes the token and epoch expiry'
    $acl = Get-Acl -LiteralPath $envFile
    $rules = @($acl.Access)
    $onlyCurrentUser = ($rules.Count -eq 1)
    Write-Result $onlyCurrentUser 'fetch script limits the token file ACL to one entry'
    Write-Result ($spaceRoot.Contains(' ')) 'fetch script accepts a repo path that contains a space'

    # A fetch script that prints on the success stream must still return only its exit code.
    $stubRoot = Join-Path $repo 'stub-root'
    $stubScripts = Join-Path $stubRoot 'Scripts'
    New-Item -ItemType Directory -Path $stubScripts | Out-Null
    $stub = Join-Path $stubScripts 'docker-compose.feed-token.ps1'
    $stubBody = @"
param([string]`$RepoRoot)
Write-Output '{"subscription":"not-a-token"}'
`$dest = Join-Path `$RepoRoot '.azure-artifacts.env'
`$utf8 = New-Object System.Text.UTF8Encoding `$false
[System.IO.File]::WriteAllText(`$dest, "AZURE_ARTIFACTS_PAT=$sentinel``nAZURE_ARTIFACTS_PAT_EXPIRES_ON=1893456000``n", `$utf8)
exit 0
"@
    [System.IO.File]::WriteAllText($stub, $stubBody)
    $global:LinkCloudFeedTokenHooks.Remove('Fetch')
    $noisyCode = Invoke-LinkCloudFeedTokenFetch -Root $stubRoot
    Write-Result (($noisyCode -is [int]) -and ($noisyCode -eq 0)) 'fetch extra output does not replace the exit code'
    Remove-Item -LiteralPath (Join-Path $stubRoot '.azure-artifacts.env') -Force
    Set-DockerHook -ExitCode 3
    $global:LinkCloudFeedTokenHooks['DockerCalls'].Clear()
    $global:LinkCloudFeedTokenHooks['NowEpoch'] = [int64]1700000000
    $global:LinkCloudFeedTokenHooks.Remove('Fetch')
    Invoke-LinkCloudCompose -RepoRoot $stubRoot -ComposeArguments @('up')
    $noisyDockerCalls = $global:LinkCloudFeedTokenHooks['DockerCalls'].Count
    Write-Result (($global:LASTEXITCODE -eq 3) -and ($noisyDockerCalls -eq 1)) 'compose starts docker after a fetch script prints extra output'

    # Messages collected above must not contain the sentinel either.
    $hookText = ($global:LinkCloudFeedTokenHooks['Messages'] -join "`n")
    Write-Result (-not $hookText.Contains($sentinel)) 'hook messages do not contain the token'
} finally {
    Remove-Item -LiteralPath $repo -Recurse -Force -ErrorAction SilentlyContinue
    $global:LinkCloudFeedTokenHooks = $null
    if (Test-Path Env:AZURE_ARTIFACTS_PAT) { Remove-Item Env:AZURE_ARTIFACTS_PAT }
}

Write-Output "PASSED=$passed FAILED=$failed TOTAL=$($passed + $failed)"
if ($failed -gt 0) { exit 1 }
exit 0
