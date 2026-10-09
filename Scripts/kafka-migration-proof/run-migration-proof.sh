#!/usr/bin/env bash
# Calls the Admin.BFF KafkaOps harness for one proof scenario (p1..p9).
# This script is not run on the build machine. It refuses unless
# KAFKA_MIGRATION_PROOF_ALLOW=1. On DESKTOP-5NA82VF it also requires
# KAFKA_MIGRATION_PROOF_ALLOW_ON_THIS_PC=1.
# The last line is PASS or FAIL.

set -euo pipefail

scenario="${1:-}"
case "$scenario" in
  p1|p2|p3|p4|p5|p6|p7|p8|p9) ;;
  *)
    echo "Usage: run-migration-proof.sh p1|p2|p3|p4|p5|p6|p7|p8|p9" >&2
    echo "FAIL"
    exit 1
    ;;
esac

if [[ "${KAFKA_MIGRATION_PROOF_ALLOW:-}" != "1" ]]; then
  echo "Refusing to run. Set KAFKA_MIGRATION_PROOF_ALLOW=1 on the proof host." >&2
  echo "FAIL"
  exit 2
fi

host_name="$(hostname 2>/dev/null || true)"
if [[ "$host_name" == "DESKTOP-5NA82VF" && "${KAFKA_MIGRATION_PROOF_ALLOW_ON_THIS_PC:-}" != "1" ]]; then
  echo "This workstation is not the proof host. The kit stays stopped here." >&2
  echo "FAIL"
  exit 2
fi

here="$(cd "$(dirname "$0")" && pwd)"
repo="$(cd "$here/../.." && pwd)"
ADMIN_BFF_URL="${ADMIN_BFF_URL:-http://127.0.0.1:5100}"
ADMIN_BFF_URL_2="${ADMIN_BFF_URL_2:-}"
USER_HEADER="${PROOF_USER_HEADER:-X-Proof-User}"
TOPIC="${TOPIC:-ResourcesNormalized}"
TARGET="${TARGET_PARTITIONS:-12}"
ORIGINAL="${ORIGINAL_PARTITIONS:-3}"
PROJECT="${COMPOSE_PROJECT:-silo-bug-investigator-kafka}"
BASE_COMPOSE="${KAFKA_PROOF_BASE_COMPOSE:-/workspace/silos/bug-investigator/kafka-proof/compose.yaml}"
OVERRIDE="${KAFKA_PROOF_OVERRIDE:-$here/compose.migration.yml}"
BROKER_SERVICE="${KAFKA_DOCKER_SERVICE:-kafka-1}"
BROKER_BOOTSTRAP="${KAFKA_BOOTSTRAP_INTERNAL:-kafka-1:9092}"
SHA="$(git -C "$repo" rev-parse --short HEAD 2>/dev/null || echo nosha)"
EVIDENCE_DIR="${EVIDENCE_DIR:-$repo/kafka-console-proof/migration-$SHA/$scenario}"
CHECK="$here/check-migration.py"
POLL_SECONDS="${PROOF_POLL_SECONDS:-2}"
RUN_TIMEOUT="${PROOF_RUN_TIMEOUT_SECONDS:-1800}"
HOLD_TIMEOUT="${PROOF_HOLD_TIMEOUT_SECONDS:-1000}"
DRAIN_TIMEOUT="${PROOF_DRAIN_TIMEOUT_SECONDS:-700}"
mkdir -p "$EVIDENCE_DIR"

unpause_consumer() {
  if [[ -f "$EVIDENCE_DIR/paused-consumer.txt" ]]; then
    docker unpause "$(cat "$EVIDENCE_DIR/paused-consumer.txt")" >/dev/null 2>&1 || true
    rm -f "$EVIDENCE_DIR/paused-consumer.txt"
  fi
}
trap unpause_consumer EXIT

fail() {
  echo "$*" >&2
  echo "FAIL"
  exit 1
}

compose() {
  docker compose -p "$PROJECT" -f "$BASE_COMPOSE" -f "$OVERRIDE" "$@"
}

bff() {
  local base="$1" user="$2" method="$3" path="$4" body="${5:-}"
  local args=(-sS -H "$USER_HEADER: $user" -H "Content-Type: application/json" -X "$method" -w $'\n%{http_code}')
  if [[ -n "$body" ]]; then
    args+=(--data "$body")
  fi
  curl "${args[@]}" "${base}${path}"
}

py() {
  python3 -c "$1"
}

json_field() {
  local expr="$1"
  py "
import json,sys
data=json.load(sys.stdin)
value=data
for part in '''$expr'''.split('.'):
    if isinstance(value, dict) and part in value:
        value=value[part]
    elif isinstance(value, dict):
        low={str(k).lower(): k for k in value}
        if part.lower() in low:
            value=value[low[part.lower()]]
        else:
            value=None
            break
    else:
        value=None
        break
if isinstance(value, (dict, list)):
    print(json.dumps(value))
elif value is None:
    print('')
else:
    print(value)
"
}

split_response() {
  local raw="$1"
  RESP_BODY="${raw%$'\n'*}"
  RESP_CODE="${raw##*$'\n'}"
}

migration_step() {
  printf '%s' "$1" | json_field step
}

call_ok() {
  local user="$1" method="$2" path="$3" body="${4:-}"
  local raw
  raw="$(bff "$ADMIN_BFF_URL" "$user" "$method" "$path" "$body")" || fail "harness call failed: $method $path"
  split_response "$raw"
  printf '%s\n' "$RESP_BODY" > "$EVIDENCE_DIR/last.json"
  if [[ "$RESP_CODE" != 2* ]]; then
    fail "harness $method $path returned $RESP_CODE: $RESP_BODY"
  fi
}

wait_for_load() {
  local deadline=$((SECONDS + 180))
  while (( SECONDS < deadline )); do
    if compose exec -T mig-producer sh -c 'test -s /logs/producer-acks.jsonl'; then
      return 0
    fi
    sleep 2
  done
  fail "producer acks were still empty; start the load and wait for it"
}

open_migration() {
  local reason="$1"
  wait_for_load
  call_ok alice POST "/api/ops/kafka/topics/${TOPIC}/migrations/plan" "{\"targetPartitions\":${TARGET},\"reason\":\"${reason}\"}"
  local hash
  hash="$(printf '%s' "$RESP_BODY" | json_field planHash)"
  [[ -n "$hash" ]] || fail "plan response has no planHash"
  call_ok alice POST "/api/ops/kafka/migrations" "{\"topic\":\"${TOPIC}\",\"targetPartitions\":${TARGET},\"reason\":\"${reason}\",\"typedName\":\"${TOPIC}\",\"planHash\":\"${hash}\"}"
  MIG_ID="$(printf '%s' "$RESP_BODY" | json_field id)"
  [[ -n "$MIG_ID" ]] || fail "create response has no id"
  call_ok bob POST "/api/ops/kafka/migrations/${MIG_ID}/approve" "{}"
  call_ok carol POST "/api/ops/kafka/migrations/${MIG_ID}/execute" "{}"
  echo "$MIG_ID" > "$EVIDENCE_DIR/migration-id.txt"
}

fetch_migration() {
  local raw
  raw="$(bff "$ADMIN_BFF_URL" carol GET "/api/ops/kafka/migrations/${MIG_ID}" "")" || true
  split_response "$raw"
  if [[ "$RESP_CODE" != 2* ]]; then
    STEP=""
    return 1
  fi
  printf '%s\n' "$RESP_BODY" > "$EVIDENCE_DIR/migration.json"
  STEP="$(migration_step "$RESP_BODY")"
  return 0
}

send_go() {
  call_ok carol POST "/api/ops/kafka/migrations/${MIG_ID}/go" "{\"typedName\":\"${TOPIC}\"}"
}

topic_describe() {
  compose exec -T "$BROKER_SERVICE" /opt/kafka/bin/kafka-topics.sh --bootstrap-server "$BROKER_BOOTSTRAP" --describe --topic "$1" 2>/dev/null || true
}

topic_id_of() {
  topic_describe "$1" | python3 -c 'import sys,re
text=sys.stdin.read()
match=re.search(r"TopicId[:=]\s*(\S+)", text)
print(match.group(1) if match else "")'
}

topic_exists() {
  local listed
  listed="$(compose exec -T "$BROKER_SERVICE" /opt/kafka/bin/kafka-topics.sh --bootstrap-server "$BROKER_BOOTSTRAP" --list 2>/dev/null || true)"
  printf '%s\n' "$listed" | grep -qx "$1"
}

dump_topic() {
  local topic="$1" out="$2"
  compose exec -T -i -e "DUMP_BOOTSTRAP=${BROKER_BOOTSTRAP}" -e "DUMP_TOPIC=${topic}" mig-producer python -u - > "$out" <<'PY'
import base64, json, os
from kafka import KafkaConsumer, TopicPartition
bootstrap = os.environ["DUMP_BOOTSTRAP"]
topic = os.environ["DUMP_TOPIC"]
consumer = KafkaConsumer(bootstrap_servers=bootstrap, enable_auto_commit=False, consumer_timeout_ms=8000)
parts = sorted(consumer.partitions_for_topic(topic) or [])
assigned = [TopicPartition(topic, partition) for partition in parts]
if assigned:
    consumer.assign(assigned)
    consumer.seek_to_beginning(*assigned)
while assigned:
    batches = consumer.poll(timeout_ms=1500, max_records=500)
    if not batches:
        break
    for messages in batches.values():
        for msg in messages:
            headers = []
            for name, value in msg.headers or []:
                headers.append({"name": name, "valueBase64": base64.b64encode(value or b"").decode("ascii")})
            print(json.dumps({
                "partition": msg.partition,
                "offset": msg.offset,
                "timestampMs": msg.timestamp or 0,
                "key": (msg.key or b"").decode("utf-8", "replace"),
                "value": (msg.value or b"").decode("utf-8", "replace"),
                "headers": headers,
            }))
PY
}

describe_group() {
  local group="$1" out="$2"
  compose exec -T "$BROKER_SERVICE" /opt/kafka/bin/kafka-consumer-groups.sh \
    --bootstrap-server "$BROKER_BOOTSTRAP" --describe --group "$group" > "$out"
}

copy_log() {
  local src="$1" dest="$2"
  compose exec -T mig-producer cat "$src" > "$dest" 2>/dev/null || true
}

snapshot_c5() {
  local backup
  backup="$(json_field backupTopic < "$EVIDENCE_DIR/migration.json")"
  [[ -n "$backup" ]] || fail "migration record has no backup topic"
  dump_topic "$TOPIC" "$EVIDENCE_DIR/topic-dump.jsonl"
  dump_topic "$backup" "$EVIDENCE_DIR/backup-dump.jsonl"
  describe_group measureeval "$EVIDENCE_DIR/measureeval.txt"
  describe_group g2 "$EVIDENCE_DIR/g2.txt"
  cp "$EVIDENCE_DIR/measureeval.txt" "$EVIDENCE_DIR/c5-measureeval.txt"
}

capture_logs() {
  copy_log /logs/producer-acks.jsonl "$EVIDENCE_DIR/producer-acks.jsonl"
  copy_log /logs/consumer-measureeval.jsonl "$EVIDENCE_DIR/consumer-measureeval.jsonl"
  copy_log /logs/consumer-g2.jsonl "$EVIDENCE_DIR/consumer-g2.jsonl"
}

wait_bff() {
  local up_deadline=$((SECONDS + 120))
  until curl -sf -o /dev/null "$ADMIN_BFF_URL/health" || curl -sf -o /dev/null "$ADMIN_BFF_URL/"; do
    if (( SECONDS >= up_deadline )); then
      fail "Admin.BFF did not return after kill"
    fi
    sleep 2
  done
}

# Drive until Done, RolledBack, or NeedsAttention. Sends go at H1 unless NO_GO=1.
# RETURN_AT_H1 returns while the hold is still open.
# KILL_AT kills ADMIN_BFF_CONTAINER the first time that step is observed.
# STOP_AT returns once that step has been seen and any requested kill has finished.
# FLUSH_AT runs FLUSHALL on redis. PAUSE_AT pauses one measureeval consumer.
# ON_C2 runs that command the first time C2 is observed.
drive() {
  local deadline=$((SECONDS + RUN_TIMEOUT))
  local sent_go=0 killed=0 flushed=0 paused=0 c2_done=0 c5_done=0 captured=0
  topic_id_of "$TOPIC" > "$EVIDENCE_DIR/original-topic-id.txt" || true
  while (( SECONDS < deadline )); do
    if ! fetch_migration; then
      sleep "$POLL_SECONDS"
      continue
    fi
    echo "$STEP" >> "$EVIDENCE_DIR/steps.log"
    if [[ "$STEP" == "H1" && "$sent_go" -eq 0 ]]; then
      if [[ "$captured" -eq 0 ]]; then
        capture_logs
        cp "$EVIDENCE_DIR/producer-acks.jsonl" "$EVIDENCE_DIR/pre-migration.jsonl"
        captured=1
      fi
      if [[ "${RETURN_AT_H1:-}" == "1" ]]; then
        return 0
      fi
      if [[ "${NO_GO:-}" != "1" ]]; then
        if [[ "${KILL_AT:-}" == "H1" && "$killed" -eq 0 ]]; then
          [[ -n "${ADMIN_BFF_CONTAINER:-}" ]] || fail "ADMIN_BFF_CONTAINER is required"
          docker kill "$ADMIN_BFF_CONTAINER" >/dev/null
          killed=1
          wait_bff
        fi
        send_go
        sent_go=1
      fi
    fi
    if [[ -n "${KILL_AT:-}" && "$KILL_AT" != "H1" && "$STEP" == "$KILL_AT" && "$killed" -eq 0 ]]; then
      [[ -n "${ADMIN_BFF_CONTAINER:-}" ]] || fail "ADMIN_BFF_CONTAINER is required"
      json_field newTopicId < "$EVIDENCE_DIR/migration.json" > "$EVIDENCE_DIR/new-topic-id-before-kill.txt" || true
      docker kill "$ADMIN_BFF_CONTAINER" >/dev/null
      killed=1
      wait_bff
      if [[ "${STOP_AT:-}" == "$KILL_AT" ]]; then
        return 0
      fi
    fi
    if [[ "${PAUSE_AT:-}" == "$STEP" && "$paused" -eq 0 ]]; then
      local consumer
      consumer="$(compose ps -q mig-consumer-measureeval | head -n 1)"
      [[ -n "$consumer" ]] || fail "measureeval consumer container is not running"
      docker pause "$consumer" >/dev/null
      echo "$consumer" > "$EVIDENCE_DIR/paused-consumer.txt"
      paused=1
    fi
    if [[ "${FLUSH_AT:-}" == "$STEP" && "$flushed" -eq 0 ]]; then
      compose exec -T redis redis-cli FLUSHALL >/dev/null
      flushed=1
    fi
    if [[ "$STEP" == "C2" && -n "${ON_C2:-}" && "$c2_done" -eq 0 ]]; then
      eval "$ON_C2"
      c2_done=1
    fi
    if [[ "$STEP" == "C5" && "$c5_done" -eq 0 ]]; then
      snapshot_c5
      c5_done=1
    fi
    case "$STEP" in
      Done|RolledBack|Rejected) return 0 ;;
      NeedsAttention)
        if [[ "${UNTIL:-}" == "Done" ]]; then
          sleep "$POLL_SECONDS"
          continue
        fi
        return 0
        ;;
    esac
    sleep "$POLL_SECONDS"
  done
  fail "timed out waiting for a terminal step (last step ${STEP:-unknown})"
}

check_data() {
  python3 "$CHECK" \
    --producer-acks "$EVIDENCE_DIR/producer-acks.jsonl" \
    --consumer-log "$EVIDENCE_DIR/consumer-measureeval.jsonl" \
    --consumer-log "$EVIDENCE_DIR/consumer-g2.jsonl" \
    --pre-migration "$EVIDENCE_DIR/pre-migration.jsonl" \
    --topic-dump "$EVIDENCE_DIR/topic-dump.jsonl" \
    --backup-dump "$EVIDENCE_DIR/backup-dump.jsonl" \
    --backup-digest "$EVIDENCE_DIR/backup-digest.json" \
    --groups-describe "$EVIDENCE_DIR/c5-measureeval.txt" \
    --groups-describe "$EVIDENCE_DIR/g2.txt" \
    --group measureeval \
    --group g2 \
    --partitions "$1" \
    --require-empty \
    --require-backup \
    --require-offsets \
    --require-digest \
    > "$EVIDENCE_DIR/check.out" || fail "check-migration.py failed: $(cat "$EVIDENCE_DIR/check.out")"
  grep -q '^PASS$' "$EVIDENCE_DIR/check.out" || fail "checker did not print PASS"
}

require_backup_kept() {
  local backup flagged
  backup="$(json_field backupTopic < "$EVIDENCE_DIR/migration.json")"
  flagged="$(json_field backupCleanupRequired < "$EVIDENCE_DIR/migration.json")"
  [[ -n "$backup" ]] || fail "no backup topic on the record"
  topic_exists "$backup" || fail "backup topic $backup was deleted"
  if [[ "${REQUIRE_FLAG:-}" == "1" ]]; then
    [[ "$flagged" == "True" || "$flagged" == "true" ]] || fail "BackupCleanupRequired was not set"
  fi
}

quiet_compare() {
  compose up -d --scale mig-producer=0 >/dev/null
  sleep 5
  capture_logs
  compose up -d --scale mig-producer=2 --scale mig-consumer-measureeval=2 >/dev/null
}

write_digest_report() {
  python3 - <<PY || fail "backup digest could not be written"
import importlib.util, json
spec = importlib.util.spec_from_file_location("check", r"$CHECK")
mod = importlib.util.module_from_spec(spec)
spec.loader.exec_module(mod)
def load(path):
    rows = []
    with open(path, encoding="utf-8") as handle:
        for line in handle:
            if line.strip():
                rows.append(json.loads(line))
    return rows
root = r"$EVIDENCE_DIR"
left, total = mod.digests_of(load(root + "/backup-dump.jsonl"))
if total == 0:
    raise SystemExit("backup is empty; a real backup is required")
json.dump({"partitions": left, "total": total, "verified": True}, open(root + "/backup-digest.json", "w", encoding="utf-8"))
PY
}

run_p1() {
  open_migration "proof p1"
  drive
  [[ "$STEP" == "Done" ]] || fail "p1 ended in $STEP"
  [[ -f "$EVIDENCE_DIR/topic-dump.jsonl" ]] || fail "missed the C5 snapshot"
  write_digest_report
  capture_logs
  check_data "$TARGET"
  require_backup_kept
}

run_p2() {
  # Abort at H1. T is unchanged. The temp topic is kept and flagged.
  open_migration "proof p2 abort"
  RETURN_AT_H1=1 NO_GO=1 drive
  RETURN_AT_H1=""
  [[ "$STEP" == "H1" ]] || fail "p2 abort was not holding at H1 (step $STEP)"
  call_ok carol POST "/api/ops/kafka/migrations/${MIG_ID}/abort" "{\"typedName\":\"${TOPIC}\"}"
  NO_GO=1 drive
  [[ "$STEP" == "RolledBack" ]] || fail "p2 abort ended in $STEP"
  REQUIRE_FLAG=1 require_backup_kept
  local after
  after="$(topic_id_of "$TOPIC")"
  [[ "$after" == "$(cat "$EVIDENCE_DIR/original-topic-id.txt")" ]] || fail "p2 abort changed the topic id"
  # Drain timeout: pause one measureeval consumer so lag cannot hit zero.
  open_migration "proof p2 drain"
  PAUSE_AT=B3 NO_GO=1 RUN_TIMEOUT="$DRAIN_TIMEOUT" drive
  PAUSE_AT=""
  unpause_consumer
  fetch_migration || fail "p2 drain lost the migration record"
  [[ "$STEP" == "RolledBack" ]] || fail "p2 drain ended in $STEP"
  REQUIRE_FLAG=1 require_backup_kept
  # H1 timeout. Do not send go.
  open_migration "proof p2 hold"
  NO_GO=1 RUN_TIMEOUT="$HOLD_TIMEOUT" drive
  [[ "$STEP" == "RolledBack" ]] || fail "p2 hold timeout ended in $STEP"
  REQUIRE_FLAG=1 require_backup_kept
  quiet_compare
  python3 "$CHECK" \
    --producer-acks "$EVIDENCE_DIR/producer-acks.jsonl" \
    --consumer-log "$EVIDENCE_DIR/consumer-measureeval.jsonl" \
    --consumer-log "$EVIDENCE_DIR/consumer-g2.jsonl" \
    > "$EVIDENCE_DIR/check.out" || fail "p2 checker failed"
}

run_p3_one() {
  local at="$1"
  rm -f "$EVIDENCE_DIR/topic-dump.jsonl" "$EVIDENCE_DIR/c5-measureeval.txt"
  open_migration "proof p3 $at"
  KILL_AT="$at" drive
  KILL_AT=""
  [[ "$STEP" == "Done" ]] || fail "p3 kill at $at ended in $STEP"
  require_backup_kept
  local before after
  before="$(cat "$EVIDENCE_DIR/new-topic-id-before-kill.txt" 2>/dev/null || true)"
  if [[ -n "$before" ]]; then
    after="$(topic_id_of "$TOPIC")"
    [[ "$after" == "$before" ]] || fail "p3 deleted or replaced T after the kill at $at"
  fi
  [[ -f "$EVIDENCE_DIR/topic-dump.jsonl" ]] || fail "p3 missed the C5 snapshot at $at"
  write_digest_report
  capture_logs
  check_data "$TARGET"
}

run_p3() {
  local at
  for at in A2 B1 B3 B6 H1 C1 C3 C5 D1 D2; do
    run_p3_one "$at"
  done
}

run_p4() {
  open_migration "proof p4"
  KILL_AT=C2 STOP_AT=C2 drive
  KILL_AT=""
  STOP_AT=""
  # Abort after C1 stops in NeedsAttention and does not delete.
  call_ok carol POST "/api/ops/kafka/migrations/${MIG_ID}/abort" "{\"typedName\":\"${TOPIC}\"}"
  fetch_migration || fail "p4 lost the record after abort"
  [[ "$STEP" == "NeedsAttention" ]] || fail "p4 abort after C1 ended in $STEP"
  call_ok carol POST "/api/ops/kafka/migrations/${MIG_ID}/recover" "{\"action\":\"original\",\"typedName\":\"${TOPIC}\"}"
  UNTIL=Done drive
  UNTIL=""
  [[ "$STEP" == "Done" ]] || fail "p4 recover ended in $STEP"
  require_backup_kept
  local count
  count="$(topic_describe "$TOPIC" | python3 -c 'import sys,re
text=sys.stdin.read()
match=re.search(r"PartitionCount[:=]\s*(\d+)", text)
if match:
    print(match.group(1))
else:
    print(text.count("Partition:"))')"
  [[ "$count" == "$ORIGINAL" ]] || fail "p4 partition count is $count, expected $ORIGINAL"
  [[ -f "$EVIDENCE_DIR/topic-dump.jsonl" ]] || fail "p4 missed the C5 snapshot"
  write_digest_report
  capture_logs
  check_data "$ORIGINAL"
}

foreign_then_forward() {
  [[ "$STEP" == "NeedsAttention" ]] || fail "$scenario did not stop on a foreign topic (step $STEP)"
  local failure
  failure="$(json_field failure < "$EVIDENCE_DIR/migration.json")"
  printf '%s' "$failure" | grep -qi 'foreign' || fail "$scenario failure text did not report a foreign topic: $failure"
  require_backup_kept
  call_ok bob POST "/api/ops/kafka/migrations/${MIG_ID}/recover" "{\"action\":\"deleteForeign\",\"typedName\":\"${TOPIC}\"}"
  call_ok carol POST "/api/ops/kafka/migrations/${MIG_ID}/recover" "{\"action\":\"forward\",\"typedName\":\"${TOPIC}\"}"
  UNTIL=Done drive
  UNTIL=""
  [[ "$STEP" == "Done" ]] || fail "$scenario forward ended in $STEP"
  require_backup_kept
  [[ -f "$EVIDENCE_DIR/topic-dump.jsonl" ]] || fail "$scenario missed the C5 snapshot"
  write_digest_report
  python3 "$CHECK" \
    --pre-migration "$EVIDENCE_DIR/pre-migration.jsonl" \
    --topic-dump "$EVIDENCE_DIR/topic-dump.jsonl" \
    --backup-dump "$EVIDENCE_DIR/backup-dump.jsonl" \
    --backup-digest "$EVIDENCE_DIR/backup-digest.json" \
    --groups-describe "$EVIDENCE_DIR/c5-measureeval.txt" \
    --groups-describe "$EVIDENCE_DIR/g2.txt" \
    --group measureeval \
    --group g2 \
    --partitions "$TARGET" \
    --require-empty \
    --require-backup \
    --require-offsets \
    --require-digest \
    > "$EVIDENCE_DIR/check.out" || fail "$scenario empty-topic check failed: $(cat "$EVIDENCE_DIR/check.out")"
}

run_p5() {
  open_migration "proof p5"
  ON_C2="compose --profile stray up -d mig-stray-producer"
  drive
  ON_C2=""
  foreign_then_forward
  compose --profile stray stop mig-stray-producer >/dev/null || true
}

run_p6() {
  call_ok carol POST "/api/ops/kafka/proof/base-listener-stop" "{\"topic\":\"${TOPIC}\"}"
  local stopped resumed
  stopped="$(printf '%s' "$RESP_BODY" | json_field stopped)"
  resumed="$(printf '%s' "$RESP_BODY" | json_field resumed)"
  [[ "$stopped" == "true" || "$stopped" == "True" ]] || fail "BaseListener did not stop when the topic disappeared"
  [[ "$resumed" == "false" || "$resumed" == "False" ]] || fail "BaseListener resumed without a restart"
}

run_p7() {
  [[ -n "$ADMIN_BFF_URL_2" ]] || fail "ADMIN_BFF_URL_2 is required"
  [[ -n "${ADMIN_BFF_CONTAINER_2:-}" ]] || fail "ADMIN_BFF_CONTAINER_2 is required"
  open_migration "proof p7"
  local raw code
  raw="$(bff "$ADMIN_BFF_URL_2" alice POST "/api/ops/kafka/migrations" "{\"topic\":\"${TOPIC}\",\"targetPartitions\":${TARGET},\"reason\":\"second host\",\"typedName\":\"${TOPIC}\",\"planHash\":\"none\"}")" || true
  split_response "$raw"
  code="$RESP_CODE"
  [[ "$code" == "409" || "$code" == "423" ]] || fail "second harness host was not refused (HTTP $code)"
  docker logs --tail 200 "$ADMIN_BFF_CONTAINER_2" 2>&1 | grep -qi 'lease' || fail "second host did not log that the lease is held"
  call_ok carol POST "/api/ops/kafka/migrations/${MIG_ID}/abort" "{\"typedName\":\"${TOPIC}\"}" || true
}

run_p8() {
  open_migration "proof p8"
  FLUSH_AT=B4 drive
  FLUSH_AT=""
  [[ "$STEP" == "Done" ]] || fail "p8 ended in $STEP"
  [[ -f "$EVIDENCE_DIR/topic-dump.jsonl" ]] || fail "p8 missed the C5 snapshot"
  write_digest_report
  capture_logs
  check_data "$TARGET"
  require_backup_kept
}

run_p9() {
  open_migration "proof p9"
  ON_C2="create_during_c2"
  drive
  ON_C2=""
  foreign_then_forward
}

create_during_c2() {
  if [[ -n "${KAFKA_REST_PROXY:-}" ]]; then
    "$repo/Scripts/create-topics-rest.sh" "$KAFKA_REST_PROXY" "${KAFKA_REST_USER:-}" "${KAFKA_REST_PASSWORD:-}" "$repo/topics.txt" || true
  else
    compose exec -T "$BROKER_SERVICE" /opt/kafka/bin/kafka-topics.sh --bootstrap-server "$BROKER_BOOTSTRAP" \
      --create --if-not-exists --topic "$TOPIC" --partitions 3 --replication-factor 1 >/dev/null || true
  fi
}

case "$scenario" in
  p1) run_p1 ;;
  p2) run_p2 ;;
  p3) run_p3 ;;
  p4) run_p4 ;;
  p5) run_p5 ;;
  p6) run_p6 ;;
  p7) run_p7 ;;
  p8) run_p8 ;;
  p9) run_p9 ;;
esac

echo "PASS"
