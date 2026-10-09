# Kafka topic migration proof

This kit proves the temp-topic migration against the bug-investigator Kafka cluster. It does not start that cluster by itself. `compose.migration.yml` is an override. The recreated topic stays empty, every group offset is 0, and the `_linkmig-` backup is kept. The kit does not copy that backup onto the recreated topic.

The proof is not run on the build machine. `run-migration-proof.sh` refuses to start unless `KAFKA_MIGRATION_PROOF_ALLOW=1`. On `DESKTOP-5NA82VF` it also requires `KAFKA_MIGRATION_PROOF_ALLOW_ON_THIS_PC=1`.

The script calls the Admin.BFF KafkaOps harness. It does not reimplement the migration.

## Environment

| Variable | Purpose |
|---|---|
| `KAFKA_BOOTSTRAP` | Broker address for a client on the compose network. Containers default to `kafka-1:9092`. |
| `KAFKA_BOOTSTRAP_INTERNAL` | Address used by `docker compose exec`. Default `kafka-1:9092`. |
| `KAFKA_MIGRATION_PROOF_ALLOW` | Must be `1` or the runner refuses. |
| `KAFKA_MIGRATION_PROOF_ALLOW_ON_THIS_PC` | Must be `1` on `DESKTOP-5NA82VF`. |
| `ADMIN_BFF_URL` | Harness base URL. Default `http://127.0.0.1:5100`. |
| `ADMIN_BFF_URL_2` | Second harness host. Required for p7. |
| `ADMIN_BFF_CONTAINER` | Container name killed for p3, p4, and the resume checks. |
| `ADMIN_BFF_CONTAINER_2` | Second host, required for p7. Its log must say the lease is held. |
| `PROOF_USER_HEADER` | User header. Default `X-Proof-User`. Values are `alice` (request), `bob` (approve and foreign-topic delete), `carol` (execute, go, recover). |
| `TOPIC` | Default `ResourcesNormalized`. |
| `TARGET_PARTITIONS` | Default `12`. |
| `ORIGINAL_PARTITIONS` | Default `3`. Used by p4. |
| `COMPOSE_PROJECT` | Default `silo-bug-investigator-kafka`. |
| `KAFKA_PROOF_BASE_COMPOSE` | Base file. Default `/workspace/silos/bug-investigator/kafka-proof/compose.yaml`. |
| `KAFKA_PROOF_OVERRIDE` | This override. |
| `KAFKA_DOCKER_SERVICE` | Broker service used for admin tools. Default `kafka-1`. |
| `MIG_TOPIC`, `MIG_RATE`, `MIG_KEYS` | Load generator. Defaults: `ResourcesNormalized`, 100 messages/s per producer replica, 10000 keys. Two replicas are about 200 messages/s. |
| `KAFKA_REST_PROXY`, `KAFKA_REST_USER`, `KAFKA_REST_PASSWORD` | Optional. p9 uses `Scripts/create-topics-rest.sh` when the proxy is set. Otherwise it creates the topic through the broker during C2. |
| `EVIDENCE_DIR` | Default `kafka-console-proof/migration-<sha>/<scenario>` under the repo. |
| `PROOF_HOLD_TIMEOUT_SECONDS` | How long p2 waits for the H1 timeout. Default 1000. |
| `PROOF_DRAIN_TIMEOUT_SECONDS` | How long p2 waits for the drain timeout. Default 700. |
| `PROOF_RUN_TIMEOUT_SECONDS` | Overall wait for one drive. Default 1800. |

Harness settings for this kit:

- `KafkaOps:InfraProvider=LocalCompose`
- `KafkaOps:AllowTopicMigration=true`
- Redis on the `redis` service
- `KafkaOps:Workloads=Normalization=mig-producer,measureeval=mig-consumer-measureeval,g2=mig-consumer-g2`

`g2` is the second consumer group. The harness must map it onto `mig-consumer-g2` or preflight refuses an active group that is not in the stop set.

## Services

The base file provides `controller`, `kafka-1`, `kafka-2`, and `kafka-3`. This override adds:

| Service | Role |
|---|---|
| `redis` | Lease and migration record. No host port. |
| `mig-producer` | 2 replicas. Persists the next seq per key on `mig-seq`. Appends acks to `mig-logs`. |
| `mig-consumer-measureeval` | 2 replicas, group `measureeval`. Writes `key,seq,partition,offset` and commits after each write. |
| `mig-consumer-g2` | One replica, group `g2`. Same commit rule. |
| `mig-stray-producer` | Profile `stray` only. Produces during the C2 window for p5. |

## Commands

From the repo root, on the proof host:

```bash
docker compose -p silo-bug-investigator-kafka \
  -f /workspace/silos/bug-investigator/kafka-proof/compose.yaml \
  -f Scripts/kafka-migration-proof/compose.migration.yml \
  up -d --scale mig-producer=2 --scale mig-consumer-measureeval=2

export KAFKA_MIGRATION_PROOF_ALLOW=1
export ADMIN_BFF_URL=http://127.0.0.1:5100
Scripts/kafka-migration-proof/run-migration-proof.sh p1
```

The runner's last line is `PASS` or `FAIL`. p2 through p9 use the same command with that argument. Each scenario calls the harness (`plan`, `migrations`, `approve`, `execute`, `go`, `abort`, `recover`) and then `check-migration.py` where the scenario has data to check.

`check-migration.py` reads evidence files. It exits non-zero when a check fails. A direct run looks like:

```bash
python3 Scripts/kafka-migration-proof/check-migration.py \
  --producer-acks evidence/producer-acks.jsonl \
  --consumer-log evidence/consumer-measureeval.jsonl \
  --consumer-log evidence/consumer-g2.jsonl \
  --pre-migration evidence/pre-migration.jsonl \
  --topic-dump evidence/topic-dump.jsonl \
  --backup-dump evidence/backup-dump.jsonl \
  --backup-digest evidence/backup-digest.json \
  --groups-describe evidence/measureeval.txt \
  --group measureeval \
  --partitions 12 \
  --require-empty --require-backup --require-offsets --require-digest
```

`--require-empty` fails when the recreated topic has any record. `--require-backup` fails unless the backup holds every pre-migration record once, in per-key order. `--require-offsets` fails unless every committed offset is 0 and every high watermark is 0.

## Pass criteria

| Scenario | Pass |
|---|---|
| p1 | No loss, no duplicates, per-key seq is contiguous. The new topic has 12 partitions and is empty. Every group offset is 0. The backup holds every pre-migration record in per-key order. A real backup is required. The temp topic is still present. |
| p2 | Abort at H1, a drain timeout from a paused consumer, and the H1 timeout. On failure or abort before the delete, T keeps its topic id and partition count. The backup is kept and `BackupCleanupRequired` is set. It is not deleted. No loss and no duplicates. |
| p3 | Kill Admin.BFF at A2, B1, B3, B6 (mid-copy), H1, C1, C3, C5, D1, and D2. Resume does not duplicate backup records and does not delete the temp topic or the new T. A finished run matches p1. |
| p4 | Kill after C1, abort into NeedsAttention, recover original. T is recreated with 3 partitions and is empty. Offsets are 0. The temp topic is kept. Pre-migration records stay on the backup and are not reprocessed. |
| p5 | Stray producer during C2. The harness reports a foreign topic and stops. Nothing is deleted without a typed action. A second person deletes the foreign topic only if it is empty, then forward completes. The new topic is empty, every group offset is 0, and the backup is kept. |
| p6 | Demonstration, not a migration. The harness runs a `BaseListener` consumer, deletes the topic, and reports that the listener stopped and did not come back. |
| p7 | One executor lease. Two harness hosts share one Redis. The second request is refused. That host's log says the lease is held. Only one host runs steps. |
| p8 | Redis `FLUSHALL` at B4. The journal restores the record, and the run finishes like p1: empty topic, offsets 0, backup kept. |
| p9 | Topic create during C2 (sync simulation). Same foreign-topic result as p5. Nothing is deleted without a typed action. The backup topic survives. |

## Cleanup

```bash
docker compose -p silo-bug-investigator-kafka \
  -f /workspace/silos/bug-investigator/kafka-proof/compose.yaml \
  -f Scripts/kafka-migration-proof/compose.migration.yml \
  --profile stray down -v --remove-orphans
```

That removes the override's volumes, including the seq store and the consumer logs. It does not remove volumes that belong to some other compose project.
