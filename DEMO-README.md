# Oct 15 demo: Kafka console changes that take effect

Branch `nm/demo-oct15` (demo only, do not merge). The Kafka console talks to its own 3-broker Kafka (KRaft, apache/kafka 3.7.1, broker-3 on the `extra-broker` profile). It uses a containerized Admin BFF with the Docker socket mounted, the Docker CLI installed, `KafkaOps:InfraProvider=LocalCompose` and `KafkaOps:RequireDifferentApprover=false`. So every change submitted in the UI is approved and executed by you and really runs: add broker, rebalance, decommission, partitions, replication, migrate and produce.

The Link pipeline (dev stack `wt-link-ui-scaffold`, single broker `kafka_b`) is not touched. Reports keep running there. The demo cluster only holds the console's topics (`Service-Healthcheck`, `ops-proof-log`, `AuditableEventOccurred`).

## Start (about 1 min after the first build)
```powershell
cd C:\repos\wt-link-demo-oct15\demo
docker compose up -d --build
# Link.UI pointed at the demo BFF (the Link.UI Docker build needs the private NuGet feed, so it runs on the host):
cd C:\repos\wt-link-demo-oct15\DotNet\Link.UI
$env:ASPNETCORE_ENVIRONMENT='Development'; $env:ServiceRegistry__AdminBffServiceUrl='http://127.0.0.1:8163'
dotnet run -c Release --urls http://127.0.0.1:5290
```
Open http://127.0.0.1:5290/Operations/Kafka.

## Demo flow
1. Brokers ? Add broker ? hold ? Execute. broker-3 starts and registers.
2. Brokers ? 3 ? Rebalance ? hold ? Approve ? Execute.
3. Brokers ? 3 ? Decommission ? hold ? Approve ? Execute. Partitions move back to 0-2 and broker-3 stops.
4. Topics ? Service-Healthcheck ? Produce ? hold.

## Stop / reset
```powershell
cd C:\repos\wt-link-demo-oct15\demo
docker compose --profile extra-broker down -v
```
Stop the Link.UI with Ctrl+C.

## Memory / switch-over
The demo adds about 2.5 GB, and the dev stack uses about 13 GB of Docker's 18 GB. It ran alongside on 12 Oct, but one earlier run OOM-killed `link-validation`. If Docker is short of memory on the day:
```powershell
cd C:\repos\wt-link-ui-scaffold; docker compose stop          # dev data stays in its volumes
cd C:\repos\wt-link-demo-oct15\demo; docker compose up -d --build
# afterwards
docker compose --profile extra-broker down -v
cd C:\repos\wt-link-ui-scaffold; docker compose start
```
Instances A (5280) and B (5281) run from `wt-link-ui-scaffold\DotNet\Link.UI\bin\Debug` and are not affected.
