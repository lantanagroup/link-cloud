# Runs docker compose from this repo. A shell AZURE_ARTIFACTS_PAT is passed through and
# Azure CLI is not called. Otherwise the token in .azure-artifacts.env is refreshed when
# it has under 10 minutes left, and a root .env value is used if that fetch fails.
# Every argument goes to docker compose unchanged, for example:
# ./Scripts/compose.ps1 build automation-ui
# AZURE_ARTIFACTS_PAT is set only for the docker process. The token is not printed.
# No profile setup is needed.
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'docker-compose.feed-token-profile.ps1')

$hooks = $global:LinkCloudFeedTokenHooks
if ($hooks -and $hooks.ContainsKey('RepoRoot') -and $hooks['RepoRoot']) {
    $composeRoot = [string]$hooks['RepoRoot']
} else {
    $composeRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
}

$global:LASTEXITCODE = 0
# Not piped: docker inherits this process's stdin directly. Enumerating $input under
# -File would block on an open stdin that never closes.
Invoke-LinkCloudCompose -RepoRoot $composeRoot -ComposeArguments @($args)
exit $global:LASTEXITCODE
