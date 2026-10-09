# Link Kafka Topic Migration: Implementation Design

**Author:** Nick Montalto
**Date:** October 8, 2026
**Status:** Design for Build. One item needs the architect's sign-off (section 13).
**Code baseline (read-only):**
- Console and services: `origin/nm/link-ui-kafka` at `208c66c3` ("Accept a listed partition that finishes during a cancel."). It is `b1a3d9c3e` plus one cancel-race fix, and it already contains the composite-key change (PR #1984, `9e228bd96`). Every `file:line` below is at `208c66c3` unless it says otherwise.
- `origin/nm/link-ui-scaffold` is at `e5481719`. It does not have the `b1a3`/`208c` console yet. The topic catalog, `topics.txt` and the sync pipeline are the same on both.
- `origin/dev` is at `87a83389`. It is only used for the sync-pipeline comparison in 2.5.

**Related:** `Link-Scaling-Implementation-Plan.md` (Workstream A), `Link-Kafka-Key-Inventory.md`, `docs/kafka-partition-operations.md`, and the console proof report on the box (`kafka-console-proof/proof-report.md`).

---

## 1. Summary

**The feature:** repartition topic T from N to M partitions with the temp-topic method, from the Link.UI Kafka console, with plan, approval, and execution steps that can be resumed.

**Chosen method: drain, back up, then recreate under the same name.**
1. Stop T's producers, let every consumer group drain T to zero lag, and then stop T's consumers.
2. Copy T into a new temp topic with M partitions. The copy re-hashes every record by key with the same murmur2 partitioner the producers use, so per-key order is kept.
3. Verify the copy. Wait for a typed go-ahead from a person.
4. Delete T and recreate T with M partitions and T's exact configs. The recreated topic starts empty. Write offset 0 on every partition for each discovered group.
5. Start the consumers, then the producers.

The temp topic stays as a verified backup for replay. It is kept until a person confirms cleanup or `KafkaOps:MigrationBackupRetentionHours` expires. It is never deleted on failure, abort, timeout, or rollback. Rollback leaves it and sets `BackupCleanupRequired`. Traffic never moves to it. The backup is not copied onto the recreated topic.

**Why this method:**
- **No service can switch topic names at runtime.**
  - .NET hard-codes names through `nameof(KafkaTopic.X)` at about 50 produce sites and 20 subscribe sites.
  - Java uses compile-time constants in `@KafkaListener` annotations.
  - The retry, redrive, and error names are derived from the main name.
  - Moving traffic to a temp or versioned topic would therefore mean a coordinated redeploy of every producer and consumer, twice per migration.
- **Keeping the name keeps everything that depends on it working unchanged:** `topics.txt`, `kafka-retry-services.txt`, the sync pipeline, the topic catalog, dashboards, and name-based ACLs (Kafka ACLs survive a delete and recreate).
- **Drain to zero lag gives a strict ordering guarantee.** Every old message is processed before any new message is produced. That is stronger than per-key order, and it means no history has to be replayed into consumers, so there are **zero duplicates by construction**.
- **The stop window is required anyway.** .NET listeners stop for good when their topic disappears (section 2.7), and Kafka only accepts admin offset writes for a group with no active members.

**First Build slice:**
- Increases only, on 16 eligible topics. Topics whose producers include Tenant or Account wait for slice 2.
- LocalCompose and Strimzi providers. Strimzi can run in a manual-checklist mode.
- A mandatory verified backup, a hold point before the delete, and full rollback before that point.
- A durable record with an executor lease.
- The topic and partition controls described in section 8.

---

## 2. What the code says

### 2.1 How topic names are resolved

| Runtime | How the name is produced | Runtime change without redeploy? |
|---|---|---|
| .NET producers | `nameof(KafkaTopic.X)` or `KafkaTopic.X.ToString()` passed per call. Examples: `Census/Application/Services/EventProducerService.cs:62`, `Report/KafkaProducers/SubmitPayLoadProducer.cs:43`, `Normalization/Listeners/ResourcesAcquiredListener.cs:572`. The enum is `DotNet/Shared/Application/Models/KafkaTopic.cs:5-72`. | No |
| .NET consumers | `consumer.Subscribe(KafkaTopicNames.Subscription(nameof(KafkaTopic.X), service))`. Examples: `Shared/Application/BaseListener.cs:73`, `Report/Listeners/PatientEventListener.cs:75`, `Audit/Listeners/AuditEventListener.cs:59`. `Subscription` returns `[main, main-Redrive-{service}]` (`Shared/Application/Models/Kafka/KafkaTopicNames.cs:53-56`). | No |
| .NET retry, redrive, error | Derived from the main name: `{T}-Retry-{svc}`, `{T}-Redrive-{svc}`, `{T}-Error` (`KafkaTopicNames.cs:9-32`). The retry handler rewrites the legacy `{T}-Retry` to the per-service form (`Shared/Application/Error/Handlers/TransientExceptionHandler.cs`, `Topic` setter; `Shared/Application/Listeners/RetryListener.cs:199-211`). | No |
| Java (measureeval, validation) | String constants in `Java/shared/src/main/java/com/lantanagroup/link/shared/kafka/Topics.java:3-18`, used in annotations: `ResourcesNormalizedConsumer.java:49`, `EvaluationRequestedConsumer.java:73`, `ReadyForValidationConsumer.java:81`. Retry and DLT names use the fixed suffixes `-Retry` and `-Error` (`measureeval/.../configs/KafkaConfig.java:252-258`, `validation/.../configs/KafkaConfig.java:222-228`). | No (annotation constants) |
| Admin.BFF integration commands, Link.UI automation | `nameof(KafkaTopic.X)`: `Admin.BFF/Application/Commands/Integration/*/*.cs` (for example `CreatePatientEvent.cs:48`) and `Automation.Link/Services/ReportApiHelper.cs:151`. Link.UI hosts Automation.Link (`Link.UI/Link.UI.csproj:25`). | No |

**Conclusion:** a topic name is a compile-time contract in both runtimes. Only partition count and topic configs can change at run time.

### 2.2 How consumer group ids are built

- **.NET main groups.** The group id is the service name constant. Examples:
  - `Census/Listeners/PatientListsAcquiredListener.cs:65` (`Census`)
  - `QueryDispatch/Listeners/PatientEventListener.cs:69` (`QueryDispatch`)
  - `Submission/Listeners/SubmitPayloadListener.cs:58`
  - `Audit/Listeners/AuditEventListener.cs:50`
  - `DataAcquisition/Listeners/DataAcquisitionRequestedListener.cs:91`

  Report, Normalization, and the DA worker use `ServiceInformation.ServiceConfigName`. That value is set from the constant the service passes at startup (`Shared/Application/Extensions/ExternalConfigurationExtension.cs:108`; `Report/Program.cs:76-78`). One group per service covers all of its listeners (Report's group covers 7 topics).
- **.NET retry groups.** `{service}-retry` (`RetryListener.cs:66-69`).
- **Java.** The group comes from configuration:
  - measureeval uses `spring.kafka.consumer.group-id: ${spring.application.name}`, which is `measureeval` (`measureeval/src/main/resources/application.yml:84`). The docker profile uses `measureeval-events` (`application-docker.yml:25`).
  - validation uses `validation` (`validation/src/main/resources/application.yml:53`).
  - The Spring retry-topic containers run in the same application. Their exact group name isn't pinned in code, so the console discovers it from the broker (4.3).
- **Group ids change only with a redeploy.** Renaming a group loses its offsets on every topic the group reads, so the migration keeps the existing group ids.

### 2.3 Producers, consumers, and the stop set per topic

"Stop set" means the workloads that must be stopped during the window. Admin.BFF and Link.UI are never stopped. They hold T in-process instead (5.3).

| Topic | Key (after composite keys) | Producers (file:line) | Consumer groups (file:line) | Stop set | Slice 1 |
|---|---|---|---|---|---|
| PatientCensusScheduled | facility JSON | Census `SchedulePatientListRetrieval.cs:40` | DataAcquisition (`PatientCensusScheduledListener.cs:66`) | Census, DataAcquisition | Yes |
| PatientListsAcquired | facility JSON | DataAcquisition `PatientCensusService.cs:378`; Admin.BFF `CreatePatientListAcquired.cs:74`, `CreatePatientAcquired.cs:65` | Census (`PatientListsAcquiredListener.cs:65,72`) | DataAcquisition, Census (+ Admin.BFF hold) | Yes |
| CernerPatientsAcquired | facility JSON | DataAcquisition `CernerCCLExtractProcessor.cs:156` | Census (`CernerPatientsAcquiredListener.cs:55,63`) | DataAcquisition, Census | Yes |
| PatientEvent | facility+patient JSON | Census `EventProducerService.cs:62`; Admin.BFF `CreatePatientEvent.cs:48` | QueryDispatch (`PatientEventListener.cs:69,78`), Report (`Report/Listeners/PatientEventListener.cs:66,75`) | Census, QueryDispatch, Report (+ hold) | Yes |
| ReportScheduled | facility JSON | Tenant `ReportScheduledJob.cs:97`; DMRP `DmrpNightlyJob.cs:339`; Admin.BFF `CreateReportScheduled.cs:94`; Link.UI automation `ReportApiHelper.cs:151` | QueryDispatch (`ReportScheduledEventListener.cs:61,70`), Report (`ReportScheduledListener.cs:72,80`) | Tenant would have to stop | No (slice 2) |
| GenerateReportRequested | facility JSON | Tenant `FacilityController.cs:597, :683` | Report (`GenerateReportListener.cs:100,111`) | Tenant API | No (slice 2) |
| RetentionCheckScheduled | facility JSON | Tenant `RetentionCheckScheduledJob.cs:60` | none | n/a | No (no consumer) |
| DataAcquisitionRequested | facility+patient JSON | QueryDispatch `QueryDispatchJob.cs:84`; Report `DataAcquisitionRequestedProducer.cs:122`; measureeval `AbstractResourceConsumer.java:507`; Admin.BFF `CreateDataAcquisitionRequested.cs:48` | DataAcquisition (`DataAcquisitionRequestedListener.cs:91`) | QueryDispatch, Report, measureeval, DataAcquisition (+ hold) | Yes, after the catalog fix (2.9) |
| ReadyToAcquire | facility+patient, or facility | DataAcquisition `AcquisitionProcessingJob.cs:321`, `DataAcquisitionLogService.cs:76` | DataAcquisitionWorker (`ReadyToAcquireListener.cs:45`) | DataAcquisition, DataAcquisitionWorker | Yes |
| ResourcesAcquired | facility+patient | DA worker `AcquisitionProcessorBackgroundService.cs:363`; DataAcquisition `TailMessageRecoveryJob.cs:104` | Normalization (`ResourcesAcquiredListener.cs:122,126`) | DA worker, DataAcquisition, Normalization | Yes |
| MappingOutcomeEvaluated | facility+patient | DA worker `AcquisitionProcessorBackgroundService.cs:328`; Normalization `ResourcesAcquiredListener.cs:683` | Report (`MappingOutcomeListener.cs:75,79`) | DA worker, Normalization, Report | Yes |
| ResourcesNormalized | facility+patient | Normalization `ResourcesAcquiredListener.cs:572` | measureeval (`ResourcesNormalizedConsumer.java:49`) | Normalization, measureeval | Yes |
| EvaluationRequested | facility+patient | Report `GenerateReportListener.cs:365` | measureeval (`EvaluationRequestedConsumer.java:73`) | Report, measureeval | Yes |
| MeasureReportGenerated | facility+patient | measureeval `MeasureReportGeneratedProducer.java:80` | Report (`MeasureReportGeneratedListener.cs:74,82`) | measureeval, Report | Yes |
| ReadyForValidation | facility+patient | Report `ReadyForValidationProducer.cs:68` | validation (`ReadyForValidationConsumer.java:81`) | Report, validation | Yes |
| ValidationComplete | facility+patient | validation `ReadyForValidationConsumer.java:329` | Report (`ValidationCompleteListener.cs:70,78`) | validation, Report | Yes |
| SubmitPayload | facility+patient, or facility (manifest) | Report `SubmitPayLoadProducer.cs:43` | Submission (`SubmitPayloadListener.cs:58,86`) | Report, Submission | Yes |
| PayloadSubmitted | facility+patient, or facility | Submission `PayloadSubmittedProducer.cs:22` | Report (`PayloadSubmittedListener.cs:47,55`) | Submission, Report | Yes |
| AuditableEventOccurred | patient, facility, or service name | Account `CreateAuditEvent.cs:41`; Tenant `CreateAuditEventCommand.cs:40`; Notification `CreateAuditEventCommand.cs:47`; Report `AuditableEventOccurredProducer.cs:29`; Submission `AuditableEventOccurredProducer.cs:25`; QueryDispatch (10 sites, for example `QueryDispatchJob.cs:132`); Admin.BFF `KafkaOpsService.cs:881` | Audit (`AuditEventListener.cs:50,59`) | Nearly the whole platform | No (slice 2) |
| NotificationRequested | facility | none found | Notification (`Notification/Application/Factory/KafkaConsumerFactory.cs:31`) | Notification | Yes (low value) |
| Service-Healthcheck | service name | every service | none | n/a | Never |

Key formats: `.NET KafkaKeys.ForPatient/ForFacility/ForAudit` (`Shared/Application/Models/Kafka/KafkaKeys.cs:9,24,52`) and `Java KafkaKeys.forPatient/forFacility` (`Java/shared/.../kafka/KafkaKeys.java:12,22`).

Partitioner:
- .NET is `murmur2_random` on every factory-built producer (`Shared/Application/Models/Kafka/KafkaClientDefaults.cs:19`).
- Java uses the kafka-clients default (murmur2), because no `partitioner.class` is set.

Both hash non-null keys the same way, which is what the copy relies on (5.4).

### 2.4 Retry, redrive, and error topics

- **.NET.**
  - A failed record goes to `{T}-Retry-{svc}`, copying the key from the failed record.
  - The `{svc}-retry` group reads it and parks it as a Quartz job (`RetryListener.cs:66-81`, `:125-130`).
  - `RetryJob` later re-produces it to `{T}-Redrive-{svc}` (`Shared/Application/Jobs/RetryJob.cs:55-62`). The service's main group reads that topic alongside T.
  - Dead letters go to `{T}-Error` through the partitioner.

  None of these require the same partition count as T.
- **Java.** `{T}-Retry` and `{T}-Error` are written to the **same partition number as the source record** (`Java/shared/.../kafka/RetryTopicRecovererFactory.java:73-77`). If T has M partitions, these siblings **must have at least M partitions before measureeval or validation consumes from the new T**. Otherwise a retry from partition M-1 has nowhere to go.
- **Legacy `{T}-Retry` topics for .NET services** are still in `topics.txt`. The per-service retry listener no longer reads them (Key Inventory, note 6). The migration leaves them alone.
- **Parked retries live in the Quartz job store, not in T.** A migration of T does not touch them. They are redriven to `{T}-Redrive-{svc}`, which keeps its name and its partitions.

### 2.5 Who creates topics, with what counts and configs

- **`topics.txt`:** 59 lines. Every line is `:3:1` (3 partitions, RF 1), and none carries a config (`topics.txt:1-59`).
- **`kafka-retry-services.txt`:** names the per-service retry and redrive topics. The sync creates them with the main topic's `topics.txt` count, defaulting to 3 (`kafka-retry-services.txt:1-21`; `Azure_Pipelines/kafka-topics-sync.yaml:215-251`).
- **`kafka-topics-sync.yaml` on the console branch:**
  - It creates missing topics with the `topics.txt` count (`:136-153`).
  - It applies listed configs (`:185-201`).
  - It grows partitions only when `growPartitions=true`, and never shrinks (`:156-179`).
  - It never deletes (`:205`, `:278-291`).
- **The same pipeline on `origin/dev` deletes every topic not in `topics.txt`, except names starting with `_`** (dev `kafka-topics-sync.yaml:174-195`, skip at `:182`). The pipeline reads `topics.txt` from whichever branch it runs on (`:28-35`).

Consequences for this design:
1. The temp and journal topics are named with a leading underscore (`_linkmig-...`), so a sync run from `dev` cannot delete them.
2. During the few seconds between deleting T and recreating it, any sync run would recreate T with 3 partitions. The recreate step detects this (5.2, step C3), and the runbook freezes sync during a window.
3. After a migration, `topics.txt` still says 3. The console-branch sync leaves a larger live count alone (`:176-177`). A follow-up PR should raise the floor to M.

- **Locally,** `Scripts/create-topics-rest.sh` creates topics, and compose sets `KAFKA_AUTO_CREATE_TOPICS_ENABLE: "false"` (`docker-compose.yml:363`).
- **The deployed broker value of `auto.create.topics.enable` is unknown.** The Strimzi and Kafka default is `true`.

### 2.6 Ordering needs and duplicate tolerance

The migration gives a stronger guarantee than per-key order: all old messages are processed before any new message is produced. So this table mainly matters for the forced and recovery paths, and it explains why the design **never replays history into consumers**.

| Topic | Order needed | Consumer behaviour on a duplicate (evidence) |
|---|---|---|
| ResourcesAcquired | per patient | Tolerated. Normalization re-runs, and Report merges outcomes by (CorrelationId, QueryType) (`Normalization/Listeners/ResourcesAcquiredListener.cs:424-427`). |
| MappingOutcomeEvaluated | per patient | Tolerated. Upsert (`Report/Listeners/MappingOutcomeListener.cs:123-124`, `:276-286`). |
| ReadyToAcquire | per patient | Tolerated. Atomic log claim (`DataAcquisition.AcquisitionWorker/Listeners/ReadyToAcquireListener.cs:81-88`). |
| ResourcesNormalized | per patient | Tolerated, at the cost of extra work. Bulk upsert with a deterministic `_id` (`AbstractResourceConsumer.java:267-299`). |
| MeasureReportGenerated | per patient | Mostly tolerated. Update if it exists (`Report/Listeners/MeasureReportGeneratedListener.cs:254-260`). |
| ReadyForValidation | per patient | **Not fully.** The bundle append is guarded, but a replay duplicates Result rows (`ReadyForValidationConsumer.java:142-152`). |
| ReportScheduled | per facility | A duplicate report id is dead-lettered (`Report/Listeners/ReportScheduledListener.cs:186-188`). Noisy, not silent. |
| AuditableEventOccurred | none required | Treated as **not** tolerated (duplicate audit rows). |
| All others | per key (catalog `OrderSensitive`) | Not proven. Treated as **not** tolerated. |

### 2.7 Client behaviour that decides the design

- **Offsets reset to earliest everywhere.** The .NET factory and defaults set `auto.offset.reset=earliest`, cooperative-sticky assignment, a 30 s metadata refresh, and `client.id` ending in `-c1` (`Shared/Application/Factories/KafkaConsumerFactory.cs:32-47`; `KafkaClientDefaults.cs:32-49`). Java sets the same (`measureeval application.yml:84-89`; `validation application.yml:53-58`).
- **Producers are idempotent with `acks=all`** (`KafkaClientDefaults.cs:16-30`; `measureeval application.yml:93-96`).
- **A .NET listener stops for good when its topic disappears.**
  - On `UnknownTopicOrPart`, the listener throws `OperationCanceledException`, closes the consumer, and leaves its loop (`Shared/Application/BaseListener.cs:143-147`, `:175-179`). The same pattern appears in 15 hand-written listeners, for example `Report/Listeners/PatientEventListener.cs:111-114, :128-133` and `Normalization/Listeners/ResourcesAcquiredListener.cs:160-165`.
  - The pod keeps running, but that listener (and its redrive topic) is dead until the pod restarts.
  - **So T can never be deleted while its consumers run.**
- **Auto-create.** The Notification consumer config sets `AllowAutoCreateTopics = true` (`Shared/Application/Models/Configs/KafkaConnection.cs:17`; used by `Notification/.../KafkaConsumerFactory.cs:30`). librdkafka producers allow auto-create by default, and so do Java producers. Only the console's audit producer turns it off (`Admin.BFF/Program.cs:108`). If the broker allows auto-create, any producer still running during the delete window would recreate T with the broker's default partition count.
- **Kafka accepts an admin offset write only for a group with no active members.** The stop window guarantees that.
- **Confluent.Kafka 2.16.0 has every admin call this design needs.** I checked the package docs on the box: `CreateTopicsAsync` with `ValidateOnly`, `DeleteTopicsAsync`, `AlterConsumerGroupOffsetsAsync`, `IncrementalAlterConfigsAsync`, `DescribeConsumerGroupsOptions.IncludeAuthorizedOperations`, and `TopicDescription.TopicId`. It still lacks `DescribeLogDirs` and `ListPartitionReassignments`.

### 2.8 How the console does a partition increase today

- **Plan:** `PartitionChangePlanner.Evaluate` (`Admin.BFF/Application/KafkaOps/PartitionChangePlanner.cs:69-189`) enforces:
  - increase only, up to a cap (`:109-113`; `KafkaOpsOptions.cs:7`, default 24)
  - main, retry, and error raised together, with no sibling shrinking (`:118-134`)
  - every member advertising `c1` (`:136-149`)
  - a quiet window for order-sensitive topics: zero lag and no high-watermark movement for twice the metadata refresh (`:151-152`; `KafkaOpsService.cs:754-775`)
  - an override only with a reason (`:154-162`)
  - a second approver, always (`:164`)
  - one change in flight per environment and per family, plus a 30-minute family rate limit (`:168-178`)
  - refusal while a reassignment is in flight (`:297-302`)
- **Request:** a reason and the typed topic name (`KafkaOpsService.cs:335-345`). The requester can't approve or execute (`ChangeRequestRecord.cs:100-152`).
- **Execute:** re-plans excluding itself, then `CreatePartitions` on main, retry, and error (`KafkaOpsService.cs:409-466`; `KafkaBrokerGateway.cs:300-319`). It then converges (every group assigned) and verifies for 15 minutes (`KafkaOpsService.cs:491-622`).
- **Disabled provider:** partition increases are refused, because no one can confirm that no reassignment is in flight (`docs/kafka-partition-operations.md`, rule 9).
- **State:** records are kept through `ICacheService` for 24 h (`KafkaOpsService.cs:828-835`). In-memory is the fallback when Redis is not configured (`Admin.BFF/Program.cs:177-184`). Every transition is audited to `AuditableEventOccurred` (`:848-892`).

### 2.9 Console gaps the migration depends on

1. **The catalog is stale after the composite-key change** (`Shared/Application/Models/Kafka/KafkaTopicCatalog.cs:34-56`):
   - PatientEvent, EvaluationRequested, ValidationComplete, ReadyForValidation, MeasureReportGenerated, and DataAcquisitionRequested are now patient-keyed JSON, not `{facilityId}`, `{facilityId}:{correlationId}`, or unkeyed.
   - DataAcquisitionRequested is still hard-blocked for a CRC32/murmur2 mismatch that `KafkaClientDefaults.cs:19` has since removed.
   - `RetryName` returns the legacy `{T}-Retry` (`:108`). The per-service retry and redrive topics and the `{svc}-retry` groups are missing.
   - There is no list of producers.
2. **Only four topic configs are read** (`KafkaBrokerGateway.cs:74-80`). A recreate must copy every dynamic topic config.
3. **Groups are described from the catalog only** (`KafkaBrokerGateway.cs:165-276`). This is deliberate: `ListConsumerGroups` crashes librdkafka 2.16.0 when a broker is lost mid-call (proof report, 332c crash matrix). A migration must find every group with offsets on T.
4. **No executor lease.** `KafkaOpsWorker` polls every 5 s on every Admin.BFF replica (`KafkaOpsWorker.cs:14-40`).
5. **The 24 h TTL and the in-memory fallback** are not acceptable for a record that spans a deleted topic.
6. **`IKafkaInfraProvider` can only scale a consumer group's Deployment** (`KafkaInfraProvider.cs:22-34`, `:143-151`, `:294-306`). It can't read replica counts or scale arbitrary workloads. The Strimzi client is unconfigured (`Admin.BFF/Program.cs:114`).

---

## 3. Decisions

### 3.1 Recreate under the same name, not a versioned name

| | Recreate same name (chosen) | Versioned name plus config switch |
|---|---|---|
| Service code change | None | A logical-to-physical routing layer in every .NET producer and consumer path and in Java, including the retry, redrive, and error derivation |
| Switch mechanism | None needed | A restart today (`IOptions`), or runtime settings (Workstream B, not built) |
| Traffic cutovers per migration | 0 (stop and start) | 1 with routing. The architect's literal method (move to temp, then back) needs 2. |
| Stop window | Yes, bounded, per topic | Still needed to keep per-key order during the producer switch |
| ACLs | Unchanged (name-based) | New grants for every new name unless a prefix is used |
| topics.txt, sync, catalog, dashboards, error monitor, Java suffixes | Unchanged | All must learn aliases |
| Irreversible step | The delete of T, after a verified backup and a human go | None. The old topic stays until it is drained. |
| Rollback before the point of no return | Full and lossless | Full |
| New failure modes | Delete and recreate races (auto-create, sync, Topic Operator), all detected | Routing drift between replicas and runtimes, permanently |

**Decision: recreate under the same name.** The versioned approach removes the delete, but it adds a permanent cross-language routing layer and still needs a stop window for ordering. Its only real advantage is avoiding the delete. The design makes the delete safe with a drained topic, a verified backup, explicit offsets, race detection, and a typed hold point. Revisit routing only if zero-stop migrations become a requirement.

### 3.2 Quiesce, not dual-write. Stop workloads; hold control-plane producers.

- **Dual-write is rejected.** It needs topic-name switching, which doesn't exist (3.1). It breaks per-key order across the two topics, and it creates duplicates.
- **Quiesce by stopping the stop-set workloads** through the infra provider, or through a verified manual checklist.
  - This works for .NET and Java with no service code change.
  - It is the only option that also takes consumers out of their groups. That is required before the delete (2.7) and for the offset writes.
  - The console never trusts the stop. It verifies it from the broker: groups Empty, and high watermarks frozen for the quiet window.
- **A runtime produce-hold flag** is better for API services such as Tenant and Account. It depends on Workstream B runtime settings, so it is slice 2. Until then, topics with Tenant or Account producers are not eligible (2.3).
- **Admin.BFF and Link.UI are never stopped.** They are the control plane. They hold T in-process:
  - Admin.BFF integration commands for T return 409.
  - The Admin.BFF integration consumer service won't subscribe to T.
  - Link.UI refuses to start an automation run while a ReportScheduled migration is open. That topic isn't eligible until slice 2, but the gate is built now.

### 3.3 Drain is the correctness mechanism; the copy is a backup

- **Correctness comes from the drain.** Every discovered group must have committed equal to the high watermark on every partition of T before the consumers stop. After that, every message has been consumed. T holds only consumed history, and the recreated topic does not need that history.
- **The backup copy writes that history into the `_linkmig-` temp topic with M partitions,** re-hashed by key, so per-key order is kept. It is verified before the delete. It stays as a verified backup for replay until a person confirms cleanup (typed name, `CanMigrateKafkaTopics`) or `KafkaOps:MigrationBackupRetentionHours` (default 168) expires. It is never deleted on failure, abort, timeout, or rollback. Rollback leaves it and sets `BackupCleanupRequired`.
- **Slice 1 does not copy the backup onto the recreated T.** The recreated topic starts empty, and consumer groups start on it at offset 0. Copying the backup onto T would either reprocess everything or need offsets set past restored data, and it would lengthen the window. That option is slice 3 (9.2). It is not in slice 1.
- **The backup copy is the only copy.** Step B6 uses `toPositive(murmur2(key)) % M`. A null key uses `sourcePartition % M`. It stamps `x-link-migration`. Resume of that copy is header-idempotent. It writes only the temp topic. It does not write the new T, and it does not delete the temp topic.
- **A topic too large to copy within `KafkaOps:MigrationMaxBackupMinutes`** can only proceed with backup set to "skip". That needs a typed acknowledgment from the requester and the approver. The proof in section 10 requires a real backup.

### 3.4 Offsets: same group ids, written explicitly to 0

The migration keeps every group id. After C4 has verified that the recreated topic is empty, and while the groups are Empty, C5 writes offset 0 on every partition for every discovered group and reads the offsets back. It does not write the high watermark of copied data. There is no copied data on the new T. If any high watermark is above 0, C5 does not write offsets and does not delete the topic. Recovery to the original partition count uses the same rule: the topic is recreated empty, and offset 0 is written only when every high watermark is 0 and the groups are Empty. The write makes the result independent of `auto.offset.reset` and of whether the broker dropped the old offsets. Kafka normally drops a group's offsets for a deleted topic, but the design doesn't rely on that.

### 3.5 Retry and DLT topics

| Sibling | Action | When |
|---|---|---|
| Java `{T}-Retry`, `{T}-Error` (partition-pinned) | Grow to at least M (grow only) | Before the stop (step A3). Required. |
| .NET `{T}-Error` | Grow to at least M for family consistency | A3. Optional, on by default. |
| .NET `{T}-Retry-{svc}`, `{T}-Redrive-{svc}` | Untouched | The partitioner picks their partitions, and they aren't deleted. |
| Legacy `{T}-Retry` (.NET) | Untouched. Its lag is shown for information. | |

Growing siblings can't be undone, but it is harmless. It is kept even if the migration rolls back.

### 3.6 Where the state lives and who executes

- **Executor:** Admin.BFF (`KafkaOpsWorker`). Link.UI is only a proxy, so a Link.UI restart has no effect on a migration.
- **Lease:** a Redis lock per environment, `kafka-ops:{env}:executor`, using `DistributedLock.Redis`, which `Shared` already references (`Shared/Shared.csproj:34`). The lease lasts 30 s and is renewed every 5 s. Every write carries a fencing token. The existing change tracking (`TrackAsync`) moves under the same lease.
- **Record:** in the ops store (Redis), with **no expiry while the migration is open**. A migration refuses to start when the ops store is in-memory, except on LocalCompose.
- **Durable mirror:** a compacted journal topic, `_linkmig-journal` (1 partition, RF = min(3, brokers), `min.insync.replicas` = min(2, RF)). It holds the latest record per migration id. Each step writes its intent before acting and its result after. On startup, Admin.BFF reconciles the ops store with the journal, and the newer step sequence wins.
- **The broker is the final source of truth.** Every step's pre- and postconditions are checked against live broker facts (topic id, partition count, high watermarks, group state), so a lost or stale record can't trigger a wrong action.

### 3.7 When the lighter tool is better

The guarded in-place partition add stays. Under a met quiet window it is ordering-safe for live consumers, and it doesn't stop consumers. Use it for a plain increase when traffic naturally pauses. Use the migration when any of these apply:
- you need a decrease (slice 2)
- retained history must be re-hashed to M and kept on the backup for replay
- you need full rollback until the last moment
- architect policy requires it

The wizard shows both options and the blast radius of each. Slice 1 does not put retained history on the new topic. That history stays on the backup. The new topic starts empty.

---

## 4. Preflight (read-only; runs at plan, again at execute, and parts again before the delete)

Every check fails closed. An unknown answer is a refusal unless the table says otherwise.

| Area | Check |
|---|---|
| Environment | Not ReadOnly. `KafkaOps:AllowTopicMigration=true` (default false). The provider is LocalCompose or Strimzi; Disabled refuses. The ops store is durable. No other open change request or migration in the environment. In-flight reassignments are known and empty (existing rule 9). `KafkaOps:TopicOperatorManagesLinkTopics=false` is set after DevOps confirms, and Strimzi with read access also checks that no `KafkaTopic` CR names T. Outside LocalCompose, a dedicated KafkaOps connection (6.2) must be configured. |
| Rights | `DescribeTopics` authorized operations on T include DESCRIBE, DESCRIBE_CONFIGS, READ, DELETE, ALTER. A validate-only `CreateTopics` succeeds for the temp name, and for T it returns already-exists rather than an authorization error. Authorized operations on each discovered group include READ. The journal topic is writable. |
| Cluster health | Every broker is up, with no under-replicated or offline partitions. T and its siblings have full ISR. The broker count is at least the target RF. `min.insync.replicas` can be met. The broker's `auto.create.topics.enable` is read; when true, the plan warns and C3 runs strict foreign-topic detection. |
| Topic | T is in the catalog, eligible for slice 1, and not hard-blocked. M is greater than N and within the cap. `cleanup.policy=delete` (compacted topics are refused in slice 1). Every dynamic topic config is captured with its source. The `message.timestamp.*` settings are captured. Retention bytes and time are captured. The topic id is recorded. |
| Size and disk | Estimated bytes = records x sampled average size (the last 100 records per partition), labelled as an estimate. LocalCompose gives exact sizes (`kafka-log-dirs.sh`). The estimated copy time must be within `MigrationMaxBackupMinutes`, or backup is set to "skip". Free disk must be at least backup bytes x temp RF x 1.5 where known. On Strimzi without metrics, the backup estimate must stay under `KafkaOps:MigrationUnknownDiskMaxBytes` (default 1 GiB). |
| Groups | Discovery: catalog groups, plus `{svc}-retry`, plus a single preflight-only `ListConsumerGroups` call through a short-lived client, made only when every broker is up (a crash there changes nothing). Then offsets for each group. Every group with offsets on T is listed with state, members, `c1` advertisement, and lag. An active group not mapped to a stop-set workload is refused. An inactive group with lag must be acknowledged by typing its name, and its offsets on T are dropped. |
| Stop set | Derived from catalog producers and consumers. Every workload must map to a provider target (`KafkaOps:Workloads`). Current replicas are read and recorded. Control-plane holds are listed. |
| Drain feasibility | Current lag divided by the observed consume rate must give an estimated drain time under 5 minutes. |
| Window estimate | Stop + quiet + drain + stop consumers + one backup + hold + delete/create + verify empty + offset write of zeros + start. The backup is counted once. There is no second copy budget. The total is shown, with an alert threshold at 1.5x. |

---

## 5. The migration state machine

### 5.1 Overview

```
Planned -> Pending -> Approved -> [A] Prepare (live, reversible)
   A1 Preflight -> A2 CreateBackupTopic -> A3 GrowPinnedSiblings -> A4 ArmHolds
-> [B] Stop window (reversible)
   B1 StopProducers -> B2 QuietWindow -> B3 Drain -> B4 StopConsumers
   -> B5 Freeze -> B6 Backup -> B7 VerifyBackup -> H1 HoldForGo (typed T)
-> [C] Point of no return
   C1 DeleteT -> C2 AwaitDeletion -> C3 CreateT(M) -> C4 VerifyT -> C5 WriteOffsets
-> [D] Resume
   D1 StartConsumers -> D2 ReleaseHolds+StartProducers -> D3 Verify -> Done
Any A/B step failure, timeout, or Abort -> RollingBack -> RolledBack
   The temp topic is kept and flagged BackupCleanupRequired.
Any C/D failure -> NeedsAttention with Recover(forward to M | original N)
   Nothing is deleted without a typed action.
```

There is no step that copies the temp topic onto T, and no second verify of such a copy. The step order is A1-A4, B1-B7, H1, C1, C2, C3, C4, C5, D1, D2, D3, Done. C5 writes 0. A backup record with no migration header, or with another migration id, stops in NeedsAttention. Nothing is deleted.

### 5.2 Steps

Each step works the same way:
1. Write intent to the record and the journal.
2. Check the precondition against the broker.
3. Act.
4. Check the postcondition against the broker.
5. Write the result and an audit event.

Re-running any step after a crash is safe.

| Step | Action | Precondition, and resume behaviour | Postcondition |
|---|---|---|---|
| A1 | Full preflight (section 4) | none | All checks pass. The material facts are hashed and compared with the approved plan hash. |
| A2 | Create `_linkmig-<T>-<id8>`: M partitions; RF = max(T's RF, min(3, brokers)); T's dynamic configs. `retention.ms` is not lowered. The age-based raise happens in B6, before any record is written. | If it exists with this migration's name and topic id and a matching spec, adopt it. If the name and id belong to this migration, the spec differs, and every high watermark is 0, replace it. Any other id, or any high watermark above 0, stops the step. The topic is not deleted. | It exists, its id is recorded, and every high watermark is 0. |
| A3 | Grow the siblings in 3.5 to at least M | Grow only. A no-op if already at least M. | Counts are at least M. |
| A4 | Set the in-process holds in Admin.BFF and Link.UI | Idempotent flag in the record | Holds are reported active. |
| B1 | Record producer replicas, then scale producers to 0 | Recorded replicas are never overwritten on resume | The provider reports 0 running, or the manual checklist is confirmed. |
| B2 | Quiet window: T's high watermarks unchanged for 2x the metadata refresh (60 s) | Restarts the timer on resume | HW frozen for the window |
| B3 | Drain: wait until every discovered group's committed offset equals the HW on every partition | Two uncached reads at least 10 s apart | Lag is 0 for all groups |
| B4 | Record consumer replicas, then scale consumers to 0 | As in B1 | Every discovered group is Empty with 0 members. With static membership this can take a session timeout. |
| B5 | Freeze: record topic id, log start and end offsets, committed offsets, configs, and RF | Re-reads and compares | HW is unchanged since B2. Committed equals HW. Groups are Empty. |
| B6 | Copy T to the temp topic (5.4). Before the first write, raise `retention.ms` on the temp topic to at least max(frozen `retention.ms`, age of the oldest CreateTime + `MigrationBackupRetentionHours`). Do not lower it afterward. This copy does not write the new T. | Resume reads the temp topic, accepts only this migration's `x-link-migration` header, and produces identities that are absent. The temp topic is not deleted. The new T is not written. A record without that header, or with another migration id, stops in NeedsAttention. | Delivery confirmed for every record, each carrying this migration's header |
| B7 | Verify: re-read the temp topic, compare counts per source partition and the order-sensitive digest per temp partition | Re-runnable | Exact match |
| H1 | Pause. Show the evidence. The executor types `T` to go. | The timer is persisted | Go received, or the hold timeout (default 15 min) triggers automatic rollback. The temp topic is kept and flagged `BackupCleanupRequired`. |
| C1 | Re-check B5 facts and the backup. Delete T. | If T's id still equals the frozen id: delete. If T is absent: done. Any other id is a foreign topic: stop. | T is gone or being deleted |
| C2 | Wait until T is absent from metadata on every broker | Poll | Absent |
| C3 | Create T with M partitions, or with N when the recovery choice is original. Use the frozen RF and the frozen dynamic configs, including the frozen `retention.ms`. Record the new topic id. Do not raise retention on the new T, and do not write records to it. | If T exists with the recorded new id: adopt it. If T exists with an unknown id: **foreign topic** (auto-create or a sync run). Stop and show partitions, configs, and HW. The operator may delete it only if it is empty, by typing its name. A second person is required. Nothing is deleted without that typed action. | It exists with the recorded new id, and it is still empty |
| C4 | Verify: M partitions (N when recovering the original count), RF, full ISR, configs equal to the frozen set including `retention.ms`, every high watermark 0, leaders spread (run a preferred election if skewed). | Re-runnable. A high watermark above 0 stops in NeedsAttention and does not delete T. | All true, and the topic is still empty |
| C5 | Write offset 0 on every partition for every discovered group, then read the offsets back. Do not write a high watermark. | Groups must be Empty and every high watermark must be 0. If any high watermark is above 0, do not write offsets and do not delete the topic. A rewrite of 0 gives the same result. | Committed offsets read back as 0 |
| D1 | Restore consumer replicas | Target counts come from the record | Every group is Stable, with every partition of T and its redrive topics assigned |
| D2 | Release the holds and restore producer replicas | As in D1 | The provider reports the replicas, and T's HW starts moving (or the note "no traffic yet") |
| D3 | Verify window (existing logic, 15 min): lag does not climb past the baseline, no partition is stalled, `{T}-Error` doesn't advance unexpectedly | Re-runnable | Done, or NeedsAttention |

After Done or RolledBack, the temp topic stays until a person confirms cleanup or `KafkaOps:MigrationBackupRetentionHours` expires. Cleanup is a separate request: the typed temp-topic name and `CanMigrateKafkaTopics`. Before the retention expires, a second person must approve. The topic is never deleted because a step failed, an operator aborted, a step timed out, or the migration rolled back. Those paths leave it and set `BackupCleanupRequired`. Expiry does not run while the migration is open or in NeedsAttention.

### 5.3 Holds for control-plane producers

Admin.BFF keeps a hold registry backed by the migration record. While a hold is set for T:
- the integration commands that produce to T return 409, naming the migration
- the integration `KafkaConsumerService` refuses to subscribe to T
- Link.UI asks Admin.BFF for active holds before starting an automation run, and refuses a run that would produce to a held topic

The console's own audit producer only writes to `AuditableEventOccurred`, which is not eligible in slice 1.

### 5.4 The backup copy

B6 copies T into the temp topic. It does not write the new T. There is no later copy from the temp topic onto T.

- **Read:** one consumer with `Assign` (no group, so no group ACL and no commits) per source partition, from the log start to the frozen end. Bytes are read as `byte[]` and never deserialized. Isolation is `read_committed`; Link has no transactional producers.
- **Partition:** `toPositive(murmur2(keyBytes)) % M`, identical to Java's default and to librdkafka's `murmur2_random` for non-null keys. A null key uses `sourcePartition % M` and is counted in the evidence. None are expected after the composite-key change.
- **Write:** one idempotent producer (`acks=all`, `enable.idempotence=true`, `max.in.flight=5`, zstd, `message.max.bytes` = T's `max.message.bytes`). Each record keeps its key, value, headers, and CreateTime. One header is added: `x-link-migration: {migrationId};{sourceTopic};{sourcePartition};{sourceOffset}`. The migration id is 32 hex digits with no hyphens. The header names the original topic, partition, and offset.
- **Resume:** this resume is only for the backup copy. Read the temp topic. Accept only records whose header belongs to this migration id. Produce source identities that are not already present. Do not delete the temp topic. Do not write the new T. A temp-topic record with no `x-link-migration` header, or with a different migration id, stops in NeedsAttention. An interrupted backup is not deleted and is not started over from an empty topic.
- **Order:** within a source partition, records are read and produced in offset order. A key comes from exactly one source partition when all producers used the same partitioner over the retained window. That has been true since the murmur2 change, and earlier history is already consumed. So each key's records arrive at their temp partition in their original order.
- **Retention:** records keep CreateTime, so a short `retention.ms` would drop them from the backup as soon as they were written. Before the first write, `retention.ms` on the temp topic is raised to at least max(frozen `retention.ms`, age of the oldest CreateTime + `MigrationBackupRetentionHours`). The value is not lowered afterward. The new T keeps the frozen `retention.ms`. It is not raised, because nothing is written to it.
- **Digest:** per temp partition, SHA-256 over the records in offset order. Each record contributes the source partition and source offset from the migration header, the key, the value, the CreateTime, and the other headers in name order. The migration header itself is left out. Counts are kept per source partition and in total. B7 recomputes both from a fresh read of the temp topic.
- **Throughput watchdog:** if the observed rate projects past `MigrationMaxBackupMinutes`, B6 fails and the migration rolls back. The temp topic is kept and flagged `BackupCleanupRequired`. There is no second copy budget.

### 5.5 Rollback and recovery

| Where it stops | What happens | Data result |
|---|---|---|
| A1 to A4 | Release the holds. Do not delete the temp topic. If it exists, set `BackupCleanupRequired`. The only delete in this band is A2 replacing an empty temp topic whose name and id belong to this migration and whose high watermarks are all 0. Siblings stay grown. | T untouched. Any temp topic that already has records is left in place. |
| B1 to B7, or H1 abort or timeout | Stop the copy. Do not delete the temp topic. Set `BackupCleanupRequired`. Restore consumer replicas, then release the holds and restore producer replicas. Verify that T's id and partitions are unchanged. | Lossless. T untouched. Consumers resume from their committed offsets. The backup is kept and flagged. |
| Header mismatch on B6 | Stop in NeedsAttention. Do not delete the temp topic and do not delete T. Rollback is available because T still has its original id, and it still does not delete the temp topic. | T untouched. The temp topic is kept for inspection. |
| C1 to C2 | Nothing is deleted automatically. Default: **forward** (C3 with M, then C4 and C5 writing offset 0 on the empty topic). **Original:** recreate with N, empty, then write offset 0 only when every high watermark is 0 and the groups are Empty. | T is gone until recreate. Every record was consumed and is in the verified backup. The recreated topic starts empty. The backup is kept. |
| C3 to C5 | **Forward** continues: verify the topic is empty and write offset 0. **Original:** delete the new T only when its id is the recorded one, every high watermark is 0, and the action is typed. Then recreate it empty with N, verify every high watermark is 0, and write offset 0. If any high watermark is above 0, do not write offsets and do not delete the topic. | The chosen layout is empty. Consumers start at offset 0. The backup is kept. |
| D1 to D3 | No automatic rollback. New traffic now uses the M layout. Going back to N is a new migration (M to N, slice 2). | Correct and ordered. Investigate any NeedsAttention. |

After C1, nothing is deleted automatically. Every post-C1 failure stops in NeedsAttention with the two recovery actions and the manual runbook. Recover-original recreates the original partition count empty. It does not copy the backup onto T. Offsets are written to 0 only when every high watermark is 0 and the groups are Empty. The temp topic stays until a person confirms cleanup or the backup retention expires. It is not deleted on failure.

### 5.6 Timeouts (defaults, configurable under `KafkaOps:Migration*`)

| Step | Timeout | On timeout |
|---|---|---|
| A1 | 2 min | Refuse |
| A2, A3 | 1 min each | Roll back |
| B1, B4 | 5 min each (pod grace periods) | Roll back |
| B2 | 60 s window, 5 min maximum | Roll back (traffic didn't stop) |
| B3 | 10 min | Roll back (a stuck partition or poison message) |
| B6 + B7 | `MigrationMaxBackupMinutes` (15) | Roll back. The temp topic is kept and flagged `BackupCleanupRequired`. |
| H1 | 15 min | Roll back. The temp topic is kept and flagged `BackupCleanupRequired`. |
| C1 to C2 | 2 min | NeedsAttention. Nothing is deleted. |
| C3 to C5 | 2 min | NeedsAttention. Nothing is deleted. Offsets are not written unless every high watermark is 0. |
| D1, D2 | 10 min each | NeedsAttention (services are partly up; the page says which) |
| D3 | 15 min | NeedsAttention or Done |

The total window is alerted at 1.5x the plan's estimate. The estimate counts the backup once. There is no second copy budget. A timeout before C1 rolls back and keeps the temp topic. A timeout after C1 stops in NeedsAttention and deletes nothing.

### 5.7 Restarts and partial failures

- **Link.UI pod restarts:** no effect. It holds no migration state and re-reads the record. Link.UI stays at one replica.
- **Admin.BFF pod restarts or is killed at any step:**
  - The lease expires within 30 s. The next pod (or the same one after restart) takes the lease, reconciles the record with the journal, and re-enters the current step through its precondition check.
  - The stop window simply lasts longer, because the stopped workloads stay stopped. Timeouts use the persisted timestamps.
  - A backup copy in progress resumes from the `x-link-migration` identities already on the temp topic. It does not start over, it does not write a duplicate, it does not write the new T, and it does not delete the temp topic or the new T. A temp-topic record without this migration's header stops in NeedsAttention.
- **Two Admin.BFF replicas:** only the lease holder runs steps. A write with a stale fencing token is rejected.
- **Redis is lost or flushed:** the journal restores the record. If both are lost, an **orphan scan** blocks every Kafka change in the environment. It looks for `_linkmig-*` topics with no record, and stop-set workloads at 0 replicas that the console can see. It does not delete those topics. It then offers only the safe actions the broker supports:
  - T still has its original id: roll back the workloads, keep the temp topic, and set `BackupCleanupRequired`.
  - T is missing: do not start consumers on a topic that is not there. A typed recover recreates T empty at M or at N and writes offset 0 only when every high watermark is 0 and the groups are Empty. The backup is kept and is not copied onto T.
  - T has the recorded new id: resume at C4 or C5. Do not delete it. Do not copy the backup onto it. If any high watermark is above 0, do not write offsets and do not delete the topic.
- **A broker is lost:** if it happens before C1, the step fails and rolls back. The temp topic is kept. After C1, recovery waits for a full ISR before C3 and C4 continue.
- **A stopped workload comes back up during the window** (for example, GitOps reverting replicas):
  - B2 sees the HW move, or B3 and B5 see members return, and the migration rolls back. The temp topic is kept.
  - After C1, C3's foreign-topic check or C5's Empty-group and high-watermark check stops in NeedsAttention. Nothing is deleted.

---

## 6. Approvals, safety rails, permissions

### 6.1 Approvals and rails

- **New permission:** `CanMigrateKafkaTopics`, added to `LinkSystemPermissions` and enforced in Admin.BFF. It is required to request, approve, execute, go, abort, recover, and delete a backup. Viewing needs `CanViewInfrastructure`.
- **Request:** a reason, the typed topic name, target M, an optional planned window start, and the plan hash. A dry run is mandatory, and the request stores it.
- **Approve:** a different person, who sees the same dry run. The approval is bound to the plan hash. If T's partitions, the stop set, the discovered groups, the size class, or the siblings change, re-approval is required.
- **Execute:** not the requester.
- **H1 go:** the executor types T again.
- **Recover-original and foreign-topic delete:** typed names. A second person is required for the foreign-topic delete.
- **Limits:** one migration per environment, no other open change, and the existing family rate limit. A migration is refused while any reassignment is in flight, and it blocks broker moves while it is open.
- **Production:** read-only by default (`KafkaOpsService.cs:134-135`), and `AllowTopicMigration` defaults to false everywhere. It is enabled in dev first, then test and qa, after the proof.

### 6.2 Kafka permissions (DevOps-managed ACLs)

These rights include Delete on Link topics. They must go to a **dedicated `link-ops` KafkaUser** used only by Admin.BFF's KafkaOps module, through a separate connection section (`KafkaOps:Connection`, with credentials as Key Vault references under the `LinkAdminBFF` label). They must never go to the shared service principal. Outside LocalCompose, migrations refuse to run on the shared principal.

| Resource | Pattern | Operations | Used for |
|---|---|---|---|
| Topic: each eligible main topic | literal | Describe, DescribeConfigs, Read, Delete, Create, Alter | describe, backup read, delete, recreate, the existing partition add |
| Topic: siblings (`-Retry`, `-Error`, `-Retry-*`, `-Redrive-*`) | literal or prefixed per main topic | Describe, DescribeConfigs, Alter | lag, growing pinned siblings |
| Topic: `_linkmig-` | prefixed | Create, Delete, Describe, DescribeConfigs, Read, Write | backup topics and the journal |
| Topic: `AuditableEventOccurred` | literal | Write | audit (existing) |
| Group: every Link group (`Audit`, `Census`, ..., `{svc}-retry`, `measureeval*`, `validation`) | literal or `*` | Describe, Read | lag, and writing explicit offsets (OffsetCommit needs Group Read and Topic Read) |
| Cluster | n/a | Describe, DescribeConfigs | preflight group discovery, the broker `auto.create.topics.enable` read, controller roles (existing) |

No transactional id and no Group Delete are needed: offsets are overwritten, never deleted. Idempotent produce needs only topic Write on Kafka 3.0 and later.

### 6.3 Provider differences

| Capability | LocalCompose (box, dev) | Strimzi (deployed, once DevOps enables the client) | Disabled |
|---|---|---|---|
| In-flight reassignment list | `kafka-reassign-partitions --list` (existing) | `KafkaRebalance` status (existing) | Unknown, so migrations are refused |
| Stop and start workloads | `docker compose up -d --scale svc=N` (extends `ScaleGroupAsync`) | Patch Deployment `spec.replicas` in `KafkaOps:WorkloadNamespace`. With KEDA, use the `autoscaling.keda.sh/paused-replicas` annotation. Without patch rights: a **manual checklist**, verified from the broker. | n/a |
| Read replica counts | `docker compose ps` | `GET` Deployment | n/a |
| Topic Operator check | n/a | List `kafkatopics.kafka.strimzi.io`, refused if one names T | n/a |
| Exact size, disk | `kafka-log-dirs.sh`, `df` | Prometheus if configured, otherwise an estimate | n/a |

New provider members: `GetWorkloadReplicasAsync`, `ScaleWorkloadAsync`, `CanScaleWorkloads`, `ListTopicResourcesAsync`, `TopicSizeAsync`. Workload mapping is configured in `KafkaOps:Workloads` (`Report=link-report,...`).

Kubernetes RBAC for the Strimzi mode (DevOps):
- In the Kafka namespace: get and list on `kafkarebalances` and `kafkatopics`.
- In the Link namespace: get and patch on the named Deployments (`resourceNames`), and get and patch on `scaledobjects` if KEDA is present.
- With read-only rights, the manual checklist mode is used.

### 6.4 Confirmations from DevOps (not architect questions)

1. The Strimzi Topic Operator does not manage Link topics. If it does, migrations stay off in that environment.
2. Set `auto.create.topics.enable=false` on the Link clusters. This is recommended, not a blocker: C3 detects a foreign topic either way.
3. Create the `link-ops` KafkaUser with the ACLs in 6.2, and wire its credentials for Admin.BFF.
4. Provide a Kubernetes client for the Strimzi provider, at least read-only.
5. Freeze `kafka-topics-sync` during migration windows.

---

## 7. Observability

- **UI timeline:** step, state, start, duration, and evidence:
  - the frozen offsets
  - lag snapshots
  - copy counts and digests
  - the stop set with live replica status
  - the H1 countdown
  - the manual runbook (the CLI equivalent of each step and each recovery path)
- **Audit:** every transition goes to `AuditableEventOccurred`: user, step, before and after values, reason, plan hash, and correlation id. The journal topic is the second, local record.
- **Logs and traces:** every log line and OpenTelemetry span carries the migration id and correlation id. Each step is one span.
- **Alerts:** in the UI, and in the audit as `migration-slow` or `migration-needs-attention`. They fire for a step timeout, the window passing 1.5x the estimate, H1 nearing its timeout, a foreign topic, and lease loss mid-step.

---

## 8. Topic and partition controls in the console

Admin.BFF enforces every rule. Link.UI only displays them.

**Topic list.** For each catalog main topic:
- partitions, RF, ISR health, leader skew (largest per-broker leader share against ideal)
- size (exact or labelled as an estimate), produce rate
- lag per group and in total, key class and key shape (fixed for composite keys)
- family status: legacy retry, per-service retry and redrive, error, and any pinned sibling that is behind
- `topics.txt` drift (live count against the file)
- eligibility for partition add and for migration, with the reason

**Topic detail.** Per partition:
- leader, replicas, ISR, preferred leader, log start offset, high watermark, estimated size
- committed offset and lag for each discovered group, plus owner member and client id

It also shows:
- the producers (workloads and code sites from the catalog) and consumers (groups and workloads)
- the stop set for a migration
- holds and any open migration

**Config view and diff.**
- Every topic config with its source (dynamic, broker default, static).
- Diffs against: the broker default, family siblings, the migration's frozen snapshot (it must be empty after C4), and `topics.txt` declared configs (none today).
- Config edits are out of scope.

**Partition add (kept).** The existing flow and guards stay. These are added:
- An ordering-impact warning that lists the keyed producers, with workload and file, the key shape, and the share of keys that will move (1 - N/M).
- A link to "Migrate instead" when the quiet window can't be met naturally.
- The catalog fixes from 2.9.

**Migration wizard.** Seven steps:
1. Topic and target.
2. Dry run: every section 4 fact, the stop set, the window estimate, the backup size and mode, the rights, warnings, and an "in-place add instead?" comparison.
3. Request.
4. Approve.
5. Execute and live timeline.
6. H1 go.
7. Verify and close.

**API (Admin.BFF, `/api/ops/kafka`):**
- `GET topics/{t}/detail`
- `GET topics/{t}/configs?diff=`
- `POST topics/{t}/migrations/plan`
- `POST migrations`
- `GET migrations/{id}`
- `POST migrations/{id}/approve|reject|execute|go|abort|recover`
- `POST migrations/{id}/manual-step` (checklist mode)
- `GET migrations/{id}/runbook`
- `POST backups/{name}/delete`
- `GET holds`

---

## 9. Scope

### 9.1 First Build slice

1. **Catalog rework and guard tests:**
   - Key classes and shapes after the composite-key change.
   - Remove the obsolete DataAcquisitionRequested block.
   - A family model: legacy retry, per-service retry and redrive, error, and pinned siblings.
   - Producer and consumer workloads, control-plane flags, and slice-1 eligibility.
   - A guard test that scans .NET produce sites (`nameof(KafkaTopic.X)` and `KafkaTopic.X.ToString()`) and Java `Topics.*` sends and subscriptions, and fails when the catalog misses a producer or consumer. It follows the pattern of `KafkaKeyProof.Tests/ProducerPartitionerGuardTests.cs` and `RetryTopicGuardTests.cs`.
2. **Read-side topic controls (section 8):** the list additions, topic detail, the config view and diff, and the ordering warning on partition add.
3. **Ops store hardening:** migration records with no TTL while open, refusal on an in-memory store (except LocalCompose), the `_linkmig-journal` mirror, and the executor lease with fencing (also applied to `TrackAsync`).
4. **Gateway additions:** full `DescribeConfigs`, `CreateTopics` (validate-only and real), `DeleteTopics`, `AlterConsumerGroupOffsets`, group authorized operations, `TopicId`, the broker config read, and preflight-only group discovery behind the healthy-cluster gate.
5. **Provider additions:** workload replica read and scale for LocalCompose and Strimzi, and the Strimzi manual-checklist mode. Disabled refuses.
6. **Migration planner, state machine, copier, and verifier:** increase only; the backup is mandatory unless explicitly skipped. Slice 1 does not copy the backup onto T. The recreated topic stays empty, and C5 writes 0 on every partition. Rollback keeps the backup and sets `BackupCleanupRequired`. The H1 hold point, recovery, and the timeouts. One backup in the window estimate.
7. **Holds** in Admin.BFF and Link.UI.
8. **Wizard, timeline, and runbook view.**
9. **Gates:** `CanMigrateKafkaTopics`, `KafkaOps:AllowTopicMigration` (default false), and a dedicated KafkaOps connection required outside LocalCompose.
10. **Tests (section 10), the proof kit `Scripts/kafka-migration-proof/`,** and docs (update `docs/kafka-partition-operations.md`; new `docs/kafka-topic-migration.md` runbook).

**Eligible topics in slice 1 (16):** PatientCensusScheduled, PatientListsAcquired, CernerPatientsAcquired, PatientEvent, DataAcquisitionRequested, ReadyToAcquire, ResourcesAcquired, MappingOutcomeEvaluated, ResourcesNormalized, EvaluationRequested, MeasureReportGenerated, ReadyForValidation, ValidationComplete, SubmitPayload, PayloadSubmitted, NotificationRequested.

### 9.2 Later

- **Slice 2:**
  - A runtime produce-hold for Tenant, Account, and Notification (needs Workstream B). That makes ReportScheduled, GenerateReportRequested, and AuditableEventOccurred eligible.
  - Partition decreases.
  - A pre-copy while live, so only the final delta is copied inside the window.
  - KEDA paused-replicas.
  - Prometheus sizes.
- **Slice 3:** optional history restore into T (temp partition to T partition 1:1, then offsets set to the restored end, before D1), compacted topics, changing RF during a migration, and several topics in one window. History restore is not in slice 1.
- **Not planned:** versioned topic names and runtime topic routing (3.1).

---

## 10. Acceptance tests

### 10.1 Unit tests (KafkaOps.Proof / ServiceTests)

- **Planner:**
  - eligibility, stop-set derivation, the cap, and refusal of compacted topics
  - RF and `min.insync.replicas`, and the `auto.create` warning
  - unknown active group refused; inactive group with lag needs a typed acknowledgment
  - the size cap and backup skip, and the plan hash changing on material facts
  - Disabled refuses, and unknown reassignment state refuses
  - the in-memory store refuses (except LocalCompose), and the shared principal refuses
- **State machine:**
  - For every step: run it twice and get the same result.
  - For every step: inject a crash between the action and the checkpoint, and resumption must reach the same end state.
  - A stale fencing token can't write.
  - Abort at each A and B step leads to rollback with T untouched. The temp topic is kept and `BackupCleanupRequired` is set.
  - The H1 timeout triggers rollback and does not delete the temp topic.
  - Foreign-topic detection at C1 and C3.
  - Recover-original at C2, C3, and C4 recreates T empty at N and writes offset 0 only when every high watermark is 0 and the groups are Empty. It deletes the new T only when its id is the recorded one, every high watermark is 0, and the action is typed. If any high watermark is above 0, it does not write offsets and does not delete the topic.
  - Nothing is deleted after C1 without a typed action. Failure, abort, timeout, and rollback do not delete the temp topic.
  - The timeouts. B6 and B7 time out into rollback and keep the temp topic. There is no second copy budget.
- **Copier:**
  - The partition function equals librdkafka `murmur2_random` for 10,000 golden JSON keys. The test produces with Confluent.Kafka to an M-partition topic and compares. It also equals the Java murmur2 vectors.
  - Order is kept per key, and null keys go to `sourcePartition % M`.
  - Headers, CreateTime, and the digest are checked. A digest mismatch fails B7 and deletes nothing.
  - Resume of the backup copy skips identities already on the temp topic. It does not write the new T and does not delete the temp topic. A temp-topic record with no `x-link-migration` header, or with another migration id, stops in NeedsAttention.
  - The offset write is 0 on every partition. It is refused when any high watermark is above 0, and the topic is not deleted in that case.
- **Approvals:**
  - The requester can't approve or execute.
  - The plan hash is bound to the approval.
  - Typed confirmations at request, H1, recover, and foreign delete.
  - `CanMigrateKafkaTopics` is enforced, with 403 before 404.
- **Catalog guard:** an unlisted producer or consumer fails the test.
- **Mutation run on the key guards,** as in the earlier console proofs. Survivors are listed as test gaps.

### 10.2 Real-broker proof (box cluster)

The pass criteria are absolute:

| # | Scenario | Pass when |
|---|---|---|
| P1 | 3 to 12 on ResourcesNormalized under live load: 2 producer replicas, 10,000 JSON patient keys at about 200 msg/s, consumer group `measureeval` with 2 replicas, plus a second catalog group | Zero loss: the set of acked produced (key, seq) equals the consumed set, per group. **Zero duplicates.** Seq is strictly increasing per key per group with no gaps. T has 12 partitions, configs equal the snapshot, and RF is unchanged. The new topic is empty. Every group offset is 0, and every high watermark is 0. The backup holds every pre-migration record in per-key order and is still present. A real backup is required. The window is recorded and within 1.5x the estimate. |
| P2 | Abort at H1; drain timeout forced with a stuck consumer; H1 timeout | On failure or abort before the delete, T is untouched (same topic id and partition count). The backup is kept and flagged `BackupCleanupRequired`. It is not deleted. Workloads are back at their recorded replicas. Zero loss and zero duplicates. |
| P3 | Admin.BFF killed (`kill -9`) at A2, B1, B3, B6 (mid-copy), H1, C1, C3, C5, D1, D2 | It resumes after restart. Resume does not duplicate backup records and does not delete the temp topic or the new T. There is no kill during a copy onto T, because that copy does not exist. When the run is allowed to finish, the result matches P1. |
| P4 | Killed after C1; recover with original N | The new T is deleted only when its id is the recorded one, every high watermark is 0, and the action is typed. T is recreated with the original partition count and is empty. Offsets are 0. If any high watermark is above 0, offsets are not written and the topic is not deleted. The backup is kept. Pre-migration records stay on the backup and are not reprocessed. Zero loss and zero duplicates. Services are back. |
| P5 | A broker variant with `auto.create.topics.enable=true` and a stray producer started during C2 | C3 reports a foreign topic and stops. Nothing is deleted without a typed action. No data loss. The typed delete of the empty foreign topic, by a second person, then forward, succeeds. The backup is kept. The new topic is empty and every group offset is 0. |
| P6 | A .NET consumer built on `BaseListener` while T is deleted (a demonstration, not a migration) | The listener stops and does not come back until restart. This confirms the stop-set rule. P6 is not a migration. |
| P7 | Two harness hosts sharing one Redis | One executor lease. Only one host runs steps. The second logs that the lease is held. No double action. |
| P8 | Redis flushed at B4 | The journal restores the record after Redis is flushed, and the migration completes as in P1 |
| P9 | `create-topics-rest.sh` run during the C2 window (sync simulation) | Treated as P5 (foreign topic). Nothing is deleted without a typed action. The backup topic survives. |

The clean path is documented as **zero duplicates by construction**. The only possible duplicate source is a consumer killed mid-message during B4. That is ordinary at-least-once delivery, bounded by one message per owned partition for .NET one-at-a-time listeners, and by the unacked queue for Java async consumers. The B3 drain gate makes it zero in practice, and the P2 and P3 runs assert it.

---

## 11. Proof steps on the box

These run after slice 1 is built. Nothing here touches a deployed host.

1. **Cluster:** `docker compose -p silo-bug-investigator-kafka -f /workspace/silos/bug-investigator/kafka-proof/compose.yaml -f <kit>/compose.migration.yml up -d`.
   - That is the controller plus kafka-1..3 (apache/kafka 4.3.1).
   - The override from `Scripts/kafka-migration-proof/` adds: Redis; `mig-producer` (2 replicas; persists its next seq per key on a volume); `mig-consumer-measureeval` (2 replicas, group `measureeval`; writes key, seq, partition, and offset to a per-group log and commits after each write); `mig-consumer-g2` (second group); and `mig-stray-producer` (profile `stray`).
2. **Seed:** create the 59 `topics.txt` topics with RF 3, as in `reproof-b1a3/seed.sh`, and the per-service retry and redrive topics from `kafka-retry-services.txt`.
3. **Harness host:** Admin.BFF KafkaOps endpoints, as in `reproof-b1a3/http-host`, with:
   - `KafkaOps:InfraProvider=LocalCompose`, `AllowTopicMigration=true`
   - `Workloads=Normalization=mig-producer,measureeval=mig-consumer-measureeval`
   - Redis on, header auth for users alice (request), bob (approve), and carol (execute)
4. **Start the load** and wait 2 minutes for a lag baseline.
5. **P1:** plan, request (alice, typed name), approve (bob), execute (carol), H1 go (carol, typed name), and wait for Done. Then run `check-migration.py`: loss, duplicates, and per-key order per group; the new T is empty; partition count is 12; every group offset is 0 and every high watermark is 0; the backup holds every pre-migration record in per-key order and is still present. A real backup is required. Also check `kafka-topics.sh --describe --topic ResourcesNormalized` and `kafka-consumer-groups.sh --describe --group measureeval`.
6. **P2 to P9** with `run-migration-proof.sh p2..p9`. The script calls the Admin.BFF harness and prints PASS or FAIL against the table in 10.2. P2 expects T untouched and the backup kept and flagged. P3 expects resume with no duplicate backup record and no delete of the temp topic or the new T. P4 expects the original partition count, an empty topic, offsets 0, and the backup kept. P5 and P9 expect a foreign topic and no delete without a typed action. P6 is the listener demonstration, not a migration. P7 expects one executor lease. P8 expects the journal to restore the record after Redis is flushed. Evidence is saved under `kafka-console-proof/migration-<sha>/`.
7. **Clean up:** `down -v --remove-orphans`, confirm the volume list is unchanged, and confirm `/etc/hosts` is unchanged.

---

## 12. Risks that remain

- **The stop window is real downtime** for the stopped workloads. Upstream topics build lag and catch up afterwards. The wizard shows the stop set and the estimate before anyone approves.
- **Catalog drift could hide a producer.** The guard test (9.1, item 1) and the B2 frozen-watermark check cover this: an unknown producer moves the HW, and the migration rolls back.
- **The deployed environment differs from the box** (Topic Operator, auto-create, ACLs). These are the DevOps confirmations in 6.4. Each is detected or refused at preflight.
- **RF 1 topics** (if deployed matches `topics.txt`): the backup topic gets a higher RF where brokers allow, but the recreated T keeps T's RF. Raising RF stays with DevOps.

---

## 13. Decision needed from the architect

**Approve one deviation from the stated method.** The architect's method moves traffic to the temp topic and back. This design moves **data** to the temp topic (a verified backup, re-keyed to M partitions) but **not traffic**. Instead:
- The topic's producers and consumers stop for a bounded window.
- T is drained, backed up, deleted, and recreated with M partitions under the same name. The recreated topic starts empty. Offsets are 0. The backup is kept for replay and is not copied onto T.

The reason is that Link services can't switch topic names at runtime (2.1). Moving traffic would need two coordinated redeploys of every producer and consumer per migration, or a new routing layer in .NET and Java.

If the architect requires zero-stop migrations, the alternative is to fund that routing layer first (3.1). Everything else in this document is decided.

## 14. Corrections from the broker proof

These replace the earlier wording where they differ.

- The backup reader sets `group.id` to `link-kafka-ops-migration-reader` and does not commit.
- An absent topic, including `DescribeTopicsException` when every topic is `UnknownTopicOrPart`, is described as missing.
- Journal restore assigns `Offset.Beginning`. Reconcile is retried until it succeeds.
- Preflight does not count the migration's own record as another open migration.
- The hold point is ticked. Its timeout and abort roll back. A waiting tick does not append a timeline note.
- NeedsAttention accepts forward, original, and delete-foreign. Delete-foreign of an empty topic does not finish the migration. Forward against a topic that has records returns 400 and names `kafka-topics.sh --delete`. Recover-original deletes an empty new topic only for a second person who is not the executor.
- A group with no committed offsets has no lag. `Dead` is treated as `Empty`. A group that never committed does not have to be `Stable` before consumers are started.
- Operator commands wait up to 8 seconds for the executor lease, then return 409 with `Retry-After`. Go runs one step and continues in the background. The lease is released when the process stops.
- Recreate passes only `DynamicTopicConfig` overrides. The backup topic sets `min.insync.replicas` to min(2, replication factor).
- After a Redis flush, a fence below the stored record fence is raised to that fence and the save returns 409.
- Holds are read from the migration store, so a second host sees them.
- Backup verify requires a read through the frozen end on every partition, equal counts, and an equal order-sensitive digest.
- The request is stored only when the client sends the dry-run plan hash and it matches.
- Abort, go, and recover on a finished migration are rejected.
- The catalog guard treats the shared retry, redrive, and error producers in `DeadLetterExceptionHandler.cs`, `RetryJob.cs`, and `TransientExceptionHandler.cs` as covered sibling helpers. Any other unbound produce still fails the guard.
- `cleanup.policy` and `min.insync.replicas` are read from the effective config row. A broker default of `delete` is allowed. A missing `min.insync.replicas` refuses the plan. Those rows are not copied onto the new topic unless the topic set them dynamically.
- The backup digest orders records by source partition and source offset. Backup offsets are not the sort key.
- Groups that have commits are recorded while the old topic still exists, from B4 through the delete. C5 writes offset 0 for every group on that list. A group that never had commits is not given an offset. Deleting the topic drops the broker's saved offsets, so the live describe is not the source of that list. A repeated B6 tick does not append another "Started B6" line. A skipped backup does not say a temp topic was kept.
