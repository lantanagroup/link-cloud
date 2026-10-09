# Kafka topic migration

Operator runbook for repartitioning one Link topic from N partitions to M partitions and keeping the topic name. Admin.BFF runs the steps. Link.UI is the console. The design is [kafka-topic-migration-design.md](kafka-topic-migration-design.md). The in-place partition add is [kafka-partition-operations.md](kafka-partition-operations.md).

Traffic never moves to the temp topic. The recreated topic starts empty. Every discovered group is written to offset 0. The `_linkmig-` backup is kept until a person confirms cleanup or the backup retention expires. The backup is not copied onto the new topic.

## When to migrate, and when to add partitions in place

Use **Add partitions** when all of the following are true:

- The change is an increase, and M is within `KafkaOps:MaxPartitionsPerTopic`.
- The topic can sit at zero lag with no produce traffic for the quiet window.
- Retained records can stay on their current partitions. New records use M. For keyed topics the share of keys that move is `1 - N/M`.
- Shrinking the topic later is not required. Kafka cannot undo an in-place add.

The dry run names the keyed producers (workload and source file), the key shape, and that share.

Use **this migration** when any of the following are true:

- The retained records must be re-hashed to M and kept on a verified backup for replay.
- The run needs a full rollback up to the moment T is deleted.
- A decrease is required. Slice 1 does not decrease. A decrease waits for slice 2.
- The quiet window cannot be met while the producers stay up.

Slice 1 is increase only. The new topic starts empty. Putting the backup onto T is not part of this runbook. `DataAcquisitionRequested` is eligible. It is not hard-blocked.

## Eligible topics

Slice 1 migrates these 16 topics:

- PatientCensusScheduled
- PatientListsAcquired
- CernerPatientsAcquired
- PatientEvent
- DataAcquisitionRequested
- ReadyToAcquire
- ResourcesAcquired
- MappingOutcomeEvaluated
- ResourcesNormalized
- EvaluationRequested
- MeasureReportGenerated
- ReadyForValidation
- ValidationComplete
- SubmitPayload
- PayloadSubmitted
- NotificationRequested

Do not migrate these in slice 1:

- ReportScheduled, GenerateReportRequested, and AuditableEventOccurred. Their producers include Tenant or Account. They wait for a runtime produce-hold (slice 2).
- RetentionCheckScheduled. It has no consumer.
- Service-Healthcheck. It is never migrated.
- Retry, redrive, and error topics. Java `{T}-Retry` and `{T}-Error` are grown to at least M before the stop. They are not recreated. That growth is kept even if the migration rolls back. .NET per-service retry and redrive topics are left as they are.

## Gates

The plan refuses when any gate fails. An unknown answer is a refusal.

- The caller has `CanMigrateKafkaTopics`. Viewing the timeline needs `CanViewInfrastructure`. `IsLinkAdmin` does not grant the migration actions.
- `KafkaOps:AllowTopicMigration` is true. The default is false in every environment. Production is also read-only unless `KafkaOps:ReadOnly` is false.
- The infrastructure provider is LocalCompose or Strimzi. The disabled provider refuses, because it cannot show that no reassignment is in flight.
- Outside LocalCompose, Admin.BFF uses the dedicated KafkaOps connection (`KafkaOps:Connection`), the `link-ops` principal. The shared service principal is refused. Credentials are Key Vault references. They are not written into this repository.
- The operations store is durable. An in-memory store is refused except on LocalCompose.
- No other change request or migration is open in the environment. No reassignment is in flight.
- Strimzi is not reconciling T. `KafkaOps:TopicOperatorManagesLinkTopics` is false, and no `KafkaTopic` resource is named T.
- `cleanup.policy` is `delete`, including a topic that does not set the key and inherits the broker default. A missing `min.insync.replicas` refuses the plan. It is not treated as 1. Compacted topics are refused in slice 1. A catalog group with no commits is not given an offset and does not have to become Stable.
- M is greater than N and within the cap.
- Every discovered group with members maps to a workload in the stop set. An inactive group that still has lag must be acknowledged by typing its name. Its offsets on T are then dropped.
- The estimated drain is under 5 minutes. The estimated backup fits in `KafkaOps:MigrationMaxBackupMinutes`, or backup is explicitly skipped. Skip needs a typed acknowledgment from the requester and the approver. Skip leaves no backup. The new topic is still empty, and the offsets written later are still 0. The proof requires a real backup.

Freeze `kafka-topics-sync` for the window. Between the delete and the recreate, a sync run from a branch that deletes unknown topics would recreate T at 3 partitions. The recreate step treats any unexpected topic id as a foreign topic.

## Approvals

Four people are not required. Three duties are. The requester cannot approve and cannot execute.

| Point | Who | What they type or bind |
|---|---|---|
| Request | Requester, with `CanMigrateKafkaTopics` | The topic name, a reason, target M, and the plan hash from the dry run. |
| Approve | A different person | The same dry run. The approval is bound to the plan hash. A change of partition count, stop set, discovered groups, size class, or siblings requires a new approval. |
| Execute | Not the requester | Starts the state machine. |
| H1 go | The executor | The topic name again. This is the last point before T is deleted. |
| Recover forward or recover original | Not the requester | The topic name. |
| Delete a foreign topic | A second person, not the executor | The topic name. The topic must be empty (every high watermark 0). |
| Delete the temp topic | `CanMigrateKafkaTopics` | The temp topic name. Before `KafkaOps:MigrationBackupRetentionHours` expires, a second person must approve. |

One migration is open per environment. It blocks broker moves until it reaches Done, RolledBack, or Rejected.

## Holds

Admin.BFF and Link.UI are not part of the stop set. They are not scaled to zero. While the hold for T is set:

- Admin.BFF integration commands that produce to T return 409 and name the migration.
- The Admin.BFF integration consumer does not subscribe to T.
- Link.UI asks Admin.BFF for active holds and refuses an automation run that would produce to a held topic.

The console audit producer writes to `AuditableEventOccurred`, which is not eligible in slice 1.

## Steps

The console shows the same order. Workloads stay stopped from B1 until D2. The temp topic is `_linkmig-<topic>-<id8>`. The order is A1-A4, B1-B7, H1, C1, C2, C3, C4, C5, D1, D2, D3, Done.

1. **A1 Preflight.** Read-only. Compare the live plan hash with the approved hash.
2. **A2 Create the temp topic** at M partitions. Adopt it if the name, topic id, and spec already belong to this migration. Replace it only when it is empty (every high watermark is 0) and the name and id belong to this migration. Any other topic is left in place.
3. **A3 Grow pinned siblings** to at least M. Java retry and error topics must be grown before measureeval or validation consumes the new T. Growth is not reversed.
4. **A4 Arm holds.**
5. **B1 Stop producers.** Record the replica count first. A resume does not overwrite that count. Scale to 0, or complete the manual checklist.
6. **B2 Quiet window.** High watermarks on T must not move for twice the metadata refresh (60 seconds).
7. **B3 Drain.** Every discovered group's committed offset equals the high watermark on every partition. Two reads, at least 10 seconds apart. Every message is consumed before the later delete.
8. **B4 Stop consumers.** Every discovered group is Empty.
9. **B5 Freeze.** Record the topic id, offsets, configs, and replication factor.
10. **B6 Backup copy.** This is the only copy. Raise `retention.ms` on the temp topic before the first write. The floor is max(frozen `retention.ms`, age of the oldest CreateTime + `MigrationBackupRetentionHours`). Do not lower it afterward. Set `min.insync.replicas` to min(2, replication factor). The copy is verified only when every source partition was read through its frozen end and the per-partition counts and order-sensitive digests, ordered by source offset, match the backup. A short read is retried. A mismatch stops before the delete. Copy T into the temp topic with `toPositive(murmur2(key)) % M`. A null key uses `sourcePartition % M`. Keep key, value, headers, and CreateTime. Stamp `x-link-migration: {migrationId};{sourceTopic};{sourcePartition};{sourceOffset}`. The producer is idempotent and uses `acks=all`. Resume reads the temp topic, skips identities already present for this migration, and does not delete the temp topic. It does not write the new T. A record with no migration header, or with another migration id, stops in NeedsAttention.
11. **B7 Verify the backup.** Counts and the per-partition order-sensitive digest match a fresh read of the temp topic.
12. **H1 Hold.** The executor types the topic name. The default hold is 15 minutes. If it expires, the migration rolls back and keeps the temp topic.
13. **C1 Delete T.** This is the point of no return. Re-check the frozen id and the verified backup first. A different topic id is a foreign topic and stops the run. Nothing else is deleted.
14. **C2 Wait** until T is absent on every broker.
15. **C3 Recreate T** at M partitions (N if the recovery choice is original), with the frozen replication factor and the frozen configs, including the frozen `retention.ms`. Record the new topic id. Do not write records to it. An unknown id is a foreign topic.
16. **C4 Verify the new T is empty.** Partition count, replication factor, full ISR, frozen configs including `retention.ms`, and every high watermark is 0. A high watermark above 0 stops in NeedsAttention and does not delete T.
17. **C5 Offsets at 0.** While every group is Empty and every high watermark is 0, set each group's committed offset on every partition to 0, then read the offsets back. Do not write a high watermark. If any high watermark is above 0, do not write offsets and do not delete the topic. Consumers then start on an empty topic.
18. **D1 Start consumers.** Every group is Stable and every partition of T is assigned.
19. **D2 Release holds and start producers.**
20. **D3 Verify** for 15 minutes. Lag must not climb past the baseline, and `{T}-Error` must not advance unexpectedly.

An interrupted backup resumes from the migration header on the temp topic. The temp topic is not deleted to start the copy over, and the resume does not write the new T.

## Rollback before C1

Abort, a failed A or B step, a B6 or B7 timeout, and the H1 timeout roll back.

- T keeps its original topic id and partition count.
- Consumer replicas are restored, holds are released, then producer replicas are restored.
- Consumers resume from the offsets they had committed.
- The temp topic is kept. The record is flagged `BackupCleanupRequired`.
- Sibling partition growth is kept.

A record on the temp topic that has no `x-link-migration` header, or that belongs to another migration, does not roll back by itself. The run stops in NeedsAttention, T is not deleted, and the temp topic is not deleted. Rollback remains available and still keeps the temp topic.

## After C1

Nothing is deleted automatically. A failed recreate, a high watermark above 0, or a timeout in C or D stops in **NeedsAttention**.

Two recoveries are on the page. Both require the typed topic name. The requester cannot run them. Neither recovery copies the backup onto T.

**Recover forward.** Continue at M. Finish C4 on the empty topic and C5 so every offset is 0. Do not delete the new T if it already has the recorded id. If any high watermark is above 0, do not write offsets and do not delete the topic.

**Recover original.** Recreate the original partition count empty. Write offset 0 only when every high watermark is 0 and the groups are Empty. Delete the new T only when all three are true: its topic id is the recorded id, every high watermark is 0, and the action was typed. If any high watermark is above 0, do not write offsets and do not delete the topic. The run stays in NeedsAttention.

A foreign topic (auto-create, or a sync run during C2) is shown with its partitions, configs, and high watermarks. Delete it only when it is empty, by typing its name, and only by a second person who is not the executor. That delete does not finish the migration. Choose forward or original afterwards. A foreign topic that already has records is not deleted by the console. Remove it with `kafka-topics.sh --delete --topic <name>`, then choose forward. Forward returns 400 and names that command while the records are still there. Nothing is deleted without a typed action. Recover original deletes an empty new topic only for a person who is not the requester and not the executor.

Going from M back to N after D1 is a new migration. Slice 1 will refuse the decrease.

## Backup cleanup

The temp topic is the verified backup. It is not deleted when a step fails, when an operator aborts, when a step times out, or when the migration rolls back.

It stays until one of these happens:

- A person with `CanMigrateKafkaTopics` confirms cleanup and types the temp topic name. If `KafkaOps:MigrationBackupRetentionHours` (default 168) has not expired, a second person must approve.
- That retention has expired, and the migration is already Done or RolledBack. Expiry does not delete the topic while the migration is open or in NeedsAttention.

`retention.ms` on the temp topic is raised so CreateTime is not past the retention window when the backup copy runs. Do not lower that value afterward. The recreated topic keeps the frozen `retention.ms`. It is not raised, because nothing is written to it.

## Timeouts

Defaults. Override them with `KafkaOps:Migration*`. The window estimate counts the backup once: stop + quiet + drain + stop consumers + one backup + hold + delete/create + verify empty + offset write of zeros + start. There is no second copy budget. An alert fires at 1.5 times the estimate.

| Steps | Timeout | Result |
|---|---|---|
| A1 | 2 min | Refuse. T is unchanged. |
| A2, A3 | 1 min each | Roll back. Temp topic kept. |
| B1, B4 | 5 min each | Roll back. Temp topic kept. |
| B2 | 60 s quiet window, 5 min maximum | Roll back. Producers did not stop. Temp topic kept. |
| B3 | 10 min | Roll back. Drain did not finish. Temp topic kept. |
| B6 + B7 | `MigrationMaxBackupMinutes` (15) | Roll back. Temp topic kept and flagged. |
| H1 | 15 min | Roll back. Temp topic kept and flagged. |
| C1, C2 | 2 min | NeedsAttention. Nothing deleted. |
| C3, C4, C5 | 2 min | NeedsAttention. Nothing deleted. Offsets stay unwritten unless every high watermark is 0. |
| D1, D2 | 10 min each | NeedsAttention. The page names which workloads are up. |
| D3 | 15 min | Done, or NeedsAttention. |

## Strimzi manual checklist

Use this when the Strimzi provider can read the cluster but cannot patch Deployments (`CanScaleWorkloads` is false). The console still decides each gate from the broker. A typed confirmation does not replace that check.

Before execute:

1. Confirm no `KafkaTopic` resource is named T, and that the Topic Operator will not recreate Link topics.
2. Confirm `auto.create.topics.enable` is false, or accept the plan warning. C3 still rejects a foreign topic id.
3. Freeze `kafka-topics-sync` and any GitOps controller that sets replicas on the stop set.
4. If a scaler (including KEDA) can raise replicas, pause it by hand for each stop-set workload. KEDA `paused-replicas` is not applied by slice 1.

During B1 and B4 the page lists each workload and the recorded replica count:

5. Scale that Deployment to 0.
6. Type the workload name on the manual step.
7. Wait. The broker must show high watermarks frozen (B2), lag 0 (B3), and every group Empty with no members (B4). If GitOps raises a replica before C1, the migration rolls back and keeps the temp topic.

During C2:

8. Do not create T. Do not run topic sync. If something creates T, leave it. The page reports a foreign topic. Delete an empty one through the typed, second-person action. If it already has records, delete it with `kafka-topics.sh --delete --topic <name>` and then choose forward.

After C5 the page lists the recorded replica counts. C5 has written 0 on every partition of the empty topic.

9. Scale consumers back first. Type each workload name. The broker must show the group Stable, with every partition of T assigned.
10. Scale producers back. Type each workload name. Then the holds release.

After C1, a workload that comes back on its own stops the run in NeedsAttention. Do not delete T or the temp topic to fix that. Use recover forward or recover original. Both leave the topic empty and leave the backup in place.

## What to watch

The timeline shows the frozen offsets, the backup counts and digest, the stop set with live replica counts, and the H1 countdown. Every transition is also written to `AuditableEventOccurred` and to `_linkmig-journal`.

Stop when the page says NeedsAttention. Do not delete topics from the Kafka CLI to hurry a step. The CLI delete is only for a foreign topic that already has records: `kafka-topics.sh --delete --topic <name>`, then choose forward. The other safe deletes are the ones the page offers: an empty foreign topic (typed, second person, and that delete does not finish the migration), an empty new T on a typed recover-original by a person who is not the executor, and the temp topic after cleanup is confirmed.
