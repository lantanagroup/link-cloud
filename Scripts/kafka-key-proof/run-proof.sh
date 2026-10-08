#!/usr/bin/env bash
# Runs the Kafka key proof against a broker in KAFKA_BOOTSTRAP.
# Single broker:  docker compose -f Scripts/kafka-key-proof/docker-compose.yml up -d
# Three brokers:  docker compose -f Scripts/kafka-key-proof/docker-compose.cluster.yml up -d
# Point KAFKA_BOOTSTRAP at the broker the test process can reach (for example kafka-1:9092 on the proof network).
set -euo pipefail

if [[ -z "${KAFKA_BOOTSTRAP:-}" ]]; then
  echo "KAFKA_BOOTSTRAP is unset. The proof tests will not contact a broker."
  exit 1
fi

ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
RESULTS="${KAFKA_PROOF_RESULTS:-$ROOT/Scripts/kafka-key-proof/results}"
mkdir -p "$RESULTS"
rm -f "$RESULTS/partitions.tsv"
export KAFKA_PROOF_RESULTS="$RESULTS"

echo "Running .NET proof tests against $KAFKA_BOOTSTRAP"
dotnet test "$ROOT/DotNet/KafkaKeyProof.Tests/KafkaKeyProof.Tests.csproj" --nologo --filter "FullyQualifiedName~KafkaPartitionProofTests|FullyQualifiedName~KafkaBrokerProofTests"

if [[ -n "${JAVA_HOME:-}" && -x "${MAVEN_HOME:-}/bin/mvn" ]]; then
  echo "Running Java proof tests"
  (cd "$ROOT/Java" && "$MAVEN_HOME/bin/mvn" -pl shared,measureeval -am test -Dtest=KafkaKeyGoldenTest,KafkaPartitionProofTest -DfailIfNoTests=false)
else
  echo "JAVA_HOME or MAVEN_HOME is unset. Java proof tests were not run."
fi

if [[ ! -s "$RESULTS/partitions.tsv" ]]; then
  echo "No partition results were written."
  exit 1
fi

echo "Partition results:"
cat "$RESULTS/partitions.tsv"

python - "$RESULTS/partitions.tsv" <<'PY'
import sys
from collections import defaultdict
rows = defaultdict(dict)
for line in open(sys.argv[1], encoding="utf-8"):
    runtime, topic, count, key, partition = line.rstrip("\n").split("\t")
    rows[(topic, count, key)][runtime] = partition
failed = False
for identity, by_runtime in sorted(rows.items()):
    values = set(by_runtime.values())
    if "dotnet" in by_runtime and "java" in by_runtime and len(values) != 1:
        print("MISMATCH", identity, by_runtime)
        failed = True
    else:
        print("OK", identity, by_runtime)
if failed:
    sys.exit(1)
PY
