# Opt-in disk cleanup for this repository's docker compose project.
#
# Always, bound to this repository's compose file and project name
# (-f and -p, so COMPOSE_FILE and COMPOSE_PROJECT_NAME cannot redirect it):
#   docker compose down -v --remove-orphans
#   docker image prune of dangling images labeled for this compose project
#
# Only with -PruneBuildCache, or after an interactive yes:
#   docker builder prune --keep-storage 10GB
# The build cache is shared by every project on this Docker daemon.
#
# Does not run docker system prune, docker volume prune, or docker image prune -a.
# Does not remove named images, and does not remove the Aspire AppHost volumes
# (link-aspire-*). Those are reset with the command in DotNet/Link.AspireAppHost/README.md.
#
# Usage, from anywhere:
#   ./Scripts/docker-compose.disk-cleanup.ps1
#   ./Scripts/docker-compose.disk-cleanup.ps1 -PruneBuildCache
#   ./Scripts/docker-compose.disk-cleanup.ps1 -SkipBuildCache

[CmdletBinding()]
param(
    [switch]$PruneBuildCache,
    [switch]$SkipBuildCache
)

$ErrorActionPreference = 'Continue'

if ($PruneBuildCache -and $SkipBuildCache) {
    Write-Error 'Pass only one of -PruneBuildCache and -SkipBuildCache.'
    exit 2
}

$Root = Split-Path -Parent $PSScriptRoot
$ComposeFile = Join-Path $Root 'docker-compose.yml'
if (-not (Test-Path -LiteralPath $ComposeFile)) {
    Write-Error "docker-compose.yml was not found at $ComposeFile."
    exit 1
}

Set-Location -LiteralPath $Root

function Test-ComposeProjectName([string]$Name) {
    return $Name -match '^[a-z0-9][a-z0-9_-]*$'
}

# Plain function on purpose. An advanced function binds -v to -Verbose and drops it,
# so `down -v` would not remove volumes.
function Invoke-RepoCompose {
    & docker compose -f $ComposeFile -p $project @args
}

function Get-ComposeProjectName {
    # -f does not override COMPOSE_PROJECT_NAME, and COMPOSE_FILE can point compose
    # at another project. Read this repository's name with both unset, then pin it.
    $previousFile = $env:COMPOSE_FILE
    $previousProject = $env:COMPOSE_PROJECT_NAME
    try {
        Remove-Item Env:COMPOSE_FILE -ErrorAction SilentlyContinue
        Remove-Item Env:COMPOSE_PROJECT_NAME -ErrorAction SilentlyContinue
        $configJson = & docker compose -f $ComposeFile config --format json
    }
    finally {
        if ($null -eq $previousFile) {
            Remove-Item Env:COMPOSE_FILE -ErrorAction SilentlyContinue
        }
        else {
            $env:COMPOSE_FILE = $previousFile
        }

        if ($null -eq $previousProject) {
            Remove-Item Env:COMPOSE_PROJECT_NAME -ErrorAction SilentlyContinue
        }
        else {
            $env:COMPOSE_PROJECT_NAME = $previousProject
        }
    }

    if ($LASTEXITCODE -ne 0 -or -not $configJson) {
        return $null
    }
    try {
        $config = $configJson | ConvertFrom-Json
    }
    catch {
        return $null
    }
    $name = [string]$config.name
    if (Test-ComposeProjectName $name) {
        return $name
    }
    return $null
}

& docker info --format '{{.ServerVersion}}' | Out-Null
if ($LASTEXITCODE -ne 0) {
    Write-Error 'Docker is not running. Start Docker Desktop, then run this script again.'
    exit 1
}

$project = Get-ComposeProjectName
if (-not $project) {
    Write-Error 'Could not read this compose project name. Refusing to prune images without it.'
    exit 1
}

Write-Host "Compose project: $project"
Write-Host "Compose file: $ComposeFile"
Write-Host ''
Write-Host 'Containers:'
Invoke-RepoCompose ps --all
Write-Host ''
Write-Host 'Volumes labeled for this project:'
& docker volume ls --filter "label=com.docker.compose.project=$project"

Write-Host ''
Write-Host "docker compose -f $ComposeFile -p $project down -v --remove-orphans"
Invoke-RepoCompose down -v --remove-orphans
if ($LASTEXITCODE -ne 0) {
    Write-Error "docker compose down failed with exit code $LASTEXITCODE. Image and build-cache prune were not run."
    exit $LASTEXITCODE
}

Write-Host ''
Write-Host "docker image prune --force --filter dangling=true --filter label=com.docker.compose.project=$project"
& docker image prune --force --filter 'dangling=true' --filter "label=com.docker.compose.project=$project"
if ($LASTEXITCODE -ne 0) {
    Write-Error "docker image prune failed with exit code $LASTEXITCODE. Build-cache prune was not run."
    exit $LASTEXITCODE
}

$pruneCache = $false
if ($PruneBuildCache) {
    $pruneCache = $true
}
elseif (-not $SkipBuildCache) {
    $interactive = -not [Console]::IsInputRedirected
    if ($interactive) {
        Write-Host ''
        Write-Host 'The Docker build cache is shared by every project on this daemon.'
        $answer = Read-Host 'Prune it down to 10GB? [y/N]'
        if ($answer -match '^(y|yes)$') {
            $pruneCache = $true
        }
        else {
            Write-Host 'Leaving the build cache in place.'
        }
    }
    else {
        Write-Host 'Build cache left in place. Re-run with -PruneBuildCache to prune it to 10GB.'
    }
}

if ($pruneCache) {
    Write-Host ''
    Write-Host 'docker builder prune --force --keep-storage 10GB'
    & docker builder prune --force --keep-storage 10GB
    if ($LASTEXITCODE -ne 0) {
        Write-Error "docker builder prune failed with exit code $LASTEXITCODE."
        exit $LASTEXITCODE
    }
}

Write-Host ''
Write-Host 'Cleanup finished. Named images and other projects were left in place.'
exit 0
