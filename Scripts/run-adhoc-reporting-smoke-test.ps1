param(
    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]] $DotnetTestArgs
)

$ErrorActionPreference = 'Stop'

# Runs the Adhoc Reporting smoke test:
#   LantanaGroup.Link.Tests.E2ETests.AdhocReportTest.ExecuteAdhocReportTest

$root = (git rev-parse --show-toplevel) 2>$null
if (-not $root) { $root = (Get-Location).Path }
Set-Location $root

$argsList = @(
    'test',
    'Tests/BackendE2ETests/BackendE2ETests.csproj',
    '--filter',
    'FullyQualifiedName~AdhocReportTest.ExecuteAdhocReportTest'
) + $DotnetTestArgs

& dotnet @argsList
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
