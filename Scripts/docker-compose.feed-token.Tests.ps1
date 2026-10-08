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
        $stdinText = ''
        foreach ($item in $input) { $stdinText += [string]$item }
        $hooks['Stdin'] = $stdinText
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

$profileScript = Join-Path $PSScriptRoot 'docker-compose.feed-token-profile.ps1'
. $profileScript

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
    Write-Result ($missing.Contains('Azure token missing. Run this one-time setup: powershell -NoProfile -ExecutionPolicy Bypass -File ./Scripts/docker-compose.feed-token-install.ps1 -ProfilePath "$PROFILE"')) 'missing message has the PowerShell setup line'
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

    # Azure CLI can return the cached token with under 10 minutes left.
    Reset-Hooks
    $global:LinkCloudFeedTokenHooks['RepoRoot'] = $repo
    $global:LinkCloudFeedTokenHooks['NowEpoch'] = $now
    Write-TokenFile -Repo $repo -Token $sentinel -Expires ($now + 599)
    Set-DockerHook -ExitCode 0
    Set-FetchHook -ExitCode 0 -Expires ($now + 400) -WriteFile
    Invoke-LinkCloudCompose -RepoRoot $repo -ComposeArguments @('ps')
    Write-Result ($global:LinkCloudFeedTokenHooks['FetchCalls'] -eq 1) 'short cached token still calls fetch'
    Write-Result ($global:LinkCloudFeedTokenHooks['DockerCalls'].Count -eq 1) 'short cached token still runs docker'

    # An expired result from fetch does not start docker.
    Reset-Hooks
    $global:LinkCloudFeedTokenHooks['RepoRoot'] = $repo
    $global:LinkCloudFeedTokenHooks['NowEpoch'] = $now
    Write-TokenFile -Repo $repo -Token $sentinel -Expires ($now + 599)
    Set-DockerHook -ExitCode 0
    Set-FetchHook -ExitCode 0 -Expires ($now - 1) -WriteFile
    Invoke-LinkCloudCompose -RepoRoot $repo -ComposeArguments @('ps')
    $expiredFetch = ($global:LinkCloudFeedTokenHooks['Messages'] -join "`n")
    Write-Result ($global:LinkCloudFeedTokenHooks['DockerCalls'].Count -eq 0) 'expired fetch result does not call docker'
    Write-Result ($expiredFetch.Contains('Azure token missing.')) 'expired fetch result prints the missing-token message'

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

    # Global docker options still reach the compose token path.
    Reset-Hooks
    $global:LinkCloudFeedTokenHooks['RepoRoot'] = $repo
    $global:LinkCloudFeedTokenHooks['NowEpoch'] = $now
    Write-TokenFile -Repo $repo -Token $sentinel -Expires ($now + 3600)
    Set-DockerHook -ExitCode 0
    Set-FetchHook -ExitCode 1
    docker --context desktop-linux compose build automation-ui
    $withContext = @($global:LinkCloudFeedTokenHooks['DockerCalls'][0])
    Write-Result ($global:LinkCloudFeedTokenHooks['FetchCalls'] -eq 0) 'docker global option does not refresh a valid token'
    Write-Result ($global:LinkCloudFeedTokenHooks['DockerSaw'] -eq 'match') 'docker global option sets the token'
    Write-Result ($withContext.Count -eq 5 -and $withContext[0] -eq '--context' -and $withContext[1] -eq 'desktop-linux' -and $withContext[2] -eq 'compose' -and $withContext[3] -eq 'build' -and $withContext[4] -eq 'automation-ui') 'docker global option keeps the original arguments'
    docker --context desktop-linux version
    $contextVersion = @($global:LinkCloudFeedTokenHooks['DockerCalls'][1])
    Write-Result ($contextVersion.Count -eq 3 -and $contextVersion[2] -eq 'version') 'docker global option on a non-compose command is passed through'

    # Pipeline input reaches docker. A command without a pipeline does not get an empty pipe.
    Reset-Hooks
    $global:LinkCloudFeedTokenHooks['RepoRoot'] = ''
    Set-DockerHook -ExitCode 0
    'hello' | docker exec -i db
    Write-Result ($global:LinkCloudFeedTokenHooks['Stdin'] -eq 'hello') 'docker wrapper forwards pipeline input outside the repo'
    docker version
    Write-Result ([string]$global:LinkCloudFeedTokenHooks['Stdin'] -eq '') 'docker wrapper does not invent pipeline input'
    Reset-Hooks
    $global:LinkCloudFeedTokenHooks['RepoRoot'] = $repo
    Set-DockerHook -ExitCode 0
    'hello' | docker exec -i db
    Write-Result ($global:LinkCloudFeedTokenHooks['Stdin'] -eq 'hello') 'docker wrapper forwards pipeline input inside the repo'
    $copyRoot = Join-Path $repo 'reload-copy'
    $copyScripts = Join-Path $copyRoot 'Scripts'
    New-Item -ItemType Directory -Path $copyScripts | Out-Null
    $copyProfile = Join-Path $copyScripts 'docker-compose.feed-token-profile.ps1'
    $copyBody = "`$global:LinkCloudFeedTokenHooks['Reloaded'] = `$true`r`n" + [System.IO.File]::ReadAllText((Join-Path $PSScriptRoot 'docker-compose.feed-token-profile.ps1'))
    [System.IO.File]::WriteAllText($copyProfile, $copyBody)
    Reset-Hooks
    $global:LinkCloudFeedTokenHooks['SkipReload'] = $false
    $global:LinkCloudFeedTokenHooks['RepoRoot'] = $copyRoot
    $global:LinkCloudFeedTokenHooks['Reloaded'] = $false
    Set-DockerHook -ExitCode 0
    'hello' | docker exec -i db
    Write-Result ((-not $global:LinkCloudFeedTokenHooks['Reloaded']) -and ($global:LinkCloudFeedTokenHooks['Stdin'] -eq 'hello')) 'non-compose docker does not load the checkout script'
    Write-TokenFile -Repo $copyRoot -Token $sentinel -Expires ($now + 3600)
    Reset-Hooks
    $global:LinkCloudFeedTokenHooks['SkipReload'] = $false
    $global:LinkCloudFeedTokenHooks['RepoRoot'] = $copyRoot
    $global:LinkCloudFeedTokenHooks['NowEpoch'] = $now
    $global:LinkCloudFeedTokenHooks['Reloaded'] = $false
    Set-DockerHook -ExitCode 0
    docker compose version
    Write-Result ($global:LinkCloudFeedTokenHooks['Reloaded'] -eq $true) 'compose loads the checkout script'
    $secondBody = "`$global:LinkCloudFeedTokenHooks['Reloaded'] = 'second'`r`n" + [System.IO.File]::ReadAllText((Join-Path $PSScriptRoot 'docker-compose.feed-token-profile.ps1'))
    [System.IO.File]::WriteAllText($copyProfile, $secondBody)
    docker compose version
    Write-Result ($global:LinkCloudFeedTokenHooks['Reloaded'] -eq 'second') 'compose reloads a changed checkout script'
    $global:LinkCloudFeedTokenHooks['SkipReload'] = $true

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
    $fetchScript = [System.IO.File]::ReadAllText((Join-Path $PSScriptRoot 'docker-compose.feed-token.ps1'))
    $bashFetch = [System.IO.File]::ReadAllText((Join-Path $PSScriptRoot 'docker-compose.feed-token-fetch.sh'))
    Write-Result ($fetchScript.Contains('--allow-no-subscriptions') -and $bashFetch.Contains('--allow-no-subscriptions')) 'login allows an account with no Azure subscription'
    Write-Result ($fetchScript.Contains('*>&1 | Out-Host')) 'winget progress is not part of the az path'
    Write-Result ($fetchScript.Contains('$env:Path = "$prior;$userPath;$machinePath"')) 'session PATH stays ahead of the registry PATH'

    # Installer is idempotent and does not touch the real profile.
    $profilePath = Join-Path $repo 'profile.ps1'
    $installDir = Join-Path $repo 'install-dir'
    $installer = Join-Path $PSScriptRoot 'docker-compose.feed-token-install.ps1'
    $hostBefore = $null
    if (Test-Path -LiteralPath $PROFILE) { $hostBefore = [System.IO.File]::ReadAllBytes($PROFILE) }
    & $installer -ProfilePath $profilePath -InstallDir $installDir | Out-Null
    & $installer -ProfilePath $profilePath -InstallDir $installDir | Out-Null
    $profileText = [System.IO.File]::ReadAllText($profilePath)
    $markerCount = ([regex]::Matches($profileText, 'link-cloud-feed-token')).Count
    Write-Result ($markerCount -eq 1) 'installer adds the profile line once'
    $hostAfter = $null
    if (Test-Path -LiteralPath $PROFILE) { $hostAfter = [System.IO.File]::ReadAllBytes($PROFILE) }
    $hostUnchanged = ($null -eq $hostBefore -and $null -eq $hostAfter)
    if ($null -ne $hostBefore -and $null -ne $hostAfter -and $hostBefore.Length -eq $hostAfter.Length) {
        $hostUnchanged = $true
        for ($i = 0; $i -lt $hostBefore.Length; $i++) {
            if ($hostBefore[$i] -ne $hostAfter[$i]) { $hostUnchanged = $false; break }
        }
    }
    Write-Result ($hostUnchanged -and (Test-Path -LiteralPath $profilePath) -and $profileText.Contains('link-cloud-feed-token')) 'ProfilePath writes the given file and leaves the host profile unchanged'
    $documented = [System.IO.File]::ReadAllText((Join-Path $PSScriptRoot '..\DEVELOPMENT.md'))
    Write-Result ($documented.Contains('powershell -NoProfile -ExecutionPolicy Bypass -File ./Scripts/docker-compose.feed-token-install.ps1 -ProfilePath "$PROFILE"')) 'documented setup passes the calling shell profile'
    Write-Result ($documented.Contains('Get-ExecutionPolicy -List') -and $documented.Contains('Set-ExecutionPolicy -Scope CurrentUser RemoteSigned')) 'documented setup names the execution policy check'
    $installerText = [System.IO.File]::ReadAllText($installer)
    Write-Result ($installerText.Contains('Set-ExecutionPolicy -Scope CurrentUser RemoteSigned') -and $installerText.Contains("if (`$scope -eq 'Process')") -and $installerText.Contains("if (-not `$effective) { `$effective = 'Restricted' }")) 'installer warns from the saved execution policy'
    Write-Result ($installerText.Contains('if (-not $ProfilePath)') -and $installerText.Contains('$ProfilePath = $PROFILE')) 'installer defaults to the running host profile'
    Write-Result ($profileText.Contains($installDir) -and $profileText.Contains('docker-compose.feed-token-profile.ps1')) 'installer profile line uses the install directory'
    Write-Result ($installerText.Contains('. "$env:USERPROFILE\.link-cloud\docker-compose.feed-token-profile.ps1" # link-cloud-feed-token')) 'default install line stays the documented snippet'
    $loader = Join-Path $repo 'load-profile.ps1'
    $loaderBody = ". '$profilePath'`r`nif (Get-Command compose -CommandType Function -ErrorAction SilentlyContinue) { 'loaded' }`r`n"
    [System.IO.File]::WriteAllText($loader, $loaderBody)
    $loadOut = Join-Path $repo 'profile-load.txt'
    $loadErr = Join-Path $repo 'profile-load-err.txt'
    $loadProc = Start-Process -FilePath powershell.exe -ArgumentList ('-NoProfile -ExecutionPolicy Bypass -File "{0}"' -f $loader) -Wait -PassThru -RedirectStandardOutput $loadOut -RedirectStandardError $loadErr -WindowStyle Hidden
    $loadText = ''
    if (Test-Path $loadOut) { $loadText = [System.IO.File]::ReadAllText($loadOut) }
    Write-Result ($loadProc.ExitCode -eq 0 -and $loadText.Contains('loaded')) 'installer profile loads the copied script'
    Write-Result (Test-Path -LiteralPath (Join-Path $installDir 'docker-compose.feed-token-profile.ps1')) 'installer copies the profile script'
    $utf16Profile = Join-Path $repo 'profile-utf16.ps1'
    $utf16 = New-Object System.Text.UnicodeEncoding $false, $true
    [System.IO.File]::WriteAllText($utf16Profile, "Write-Host 'kept'`r`n", $utf16)
    & $installer -ProfilePath $utf16Profile -InstallDir $installDir | Out-Null
    $utf16Text = [System.IO.File]::ReadAllText($utf16Profile)
    Write-Result ($utf16Text.Contains('kept') -and $utf16Text.Contains('link-cloud-feed-token')) 'installer keeps an existing UTF-16 profile readable'
    $ansiProfile = Join-Path $repo 'profile-ansi.ps1'
    $ansiEnc = [System.Text.Encoding]::GetEncoding(1252)
    $ansiBody = "Write-Host 'caf$([char]0x00E9)'`r`n"
    [System.IO.File]::WriteAllBytes($ansiProfile, $ansiEnc.GetBytes($ansiBody))
    & $installer -ProfilePath $ansiProfile -InstallDir $installDir | Out-Null
    $ansiText = Get-Content -Raw -LiteralPath $ansiProfile
    Write-Result (($null -ne $ansiText) -and $ansiText.Contains([char]0x00E9) -and $ansiText.Contains('link-cloud-feed-token')) 'installer keeps an ANSI profile readable'
    $utf8Dir = Join-Path $repo ('caf' + [char]0x00E9)
    New-Item -ItemType Directory -Path $utf8Dir -Force | Out-Null
    $utf8Profile = Join-Path $utf8Dir 'profile.ps1'
    $utf8NoBom = New-Object System.Text.UTF8Encoding $false
    [System.IO.File]::WriteAllBytes($utf8Profile, $utf8NoBom.GetBytes("Write-Host 'caf$([char]0x00E9)'`r`n"))
    $setupArgs = '-NoProfile -ExecutionPolicy Bypass -File "{0}" -ProfilePath "{1}" -InstallDir "{2}"' -f $installer, $utf8Profile, $installDir
    $utf8Out = Join-Path $repo 'utf8-out.txt'
    $utf8Err = Join-Path $repo 'utf8-err.txt'
    $utf8Proc = Start-Process -FilePath powershell.exe -ArgumentList $setupArgs -Wait -PassThru -RedirectStandardOutput $utf8Out -RedirectStandardError $utf8Err -WindowStyle Hidden
    $utf8Text = ''
    if (Test-Path -LiteralPath $utf8Profile) { $utf8Text = [System.IO.File]::ReadAllText($utf8Profile) }
    Write-Result ($utf8Proc.ExitCode -eq 0 -and $utf8Text.Contains([char]0x00E9) -and $utf8Text.Contains('link-cloud-feed-token') -and -not $utf8Text.Contains([char]0x00C3)) 'documented setup keeps a BOM-less UTF-8 profile on a non-ASCII path'

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
    $temps = @(Get-ChildItem -LiteralPath $spaceRoot -Force -Filter '.azure-artifacts.*.tmp' -ErrorAction SilentlyContinue)
    Write-Result ($temps.Count -eq 0) 'fetch script does not leave a temporary token file'
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

    # Per-user ZIP install, then winget without --scope user. Neither test changes the real user PATH.
    $userPathBefore = [Environment]::GetEnvironmentVariable('Path', 'User')
    $toolDir = Join-Path $repo 'az-tools'
    New-Item -ItemType Directory -Path $toolDir | Out-Null
    $payload = Join-Path $toolDir 'payload.cmd'
    @"
@echo off
if /I "%~1"=="account" (
  if /I "%~2"=="show" exit /b 0
  if /I "%~2"=="get-access-token" (
    echo {"accessToken":"$sentinel","expires_on":1893456000}
    exit /b 0
  )
)
exit /b 0
"@ | Set-Content -Encoding ASCII -Path $payload
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $zipPath = Join-Path $repo 'azure-cli.zip'
    $zipArchive = [System.IO.Compression.ZipFile]::Open($zipPath, 'Create')
    try {
        $entry = $zipArchive.CreateEntry('bin/az.cmd')
        $entryStream = $entry.Open()
        try {
            $payloadBytes = [System.IO.File]::ReadAllBytes($payload)
            $entryStream.Write($payloadBytes, 0, $payloadBytes.Length)
        } finally { $entryStream.Dispose() }
    } finally { $zipArchive.Dispose() }
    $runner = Join-Path $repo 'run-install.ps1'
    @'
param(
    [string]$RepoRoot,
    [string]$EnvFile,
    [string]$InstallDirectory,
    [string]$ZipPackage,
    [string]$ZipUri,
    [string]$WingetExecutable,
    [string]$AzExecutable,
    [switch]$SkipUserPathUpdate
)
$env:PATH = $env:LINK_CLOUD_TEST_PATH
$ErrorActionPreference = 'Stop'
& $env:LINK_CLOUD_TEST_SCRIPT -RepoRoot $RepoRoot -EnvFile $EnvFile -InstallDirectory $InstallDirectory -ZipPackage $ZipPackage -ZipUri $ZipUri -WingetExecutable $WingetExecutable -AzExecutable $AzExecutable -SkipUserPathUpdate:$SkipUserPathUpdate *>&1 | Out-Host
exit $LASTEXITCODE
'@ | Set-Content -Encoding ASCII -Path $runner
    $fetchScriptPath = Join-Path $PSScriptRoot 'docker-compose.feed-token.ps1'
    $restricted = "$toolDir;C:\Windows\System32;C:\Windows"
    $env:LINK_CLOUD_TEST_PATH = $restricted
    $env:LINK_CLOUD_TEST_SCRIPT = $fetchScriptPath
    $installRoot = Join-Path $repo 'az-install'
    $zipEnv = Join-Path $repo 'zip.env'
    $zipOut = Join-Path $repo 'zip-out.txt'
    $zipErr = Join-Path $repo 'zip-err.txt'
    $zipArgs = '-NoProfile -ExecutionPolicy Bypass -File "{0}" -RepoRoot "{1}" -EnvFile "{2}" -InstallDirectory "{3}" -ZipPackage "{4}" -SkipUserPathUpdate' -f $runner, $repo, $zipEnv, $installRoot, $zipPath
    $zipProc = Start-Process -FilePath powershell.exe -ArgumentList $zipArgs -Wait -PassThru -RedirectStandardOutput $zipOut -RedirectStandardError $zipErr -WindowStyle Hidden
    $zipText = ''
    if (Test-Path $zipOut) { $zipText += [System.IO.File]::ReadAllText($zipOut) }
    if (Test-Path $zipErr) { $zipText += [System.IO.File]::ReadAllText($zipErr) }
    Write-Result ($zipProc.ExitCode -eq 0) 'per-user zip install fetches a token'
    Write-Result (Test-Path -LiteralPath (Join-Path $installRoot 'bin\az.cmd')) 'per-user zip install extracts az.cmd'
    Write-Result ((Test-Path -LiteralPath $zipEnv) -and -not $zipText.Contains($sentinel)) 'per-user zip install does not print the token'
    $wingetCmd = Join-Path $toolDir 'winget.cmd'
    @"
@echo off
echo %*> "%~dp0winget-args.txt"
copy /Y "%~dp0payload.cmd" "%~dp0az.cmd" >nul
exit /b 0
"@ | Set-Content -Encoding ASCII -Path $wingetCmd
    $wingetEnv = Join-Path $repo 'winget.env'
    $wingetOut = Join-Path $repo 'winget-out.txt'
    $wingetErr = Join-Path $repo 'winget-err.txt'
    $missingZip = Join-Path $repo 'missing-azure-cli.zip'
    $wingetArgs = '-NoProfile -ExecutionPolicy Bypass -File "{0}" -RepoRoot "{1}" -EnvFile "{2}" -InstallDirectory "{3}" -ZipPackage "{4}" -WingetExecutable "{5}" -SkipUserPathUpdate' -f $runner, $repo, $wingetEnv, $installRoot, $missingZip, $wingetCmd
    $wingetProc = Start-Process -FilePath powershell.exe -ArgumentList $wingetArgs -Wait -PassThru -RedirectStandardOutput $wingetOut -RedirectStandardError $wingetErr -WindowStyle Hidden
    $wingetText = ''
    if (Test-Path $wingetOut) { $wingetText += [System.IO.File]::ReadAllText($wingetOut) }
    if (Test-Path $wingetErr) { $wingetText += [System.IO.File]::ReadAllText($wingetErr) }
    $wingetArgText = ''
    $wingetArgFile = Join-Path $toolDir 'winget-args.txt'
    if (Test-Path $wingetArgFile) { $wingetArgText = [System.IO.File]::ReadAllText($wingetArgFile) }
    $scriptText = [System.IO.File]::ReadAllText($fetchScriptPath)
    Write-Result ($wingetProc.ExitCode -eq 0) 'winget fallback fetches a token after the zip is missing'
    Write-Result ($scriptText.Contains('needs an administrator')) 'winget fallback says the MSI needs an administrator'
    Write-Result ($scriptText.Contains('per-user ZIP')) 'zip install tells the user it is using the ZIP'
    Write-Result ($wingetArgText.Contains('Microsoft.AzureCLI') -and -not $wingetArgText.Contains('--scope')) 'winget fallback does not pass --scope user'

    $signedOut = Join-Path $toolDir 'signed-out-az.cmd'
    @'
@echo off
if /I "%~1"=="account" (
  if /I "%~2"=="show" (
    echo not signed in 1>&2
    exit /b 1
  )
  if /I "%~2"=="get-access-token" (
    echo {"accessToken":"unit-test-feed-token-sentinel","expires_on":1893456000}
    exit /b 0
  )
)
if /I "%~1"=="login" (
  echo device code 1>&2
  exit /b 0
)
exit /b 1
'@ | Set-Content -Encoding ASCII -Path $signedOut
    $signedOutEnv = Join-Path $repo 'signed-out.env'
    $signedOutOut = Join-Path $repo 'signed-out-out.txt'
    $signedOutErr = Join-Path $repo 'signed-out-err.txt'
    $signedOutArgs = '-NoProfile -ExecutionPolicy Bypass -File "{0}" -RepoRoot "{1}" -EnvFile "{2}" -AzExecutable "{3}" -SkipUserPathUpdate' -f $runner, $repo, $signedOutEnv, $signedOut
    $signedOutProc = Start-Process -FilePath powershell.exe -ArgumentList $signedOutArgs -Wait -PassThru -RedirectStandardOutput $signedOutOut -RedirectStandardError $signedOutErr -WindowStyle Hidden
    $signedOutText = ''
    if (Test-Path $signedOutOut) { $signedOutText += [IO.File]::ReadAllText($signedOutOut) }
    if (Test-Path $signedOutErr) { $signedOutText += [IO.File]::ReadAllText($signedOutErr) }
    Write-Result ($signedOutProc.ExitCode -eq 0 -and (Test-Path -LiteralPath $signedOutEnv)) 'signed-out az stderr still reaches device-code sign-in'
    Write-Result (-not $signedOutText.Contains($sentinel)) 'signed-out az run does not print the token'

    $staleAz = Join-Path $toolDir 'az.cmd'
    if (Test-Path -LiteralPath $staleAz) { Remove-Item -LiteralPath $staleAz -Force }
    $reuseRoot = Join-Path $repo 'reuse-cli'
    $reuseBin = Join-Path $reuseRoot 'bin'
    New-Item -ItemType Directory -Path $reuseBin | Out-Null
    Copy-Item -LiteralPath $payload -Destination (Join-Path $reuseBin 'az.cmd')
    $reuseEnv = Join-Path $repo 'reuse.env'
    $reuseOut = Join-Path $repo 'reuse-out.txt'
    $reuseErr = Join-Path $repo 'reuse-err.txt'
    $reuseArgs = '-NoProfile -ExecutionPolicy Bypass -File "{0}" -RepoRoot "{1}" -EnvFile "{2}" -InstallDirectory "{3}" -ZipUri "http://127.0.0.1:9/azure-cli.zip" -SkipUserPathUpdate' -f $runner, $repo, $reuseEnv, $reuseRoot
    $reuseProc = Start-Process -FilePath powershell.exe -ArgumentList $reuseArgs -Wait -PassThru -RedirectStandardOutput $reuseOut -RedirectStandardError $reuseErr -WindowStyle Hidden
    $reuseText = ''
    if (Test-Path $reuseOut) { $reuseText += [IO.File]::ReadAllText($reuseOut) }
    if (Test-Path $reuseErr) { $reuseText += [IO.File]::ReadAllText($reuseErr) }
    Write-Result ($reuseProc.ExitCode -eq 0 -and (Test-Path -LiteralPath $reuseEnv) -and (Test-Path -LiteralPath (Join-Path $reuseBin 'az.cmd')) -and -not $reuseText.Contains('Downloading the per-user ZIP')) 'existing per-user CLI is reused without downloading'
    Write-Result (-not $reuseText.Contains($sentinel)) 'reused per-user CLI does not print the token'
    Write-Result (-not $wingetText.Contains($sentinel)) 'winget fallback does not print the token'
    $userPathAfter = [Environment]::GetEnvironmentVariable('Path', 'User')
    if ($userPathBefore -ne $userPathAfter) {
        [Environment]::SetEnvironmentVariable('Path', $userPathBefore, 'User')
    }
    Write-Result ($userPathBefore -eq $userPathAfter) 'install tests do not change the user PATH'
    Remove-Item Env:LINK_CLOUD_TEST_PATH -ErrorAction SilentlyContinue
    Remove-Item Env:LINK_CLOUD_TEST_SCRIPT -ErrorAction SilentlyContinue

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
