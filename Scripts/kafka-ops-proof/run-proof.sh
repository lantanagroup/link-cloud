#!/usr/bin/env bash
# Isolated-host proof for the Kafka operations console.
# Refuses to run unless KAFKA_PROOF_ALLOW=1.
# The day-to-day workstation also requires KAFKA_PROOF_ALLOW_ON_THIS_PC=1.
# "down" removes the extra broker and the volumes.
# A refusal exits 2.
set -euo pipefail

if [[ "${KAFKA_PROOF_ALLOW:-}" != "1" ]]; then
  echo "Refusing to start containers. Set KAFKA_PROOF_ALLOW=1 on the isolated host." >&2
  exit 2
fi

host="$(hostname 2>/dev/null || true)"
if [[ "$host" == "DESKTOP-5NA82VF" && "${KAFKA_PROOF_ALLOW_ON_THIS_PC:-}" != "1" ]]; then
  echo "This workstation is not the isolated Docker host. The kit stays stopped here." >&2
  exit 2
fi

project="${KAFKA_PROOF_COMPOSE_PROJECT:-kafka-ops-proof}"
if [[ ! "$project" =~ ^[A-Za-z0-9][A-Za-z0-9_.-]{0,79}$ ]]; then
  echo "The compose project name is invalid." >&2
  exit 2
fi
if [[ "$project" == *linkui* || "$project" == *scaffold* || "$project" == *shared* ]]; then
  echo "Refusing a compose project name that could match the shared stack." >&2
  exit 2
fi

here="$(cd "$(dirname "$0")" && pwd)"
repo="$(cd "$here/../.." && pwd)"
compose="$here/compose.yml"
publish="$here/compose.publish.yml"
files=(-f "$compose")
publish_flag=0
deadline=$((SECONDS + 1200))

for arg in "$@"; do
  case "$arg" in
    --publish) publish_flag=1 ;;
    --down)
      docker compose -p "$project" -f "$compose" -f "$publish" --profile extra-broker down -v --remove-orphans --timeout 30
      echo "Stopped project $project and removed its volumes, including the extra broker."
      exit 0
      ;;
    *) echo "Unknown argument: $arg" >&2; exit 2 ;;
  esac
done

if [[ "$publish_flag" == "1" ]]; then
  port="${KAFKA_PROOF_PORT:-19094}"
  if [[ ! "$port" =~ ^[0-9]+$ ]] || (( port < 1 || port > 65535 )); then
    echo "KAFKA_PROOF_PORT is not a valid port." >&2
    exit 2
  fi
  if (( port >= 5280 && port <= 5294 )); then
    echo "Port $port is reserved. Pick a host port outside 5280-5294." >&2
    exit 2
  fi
  if (( port != 19094 )); then
    echo "KAFKA_PROOF_PORT must stay 19094. Broker 0 advertises localhost:19094, and a different port would recreate the controller." >&2
    exit 2
  fi
  export KAFKA_PROOF_PORT="19094"
  export KAFKA_BOOTSTRAP="localhost:19094"
  files+=(-f "$publish")
fi

check_time() {
  if (( SECONDS > deadline )); then
    echo "The proof run exceeded 20 minutes." >&2
    exit 1
  fi
}

compose() {
  check_time
  docker compose -p "$project" "${files[@]}" "$@"
}

echo "STEP unit-tests"
if command -v dotnet >/dev/null 2>&1; then
  dotnet test "$repo/DotNet/KafkaOps.Proof/KafkaOps.Proof.csproj" --filter "Category=UnitTests" --nologo -v q
  echo "PASS unit-tests"
else
  echo "SKIP unit-tests (dotnet is not on PATH)"
fi

echo "STEP compose-up"
if [[ "$publish_flag" == "1" ]]; then
  compose up -d --no-recreate
else
  compose up -d
fi

echo "STEP overview"
created=0
for _ in $(seq 1 40); do
  check_time
  if compose exec -T broker-0 /opt/kafka/bin/kafka-topics.sh --bootstrap-server broker-0:9092 --create --if-not-exists --topic ops-proof-log --partitions 3 --replication-factor 3; then
    created=1
    break
  fi
  sleep 3
done
[[ "$created" == "1" ]] || { echo "ops-proof-log was not created." >&2; exit 1; }

brokers="$(compose exec -T broker-0 /opt/kafka/bin/kafka-broker-api-versions.sh --bootstrap-server broker-0:9092)"
for id in 0 1 2; do
  grep -Eq "\(id:[[:space:]]*$id[[:space:]]" <<<"$brokers" || { echo "Broker $id is missing from the cluster overview." >&2; exit 1; }
done
echo "PASS overview"

echo "STEP replicas"
compose up -d --scale consumer=3 --no-recreate consumer
stable=0
for _ in $(seq 1 40); do
  check_time
  state="$(compose exec -T broker-0 /opt/kafka/bin/kafka-consumer-groups.sh --bootstrap-server broker-0:9092 --describe --group ops-proof-consumers --state 2>&1 || true)"
  if grep -Eq "ops-proof-consumers[[:space:]]+.*[[:space:]]Stable[[:space:]]+3[[:space:]]*$" <<<"$state"; then
    stable=1
    break
  fi
  sleep 3
done
[[ "$stable" == "1" ]] || { echo "ops-proof-consumers did not become Stable with 3 members." >&2; exit 1; }
echo "PASS replicas"

echo "STEP add-broker"
if [[ "$publish_flag" == "1" ]]; then
  compose --profile extra-broker up -d --no-recreate broker-3 host-proxy-3
else
  compose --profile extra-broker up -d broker-3
fi
joined=0
for _ in $(seq 1 40); do
  check_time
  listed="$(compose exec -T broker-0 /opt/kafka/bin/kafka-broker-api-versions.sh --bootstrap-server broker-0:9092 2>&1 || true)"
  if grep -Eq "\(id:[[:space:]]*3[[:space:]]" <<<"$listed"; then
    joined=1
    break
  fi
  sleep 3
done
[[ "$joined" == "1" ]] || { echo "Broker 3 did not register." >&2; exit 1; }
echo "PASS add-broker"

reassign() {
  local json="$1"
  printf '%s' "$json" | compose exec -T broker-0 bash -lc "cat > /tmp/link-ops-reassign.json && /opt/kafka/bin/kafka-reassign-partitions.sh --bootstrap-server broker-0:9092 --reassignment-json-file /tmp/link-ops-reassign.json --execute"
  for _ in $(seq 1 40); do
    check_time
    local verify
    verify="$(compose exec -T broker-0 /opt/kafka/bin/kafka-reassign-partitions.sh --bootstrap-server broker-0:9092 --reassignment-json-file /tmp/link-ops-reassign.json --verify 2>&1 || true)"
    if grep -q "completed" <<<"$verify" && ! grep -Eq "in progress|failed" <<<"$verify"; then
      return 0
    fi
    sleep 3
  done
  echo "Reassignment did not complete." >&2
  exit 1
}

echo "STEP reassignment"
reassign '{"version":1,"partitions":[{"topic":"ops-proof-log","partition":0,"replicas":[3,1,2]},{"topic":"ops-proof-log","partition":1,"replicas":[0,3,2]},{"topic":"ops-proof-log","partition":2,"replicas":[0,1,3]}]}'
reassign '{"version":1,"partitions":[{"topic":"ops-proof-log","partition":0,"replicas":[0,1,2]},{"topic":"ops-proof-log","partition":1,"replicas":[0,1,2]},{"topic":"ops-proof-log","partition":2,"replicas":[0,1,2]}]}'

described="$(compose exec -T broker-0 /opt/kafka/bin/kafka-topics.sh --bootstrap-server broker-0:9092 --describe --topic ops-proof-log)"
echo "$described" | grep -q "Replicas:" || { echo "Topic describe did not list replicas." >&2; exit 1; }
while IFS= read -r line; do
  [[ "$line" == *"Replicas:"* ]] || continue
  replicas="$(sed -n 's/.*Replicas:[[:space:]]*\([0-9,]*\).*/\1/p' <<<"$line")"
  IFS=',' read -ra ids <<<"$replicas"
  for id in "${ids[@]}"; do
    if [[ "$id" == "3" ]]; then
      echo "Broker 3 still holds a replica: $line" >&2
      exit 1
    fi
  done
done <<<"$described"

under="$(compose exec -T broker-0 /opt/kafka/bin/kafka-topics.sh --bootstrap-server broker-0:9092 --describe --topic ops-proof-log --under-replicated-partitions 2>&1 || true)"
if [[ -n "${under//[[:space:]]/}" ]]; then
  echo "Under-replicated partitions remain: $under" >&2
  exit 1
fi
echo "PASS decommission-empty"

echo "STEP stop-broker"
compose stop broker-3
echo "PASS decommission-stop"

if ! command -v dotnet >/dev/null 2>&1; then
  echo "SKIP console-flow (dotnet is not on PATH)"
elif [[ -z "${KAFKA_BOOTSTRAP:-}" ]]; then
  echo "SKIP console-flow (KAFKA_BOOTSTRAP is not set; pass --publish)"
else
  echo "STEP console-flow"
  dotnet test "$repo/DotNet/KafkaOps.Proof/KafkaOps.Proof.csproj" --filter "FullyQualifiedName~KafkaOpsConsoleFlowTests" --nologo -v q
  echo "PASS console-flow"
fi

echo "PASS proof"
echo "Inspect the project with: docker compose -p $project -f $compose ps"
echo "Stop it with: $0 --down"
echo "That stop removes the extra broker and the volumes."
