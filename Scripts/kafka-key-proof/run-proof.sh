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

if [[ -z "${JAVA_HOME:-}" || ! -x "${JAVA_HOME}/bin/java" ]]; then
  echo "JAVA_HOME is unset or does not contain bin/java."
  exit 1
fi
if [[ -z "${MAVEN_HOME:-}" || ! -x "${MAVEN_HOME}/bin/mvn" ]]; then
  echo "MAVEN_HOME is unset or does not contain bin/mvn."
  exit 1
fi

echo "Running .NET proof tests against $KAFKA_BOOTSTRAP"
dotnet test "$ROOT/DotNet/KafkaKeyProof.Tests/KafkaKeyProof.Tests.csproj" --nologo --filter "FullyQualifiedName~KafkaPartitionProofTests|FullyQualifiedName~KafkaBrokerProofTests"

echo "Running Java proof tests"
(cd "$ROOT/Java" && "$MAVEN_HOME/bin/mvn" -pl shared,measureeval -am test -Dtest=KafkaKeyGoldenTest,KafkaPartitionProofTest "-Dsurefire.failIfNoSpecifiedTests=false")

if [[ ! -s "$RESULTS/partitions.tsv" ]]; then
  echo "No partition results were written."
  exit 1
fi

echo "Partition results:"
cat "$RESULTS/partitions.tsv"

PYTHON=""
if command -v python3 >/dev/null 2>&1; then
  PYTHON="python3"
elif command -v python >/dev/null 2>&1; then
  PYTHON="python"
else
  echo "python3 or python is required to compare partition results."
  exit 1
fi

"$PYTHON" - "$RESULTS/partitions.tsv" <<'PY'
import sys
from collections import defaultdict
raw = open(sys.argv[1], "rb").read()
if raw.startswith(b"\xef\xbb\xbf"):
    print("MISMATCH partition results start with a UTF-8 BOM, so the first row would not compare")
    sys.exit(1)
text = raw.decode("utf-8")
rows = defaultdict(dict)
for lineno, line in enumerate(text.splitlines(), 1):
    if line == "":
        continue
    parts = line.split("\t")
    if len(parts) != 5:
        print("MISMATCH line", lineno, "expected 5 fields")
        sys.exit(1)
    runtime, topic, count, key, partition = parts
    if runtime not in ("dotnet", "java"):
        print("MISMATCH line", lineno, "runtime", runtime)
        sys.exit(1)
    rows[(topic, count, key)][runtime] = partition
failed = False
for identity, by_runtime in sorted(rows.items()):
    if "dotnet" not in by_runtime or "java" not in by_runtime:
        print("MISMATCH", identity, "missing runtime", by_runtime)
        failed = True
        continue
    values = set(by_runtime.values())
    if len(values) != 1:
        print("MISMATCH", identity, by_runtime)
        failed = True
    else:
        print("OK", identity, by_runtime)
if failed:
    sys.exit(1)
PY
