# Local Kafka console demo on DESKTOP-5NA82VF: the 3-broker kafka-ops-proof cluster (plus broker-3 on demand),
# a host-run Admin.BFF with Docker access on 127.0.0.1:8163 (KafkaOps:InfraProvider=LocalCompose) and a Link.UI on 127.0.0.1:5290.
# Instances A (5280) and B (5281) and the dev compose stack are not touched.
#   .\run-demo-instance.ps1        start (publishes to %LOCALAPPDATA%\kafka-ops-proof-run)
#   .\run-demo-instance.ps1 -Down  stop the instance and remove the proof cluster and its volumes
[CmdletBinding()] param([switch]$Down, [switch]$NoBuild)
$ErrorActionPreference = "Stop"
$here = $PSScriptRoot; $repo = (Resolve-Path (Join-Path $here "..\..")).Path
$run = Join-Path $env:LOCALAPPDATA "kafka-ops-proof-run"; $project = "kafka-ops-proof"
$files = @("-f", (Join-Path $here "compose.yml"), "-f", (Join-Path $here "compose.publish.yml"))
function Stop-Port([int]$port) { Get-NetTCPConnection -State Listen -LocalPort $port -ErrorAction SilentlyContinue | ForEach-Object { Stop-Process -Id $_.OwningProcess -Force -ErrorAction SilentlyContinue } }
Stop-Port 5290; Stop-Port 8163
if ($Down) { & docker compose -p $project @files --profile extra-broker down -v --remove-orphans --timeout 30; exit $LASTEXITCODE }
$env:KAFKA_PROOF_ALLOW_ON_THIS_PC = "1"
& docker compose -p $project @files up -d; if ($LASTEXITCODE -ne 0) { throw "compose up failed" }
$deadline = (Get-Date).AddMinutes(3)
do { Start-Sleep 3; & docker compose -p $project @files exec -T broker-0 /opt/kafka/bin/kafka-topics.sh --bootstrap-server broker-0:9092 --list *> $null } until ($LASTEXITCODE -eq 0 -or (Get-Date) -gt $deadline)
# The audit producer blocks the request path for ~60 s when its topic is missing (longer than Link.UI's 30 s timeout).
& docker compose -p $project @files exec -T broker-0 /opt/kafka/bin/kafka-topics.sh --bootstrap-server broker-0:9092 --create --if-not-exists --topic AuditableEventOccurred --partitions 3 --replication-factor 3
if (-not $NoBuild) {
    & dotnet publish (Join-Path $repo "DotNet\Admin.BFF\Admin.BFF.csproj") -c Release -o (Join-Path $run "bff") -v q; if ($LASTEXITCODE -ne 0) { throw "BFF publish failed" }
    & dotnet publish (Join-Path $repo "DotNet\Link.UI\Link.UI.csproj") -c Release -o (Join-Path $run "ui") -v q; if ($LASTEXITCODE -ne 0) { throw "UI publish failed" }
}
$compose = (Join-Path $here "compose.yml") + "|" + (Join-Path $here "compose.publish.yml")
$bff = @{
    ASPNETCORE_ENVIRONMENT = "Development"; ASPNETCORE_URLS = "http://127.0.0.1:8163"; SecretManagement__Manager = "Local"; Cache__Type = "InMemory"
    Telemetry__EnableTelemetry = "false"; Telemetry__EnableOtelCollector = "false"; Authentication__EnableAnonymousAccess = "true"
    Authentication__Schemas__Jwt__Enabled = "false"; Authentication__Schemas__OpenIdConnect__Enabled = "false"; Authentication__Schemas__Oauth2__Enabled = "false"; Authentication__DefaultChallengeScheme = "link_cookie"
    KafkaConnection__BootstrapServers__0 = "localhost:19094"; KafkaConnection__SaslProtocolEnabled = "false"
    KafkaOps__Connection__BootstrapServers = "localhost:19094"; KafkaOps__Connection__SaslProtocolEnabled = "false"
    KafkaOps__InfraProvider = "LocalCompose"; KafkaOps__ComposeProject = $project; KafkaOps__ComposeFile = $compose
    KafkaOps__BrokerService = "broker-0"; KafkaOps__BrokerServicePrefix = "broker-"; KafkaOps__ExtraBrokerProfile = "extra-broker"; KafkaOps__ExtraBrokerService = "broker-3,host-proxy-3"
    KafkaOps__KafkaToolsPath = "/opt/kafka/bin"; KafkaOps__BrokerInternalPort = "9092"; KafkaOps__ConsumerDeployments = "ops-proof-consumers=consumer"; KafkaOps__AllowTopicMigration = "true"; KafkaOps__RequireDifferentApprover = "false"
}
$key = (& docker inspect link-admin-bff --format '{{range .Config.Env}}{{println .}}{{end}}' 2>$null | Where-Object { $_ -like "LinkTokenService__SigningKey=*" }) -replace '^[^=]+=', ''
if ($key) { $bff.LinkTokenService__SigningKey = $key }
function Start-Hosted([string]$dir, [string]$dll, [hashtable]$vars, [string]$log, [string[]]$extra) {
    $psi = New-Object System.Diagnostics.ProcessStartInfo "dotnet"; $psi.WorkingDirectory = $dir; $psi.UseShellExecute = $false; $psi.CreateNoWindow = $true
    $psi.Arguments = (@($dll) + $extra) -join " "; foreach ($k in $vars.Keys) { $psi.Environment[$k] = $vars[$k] }
    $psi.RedirectStandardOutput = $true; $psi.RedirectStandardError = $true
    $p = [System.Diagnostics.Process]::Start($psi); $w = [System.IO.StreamWriter]::new($log); $w.AutoFlush = $true
    Register-ObjectEvent $p OutputDataReceived -MessageData $w -Action { if ($EventArgs.Data) { $Event.MessageData.WriteLine($EventArgs.Data) } } | Out-Null
    Register-ObjectEvent $p ErrorDataReceived -MessageData $w -Action { if ($EventArgs.Data) { $Event.MessageData.WriteLine($EventArgs.Data) } } | Out-Null
    $p.BeginOutputReadLine(); $p.BeginErrorReadLine(); return $p
}
$null = Start-Hosted (Join-Path $run "bff") "Admin.BFF.dll" $bff (Join-Path $run "bff.log") @()
$null = Start-Hosted (Join-Path $run "ui") "Link.UI.dll" @{ ASPNETCORE_ENVIRONMENT = "Development"; ServiceRegistry__AdminBffServiceUrl = "http://127.0.0.1:8163" } (Join-Path $run "ui.log") @("--urls", "http://127.0.0.1:5290")
Write-Host "Demo console: http://127.0.0.1:5290/Operations/Kafka (BFF http://127.0.0.1:8163). Keep this window open; stop with -Down."
Wait-Event
