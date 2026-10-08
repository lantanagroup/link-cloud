# Isolated-host proof for the Kafka operations console.
# Refuses to run unless KAFKA_PROOF_ALLOW=1.
# This workstation also requires KAFKA_PROOF_ALLOW_ON_THIS_PC=1, which is not set for day-to-day work.
# Stopping the project removes the extra broker and the volumes.
# A refusal writes to stderr and exits 2.

[CmdletBinding()]
param(
    [switch]$Publish,
    [switch]$Down
)

$ErrorActionPreference = "Stop"

function Exit-Refusal {
    param([string]$Message)
    [Console]::Error.WriteLine($Message)
    exit 2
}

if ($env:KAFKA_PROOF_ALLOW -ne "1") {
    Exit-Refusal "Refusing to start containers. Set KAFKA_PROOF_ALLOW=1 on the isolated host."
}

if ($env:COMPUTERNAME -eq "DESKTOP-5NA82VF" -and $env:KAFKA_PROOF_ALLOW_ON_THIS_PC -ne "1") {
    Exit-Refusal "This workstation is not the isolated Docker host. The kit stays stopped here."
}

$project = if ([string]::IsNullOrWhiteSpace($env:KAFKA_PROOF_COMPOSE_PROJECT)) { "kafka-ops-proof" } else { $env:KAFKA_PROOF_COMPOSE_PROJECT.Trim() }
if ($project -notmatch "^[A-Za-z0-9][A-Za-z0-9_.-]{0,79}$") {
    Exit-Refusal "The compose project name is invalid."
}
if ($project -match "linkui|scaffold|shared") {
    Exit-Refusal "Refusing a compose project name that could match the shared stack."
}

$here = $PSScriptRoot
$compose = Join-Path $here "compose.yml"
$publishFile = Join-Path $here "compose.publish.yml"
$repo = (Resolve-Path (Join-Path $here "..\..")).Path
$deadline = (Get-Date).AddMinutes(20)

function Assert-Time {
    if ((Get-Date) -gt $deadline) {
        throw "The proof run exceeded 20 minutes."
    }
}

function Get-ComposeFiles {
    $files = @("-f", $compose)
    if ($Publish) { $files += @("-f", $publishFile) }
    return $files
}

function Invoke-Compose {
    param([string[]]$ComposeArgs)
    Assert-Time
    $files = Get-ComposeFiles
    & docker compose -p $project @files @ComposeArgs
    if ($LASTEXITCODE -ne 0) {
        throw "docker compose exited $LASTEXITCODE"
    }
}

function Invoke-Broker {
    param([Parameter(ValueFromRemainingArguments = $true)][string[]]$BrokerArgs)
    Assert-Time
    $files = Get-ComposeFiles
    & docker compose -p $project @files exec -T broker-0 @BrokerArgs
    if ($LASTEXITCODE -ne 0) {
        throw "broker command exited $LASTEXITCODE"
    }
}

if ($Down) {
    & docker compose -p $project -f $compose -f $publishFile --profile extra-broker down -v --remove-orphans --timeout 30
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    $left = @( & docker volume ls -q --filter "label=com.docker.compose.project=$project" | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } )
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    if ($left.Count -gt 0) {
        & docker volume rm @left
        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    }
    Write-Output "Stopped project $project and removed its volumes, including volumes left by scaled consumers and the extra broker."
    exit 0
}

if ($Publish) {
    $portText = if ([string]::IsNullOrWhiteSpace($env:KAFKA_PROOF_PORT)) { "19094" } else { $env:KAFKA_PROOF_PORT.Trim() }
    $portNumber = 0
    if (-not [int]::TryParse($portText, [ref]$portNumber) -or $portNumber -lt 1 -or $portNumber -gt 65535) {
        Exit-Refusal "KAFKA_PROOF_PORT is not a valid port."
    }
    if ($portNumber -ge 5280 -and $portNumber -le 5294) {
        Exit-Refusal "Port $portNumber is reserved. Pick a host port outside 5280-5294."
    }
    if ($portNumber -ne 19094) {
        Exit-Refusal "KAFKA_PROOF_PORT must stay 19094. Broker 0 advertises localhost:19094, and a different port would recreate the controller."
    }
    $env:KAFKA_PROOF_PORT = "19094"
    $env:KAFKA_BOOTSTRAP = "localhost:19094"
}

Write-Output "STEP unit-tests"
$dotnet = Get-Command dotnet -ErrorAction SilentlyContinue
if ($null -eq $dotnet) {
    Write-Output "SKIP unit-tests (dotnet is not on PATH)"
}
else {
    & dotnet test (Join-Path $repo "DotNet\KafkaOps.Proof\KafkaOps.Proof.csproj") --filter "Category=UnitTests" --nologo -v q
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    Write-Output "PASS unit-tests"
}

Write-Output "STEP compose-up"
if ($Publish) {
    Invoke-Compose @("up", "-d", "--no-recreate")
}
else {
    Invoke-Compose @("up", "-d")
}

Write-Output "STEP overview"
$created = $false
for ($i = 0; $i -lt 40; $i++) {
    Assert-Time
    $files = Get-ComposeFiles
    & docker compose -p $project @files exec -T broker-0 /opt/kafka/bin/kafka-topics.sh --bootstrap-server broker-0:9092 --create --if-not-exists --topic ops-proof-log --partitions 3 --replication-factor 3
    if ($LASTEXITCODE -eq 0) { $created = $true; break }
    Start-Sleep -Seconds 3
}
if (-not $created) { throw "ops-proof-log was not created." }

$brokers = & docker compose -p $project @(Get-ComposeFiles) exec -T broker-0 /opt/kafka/bin/kafka-broker-api-versions.sh --bootstrap-server broker-0:9092
if ($LASTEXITCODE -ne 0) { throw "Broker overview failed." }
$brokerText = ($brokers | Out-String)
foreach ($id in 0, 1, 2) {
    if ($brokerText -notmatch "\(id:\s*$id\s") { throw "Broker $id is missing from the cluster overview." }
}
Write-Output "PASS overview"

Write-Output "STEP replicas"
Invoke-Compose @("up", "-d", "--scale", "consumer=3", "--no-recreate", "consumer")
$stable = $false
for ($i = 0; $i -lt 40; $i++) {
    Assert-Time
    $state = & docker compose -p $project @(Get-ComposeFiles) exec -T broker-0 /opt/kafka/bin/kafka-consumer-groups.sh --bootstrap-server broker-0:9092 --describe --group ops-proof-consumers --state 2>&1 | Out-String
    if ($state -match "ops-proof-consumers\s+\S.*\s+Stable\s+3\s*$") { $stable = $true; break }
    Start-Sleep -Seconds 3
}
if (-not $stable) { throw "ops-proof-consumers did not become Stable with 3 members." }
Write-Output "PASS replicas"

Write-Output "STEP add-broker"
if ($Publish) {
    Invoke-Compose @("--profile", "extra-broker", "up", "-d", "--no-recreate", "broker-3", "host-proxy-3")
}
else {
    Invoke-Compose @("--profile", "extra-broker", "up", "-d", "broker-3")
}
$joined = $false
for ($i = 0; $i -lt 40; $i++) {
    Assert-Time
    $listed = & docker compose -p $project @(Get-ComposeFiles) exec -T broker-0 /opt/kafka/bin/kafka-broker-api-versions.sh --bootstrap-server broker-0:9092 2>&1 | Out-String
    if ($listed -match "\(id:\s*3\s") { $joined = $true; break }
    Start-Sleep -Seconds 3
}
if (-not $joined) { throw "Broker 3 did not register." }
Write-Output "PASS add-broker"

function Invoke-Reassign {
    param([string]$Json)
    $files = Get-ComposeFiles
    $Json | & docker compose -p $project @files exec -T broker-0 bash -lc "cat > /tmp/link-ops-reassign.json && /opt/kafka/bin/kafka-reassign-partitions.sh --bootstrap-server broker-0:9092 --reassignment-json-file /tmp/link-ops-reassign.json --execute"
    if ($LASTEXITCODE -ne 0) { throw "Reassignment execute failed." }
    for ($i = 0; $i -lt 40; $i++) {
        Assert-Time
        $verify = & docker compose -p $project @files exec -T broker-0 /opt/kafka/bin/kafka-reassign-partitions.sh --bootstrap-server broker-0:9092 --reassignment-json-file /tmp/link-ops-reassign.json --verify 2>&1 | Out-String
        if ($verify -match "completed" -and $verify -notmatch "in progress" -and $verify -notmatch "failed") { return }
        Start-Sleep -Seconds 3
    }
    throw "Reassignment did not complete."
}

Write-Output "STEP reassignment"
$onto = '{"version":1,"partitions":[{"topic":"ops-proof-log","partition":0,"replicas":[3,1,2]},{"topic":"ops-proof-log","partition":1,"replicas":[0,3,2]},{"topic":"ops-proof-log","partition":2,"replicas":[0,1,3]}]}'
Invoke-Reassign $onto
$off = '{"version":1,"partitions":[{"topic":"ops-proof-log","partition":0,"replicas":[0,1,2]},{"topic":"ops-proof-log","partition":1,"replicas":[0,1,2]},{"topic":"ops-proof-log","partition":2,"replicas":[0,1,2]}]}'
Invoke-Reassign $off

$described = & docker compose -p $project @(Get-ComposeFiles) exec -T broker-0 /opt/kafka/bin/kafka-topics.sh --bootstrap-server broker-0:9092 --describe --topic ops-proof-log
if ($LASTEXITCODE -ne 0) { throw "Topic describe failed." }
$replicaLines = @($described | Where-Object { $_ -match "Replicas:" })
if ($replicaLines.Count -lt 1) { throw "Topic describe did not list replicas." }
foreach ($line in $replicaLines) {
    if ($line -match "Replicas:\s*([0-9,]+)") {
        $ids = $Matches[1].Split(",")
        if ($ids -contains "3") { throw "Broker 3 still holds a replica: $line" }
    }
}
$under = & docker compose -p $project @(Get-ComposeFiles) exec -T broker-0 /opt/kafka/bin/kafka-topics.sh --bootstrap-server broker-0:9092 --describe --topic ops-proof-log --under-replicated-partitions 2>&1 | Out-String
if ($under.Trim().Length -gt 0) { throw "Under-replicated partitions remain: $under" }
Write-Output "PASS decommission-empty"

if ($null -eq $dotnet) {
    Write-Output "SKIP controller-roles (dotnet is not on PATH)"
}
elseif ([string]::IsNullOrWhiteSpace($env:KAFKA_BOOTSTRAP)) {
    Write-Output "SKIP controller-roles (KAFKA_BOOTSTRAP is not set; pass -Publish)"
}
else {
    Write-Output "STEP controller-roles"
    & dotnet test (Join-Path $repo "DotNet\KafkaOps.Proof\KafkaOps.Proof.csproj") --filter "FullyQualifiedName~KitCluster_KnowsControllerRoles_AndAllowsBroker3" --nologo -v q
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    Write-Output "PASS controller-roles"
}

Write-Output "STEP stop-broker"
Invoke-Compose @("stop", "broker-3")
Write-Output "PASS decommission-stop"

if ($null -eq $dotnet) {
    Write-Output "SKIP console-flow (dotnet is not on PATH)"
}
elseif ([string]::IsNullOrWhiteSpace($env:KAFKA_BOOTSTRAP)) {
    Write-Output "SKIP console-flow (KAFKA_BOOTSTRAP is not set; pass -Publish)"
}
else {
    Write-Output "STEP console-flow"
    & dotnet test (Join-Path $repo "DotNet\KafkaOps.Proof\KafkaOps.Proof.csproj") --filter "FullyQualifiedName~TwoPeopleApproveAndExecute_AndThreeGroupsAreListed" --nologo -v q
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    Write-Output "PASS console-flow"
}

Write-Output "PASS proof"
Write-Output "Inspect the project with: docker compose -p $project -f $compose ps"
Write-Output "Stop it with: $PSCommandPath -Down"
Write-Output "That stop removes the extra broker and the volumes."
