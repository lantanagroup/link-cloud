# Kafka operations

Link.UI exposes the Kafka console under **Operations → Throughput and scaling → Kafka**.
Admin.BFF owns the broker calls at `/api/ops/kafka`. Link.UI does not open a broker connection.

The console has four panels. Search, filter, sort, and page stay in the query string. Times are the viewer's local time, with UTC on hover. Test groups named `e2e-diag-*` and `Dynamic:*` stay hidden unless the page asks for them.

- **Overview.** Controller, broker count, under-replicated partitions, offline partitions, and ISR shrinks. Topics show partitions, replication factor, key class, produce rate, and total lag. Lag is also listed per group. The panel refreshes in place.
- **Topic.** Leaders, replicas, in-sync replicas, topic config, and lag per partition per group. **Add partitions** sits behind an Advanced disclosure. The change is permanent: Kafka does not shrink a topic, and existing keys are remapped. The operator types the topic name, sees the same dry run an approver sees, and submits a reason. The main topic, its retry topic, and its error topic increase together. A retry or error topic that is missing, or that would shrink, refuses the change.
- **Consumers.** Group state, members (client id, host, assignment), lag per partition, and total lag. The broker description does not include a time since the last rebalance. The primary actions are add or remove replicas. A replica count above the partition count is idle and is refused. The page then waits until the group is Stable, the member count matches, and every partition is assigned.
- **Brokers.** State, rack, partition count, leader count. Log-directory size is not available through the client this service uses, so the column says so. Adding a broker waits until it is registered. A follow-up rebalance is a separate request. Decommission is guided: preview the moves, refuse a move that would leave a topic with fewer brokers than its replication factor or `min.insync.replicas`, move replicas off, wait until the broker holds no replicas and no leaders and the cluster has no under-replicated or offline partitions, and only then stop the broker. An in-flight reassignment can be cancelled. Cancelling does not stop the broker.

`IsLinkAdmin` does not grant these actions.

| Action | Permission |
|---|---|
| Read the four panels, dry runs, and the rights probe | `CanViewInfrastructure` |
| Create a partition increase | `CanManageKafkaTopics` |
| Create a replica, broker, or rebalance change | `CanManageScaling` |
| Approve, reject, execute, or cancel | `CanOperateKafka` (either manage permission) plus the permission that matches the request kind |

## What the service refuses

The UI displays these rules. Admin.BFF enforces them.

1. The new partition count must be higher than the current count and no higher than `KafkaOps:MaxPartitionsPerTopic` (default 24).
2. The main topic, the retry topic, and the error topic are raised together. None of them can shrink. Retry and error topics are not increased on their own.
3. Execute waits until every member of every subscribed group advertises the expected config version on `client.id` (`<service>-<host>-c<version>`, version 1). An empty group does not block. A member that does not advertise the version does. Consumer clients are unchanged by this console, so a group that still has members refuses an increase until those clients advertise the version.
4. Key class comes from the topic catalog. Unknown topics are treated as facility-keyed. Facility keys are `{facilityId}`, patient keys are `{facilityId}:{patientId}`, and report keys are `{facilityId}:{reportScheduleId}`. Those topics keep per-key order, so an increase waits for a quiet window: zero lag in every subscribed group and no produce traffic for at least twice the metadata refresh, checked again immediately before execute. The dry run for a facility-keyed topic states that one facility can never use more than one partition. Skipping the quiet window needs a second approver and a reason. Log, correlation, and health topics may change while traffic is moving. `DataAcquisitionRequested` stays blocked: .NET producers hash the facility key with CRC32 and MeasureEval hashes it with murmur2, so one facility can land on different partitions.
5. The new partition count is the replica ceiling (`maxReplicas` stays at or below the partition count). The consumers panel shows that ceiling and refuses a scale past it.
6. After the increase, the request stays open until every subscribed group with members has every partition assigned. If that takes longer than three metadata-refresh intervals, the request records a slow-convergence audit.
7. A verification window (default 15 minutes, `KafkaOps:VerificationWindowSeconds`) then checks that lag did not rise above the baseline and that a partition whose high watermark moved also moved its committed offset. The request closes as done or needs attention.
8. One change is in flight per environment. A topic family also waits `KafkaOps:RateLimitMinutes` (default 30) between partition increases.

The requester cannot approve their own request when a second approver is required. Order-sensitive topics, quiet-window overrides, decommission, and rebalance always need a different person. Replica and add-broker changes need a second approver only when `KafkaOps:RequireSecondApprover` is true. Production is read-only unless `KafkaOps:ReadOnly` is set to false. A missing value means read-only in Production and writable elsewhere.

Pending requests are stored through `ICacheService` for 24 hours. Each request, approval, rejection, execution, and convergence result is also produced to `AuditableEventOccurred`.

## Broker and replica changes

Describe, the rights probe, and partition increases use one Admin client in Admin.BFF. Replica moves and process scaling go through `IKafkaInfraProvider`.

| Provider | When it runs |
|---|---|
| Disabled | Default. The buttons explain that broker and replica changes stay off until they are enabled. |
| Strimzi | Patches a KafkaNodePool and a Deployment or scaled object, and submits a KafkaRebalance (`add-brokers` or `remove-brokers`). It stays inert until the cluster client is configured. |
| LocalCompose | Dev proof only. `docker compose` scale, start, and stop for one named project, and `kafka-reassign-partitions.sh` for the replica plan. Refused when the host environment is Production or the project name is empty. |

The .NET client in use does not expose `AlterPartitionReassignments`, `ListPartitionReassignments`, or `DescribeLogDirs`. The local provider applies the replica plan by running `kafka-reassign-partitions.sh` inside the broker container. Emptiness is the topic metadata: no replicas and no leaders on that broker, and no under-replicated or offline partitions. Log-directory bytes stay unknown. A throttle is optional (`KafkaOps:ReassignmentThrottleBytesPerSecond`).

`GET /api/ops/kafka/capabilities` is the rights probe. For each catalog topic it reports `DescribeTopics` with authorized operations and a validate-only `CreatePartitions`. Run that probe in each deployed environment before the first real increase. This runbook does not call a deployed host. Proceed only when the probe shows the principal can alter partitions, the topic operator is not reconciling those topics back down, and the replication factor is acceptable.

## Local proof

Use a broker you can destroy. Do not point this kit at a shared cluster.

The kit is `Scripts/kafka-ops-proof/`. `compose.yml` is a three-broker KRaft cluster plus a fourth broker on the `extra-broker` profile, a producer, and a consumer service named `consumer` in group `ops-proof-consumers` on topic `ops-proof-log`. The compose file publishes no host ports and sets no container name. The runner refuses to start unless `KAFKA_PROOF_ALLOW=1`. On the day-to-day workstation it also refuses unless `KAFKA_PROOF_ALLOW_ON_THIS_PC=1`. Stopping the project does not delete volumes.

```
$env:KAFKA_PROOF_ALLOW = '1'
./Scripts/kafka-ops-proof/run-proof.ps1
./Scripts/kafka-ops-proof/run-proof.ps1 -Publish
./Scripts/kafka-ops-proof/run-proof.ps1 -Down
```

The shell equivalent is `run-proof.sh`, `run-proof.sh --publish`, and `run-proof.sh --down`. `-Publish` binds broker 0 on `KAFKA_PROOF_PORT` (default 19094, never 5280-5294) and sets `KAFKA_BOOTSTRAP` so the integration test can read cluster metadata. Without that variable the integration test returns immediately and does not prove the broker path. The runner's own checks do.

The runner:

1. Runs the Kafka unit tests when `dotnet` is on PATH. Those tests are the partition-add guard: an order-sensitive topic with lag is refused, a drained order-sensitive topic is allowed, a log topic does not need the quiet window, and `DataAcquisitionRequested` stays blocked. They also cover the replica ceiling and the decommission planner.
2. Reads the cluster and requires brokers 0, 1, and 2.
3. Scales the proof consumer to 3 and waits until the group is Stable with 3 members.
4. Starts broker 3 and waits until it is registered.
5. Moves a replica of each `ops-proof-log` partition onto broker 3, moves those replicas back to brokers 0, 1, and 2, checks that broker 3 is empty, and checks that the topic has no under-replicated partitions.
6. Stops broker 3 only after that empty check.

Screenshots of the four panels can be taken with `LinkUi:KafkaOpsFixture=true` on a local Link.UI process. That flag is not in the committed settings. It serves a recorded Admin.BFF response, including a disabled provider, so the pages render without a broker.
