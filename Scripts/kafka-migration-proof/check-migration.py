#!/usr/bin/env python3
"""Check a topic-migration proof run.

The proof is not run on the build machine. This checker only reads evidence
files, or a broker when asked. It exits 0 on success and non-zero on failure.

Checks, when the matching input is present:

- loss: every acked (key, seq) appears once in each consumer log
- duplicates: no repeated (key, seq) in a consumer log
- per-key order: seq is contiguous and increases with offset
- the recreated topic is empty
- the backup holds every pre-migration record once, in per-key order
- partition count
- every committed offset is 0 and every high watermark is 0
- backup digest, recomputed from the backup dump
"""

from __future__ import annotations

import argparse
import base64
import hashlib
import json
import os
import struct
import subprocess
import sys
from collections import defaultdict


MIGRATION_HEADER = "x-link-migration"


def fail(errors, message):
    errors.append(message)
    print("FAIL " + message)


def load_jsonl(path):
    rows = []
    with open(path, "r", encoding="utf-8") as handle:
        for line_number, line in enumerate(handle, start=1):
            text = line.strip()
            if not text:
                continue
            try:
                rows.append(json.loads(text))
            except json.JSONDecodeError as error:
                raise SystemExit(f"FAIL {path}:{line_number} is not JSON ({error})")
    return rows


def load_json(path):
    with open(path, "r", encoding="utf-8") as handle:
        return json.load(handle)


def key_of(row):
    if "key" in row and row["key"] is not None:
        return row["key"] if isinstance(row["key"], str) else json.dumps(row["key"], separators=(",", ":"), sort_keys=True)
    if "keyBase64" in row:
        return base64.b64decode(row["keyBase64"]).decode("utf-8")
    return ""


def seq_of(row):
    if "seq" in row and row["seq"] is not None:
        return int(row["seq"])
    value = row.get("value")
    if isinstance(value, str) and value.startswith("{"):
        try:
            parsed = json.loads(value)
        except json.JSONDecodeError:
            parsed = None
        if isinstance(parsed, dict) and "seq" in parsed:
            return int(parsed["seq"])
    return None


def acked_pairs(rows):
    pairs = []
    for row in rows:
        if row.get("acked") is False:
            continue
        seq = seq_of(row)
        if seq is None:
            continue
        pairs.append((key_of(row), seq))
    return pairs


def check_loss_and_order(errors, acks, consumer_rows, label):
    expected = acked_pairs(acks)
    if not expected:
        fail(errors, f"{label}: producer ack log has no records")
        return
    seen = []
    for row in consumer_rows:
        seq = seq_of(row)
        if seq is None:
            fail(errors, f"{label}: consumer row has no seq")
            return
        seen.append((key_of(row), seq, row.get("partition"), row.get("offset")))
    got = [(key, seq) for key, seq, _partition, _offset in seen]
    if len(got) != len(set(got)):
        fail(errors, f"{label}: duplicate (key, seq) in the consumer log")
    if set(got) != set(expected):
        missing = len(set(expected) - set(got))
        extra = len(set(got) - set(expected))
        fail(errors, f"{label}: loss or unexpected records (missing {missing}, extra {extra})")
    by_key = defaultdict(list)
    for key, seq, partition, offset in seen:
        by_key[key].append((seq, partition, offset))
    for key, items in by_key.items():
        ordered = sorted(items, key=lambda item: (item[2] is None, item[2] if item[2] is not None else 0, item[0]))
        seqs = [item[0] for item in ordered]
        if seqs != sorted(seqs) or len(seqs) != len(set(seqs)):
            fail(errors, f"{label}: per-key order broken for {key}")
            return
        if seqs != list(range(seqs[0], seqs[0] + len(seqs))):
            fail(errors, f"{label}: seq gap for {key}")
            return
        partitions = {item[1] for item in items if item[1] is not None}
        if len(partitions) > 1:
            fail(errors, f"{label}: key {key} is on more than one partition")
            return


def check_topic_empty(errors, dump_rows):
    if dump_rows:
        fail(errors, f"new topic is not empty ({len(dump_rows)} records)")


def check_backup_holds(errors, pre_rows, backup_rows):
    pre = acked_pairs(pre_rows)
    if not pre:
        fail(errors, "pre-migration snapshot is empty")
        return
    if not backup_rows:
        fail(errors, "backup is empty; a real backup is required")
        return
    held = []
    for row in backup_rows:
        seq = seq_of(row)
        if seq is None:
            continue
        held.append((key_of(row), seq, row.get("partition"), row.get("offset")))
    counts = defaultdict(int)
    by_key = defaultdict(list)
    for key, seq, partition, offset in held:
        counts[(key, seq)] += 1
        by_key[key].append((seq, partition, offset))
    for pair in pre:
        if counts[pair] == 0:
            fail(errors, "backup is missing a pre-migration record")
            return
        if counts[pair] != 1:
            fail(errors, "backup has a duplicate pre-migration record")
            return
    for key, items in by_key.items():
        ordered = sorted(items, key=lambda item: (item[2] is None, item[2] if item[2] is not None else 0))
        seqs = [item[0] for item in ordered]
        if seqs != list(range(seqs[0], seqs[0] + len(seqs))):
            fail(errors, f"backup per-key order broken for {key}")
            return
        partitions = {item[1] for item in items if item[1] is not None}
        if len(partitions) > 1:
            fail(errors, f"backup key {key} is on more than one partition")
            return


def raw_bytes(row, field, b64_field):
    if b64_field in row and row[b64_field] is not None:
        return base64.b64decode(row[b64_field])
    if field in row and row[field] is not None:
        value = row[field]
        if isinstance(value, str):
            return value.encode("utf-8")
        return json.dumps(value, separators=(",", ":"), sort_keys=True).encode("utf-8")
    return b""


def source_identity(row):
    header = header_map(row).get(MIGRATION_HEADER)
    if header:
        parts = header.split(";")
        if len(parts) == 4:
            try:
                return int(parts[2]), int(parts[3])
            except ValueError:
                pass
    return int(row.get("sourcePartition", row.get("partition", 0))), int(row.get("sourceOffset", row.get("offset", 0)))


def header_map(row):
    found = {}
    headers = row.get("headers") or []
    if isinstance(headers, dict):
        return {str(name): str(value) for name, value in headers.items()}
    for item in headers:
        if isinstance(item, dict):
            name = item.get("name") or item.get("key")
            value = item.get("value", "")
            if item.get("valueBase64"):
                value = base64.b64decode(item["valueBase64"]).decode("utf-8")
        elif isinstance(item, (list, tuple)) and len(item) == 2:
            name, value = item
        else:
            continue
        if name:
            found[str(name)] = value if isinstance(value, str) else str(value)
    return found


def digest_partition(rows):
    ordered = sorted(rows, key=lambda row: int(row.get("offset", 0)))
    hasher = hashlib.sha256()
    for row in ordered:
        partition, offset = source_identity(row)
        hasher.update(struct.pack("<i", partition))
        hasher.update(struct.pack("<q", offset))
        hasher.update(raw_bytes(row, "key", "keyBase64"))
        hasher.update(raw_bytes(row, "value", "valueBase64"))
        hasher.update(struct.pack("<q", int(row.get("timestampMs", row.get("timestamp", 0)))))
        headers = header_map(row)
        for name in sorted(key for key in headers if key.lower() != MIGRATION_HEADER):
            hasher.update(name.encode("utf-8"))
            hasher.update(headers[name].encode("utf-8"))
    return hasher.hexdigest().upper()


def digests_of(rows):
    grouped = defaultdict(list)
    for row in rows:
        grouped[int(row.get("partition", 0))].append(row)
    return {str(partition): digest_partition(group) for partition, group in sorted(grouped.items())}, len(rows)


def check_backup_digest(errors, report, dump_rows):
    expected = report.get("partitions") or report.get("expected") or {}
    if dump_rows is not None:
        recomputed, total = digests_of(dump_rows)
        if total == 0:
            fail(errors, "backup is empty; a real backup is required")
            return
        if expected and {str(key): str(value).upper() for key, value in expected.items()} != recomputed:
            fail(errors, "backup digest does not match the backup dump")
        elif not expected:
            expected = recomputed
        report_total = report.get("total")
        if report_total is not None and int(report_total) != total:
            fail(errors, f"backup count {report_total} does not match dump {total}")
    if report.get("verified") is False or report.get("match") is False:
        fail(errors, "backup digest report says the verify failed")
    if not expected and dump_rows is None:
        fail(errors, "backup digest report has nothing to compare")


def check_offsets(errors, payload, partitions_expected):
    rows = payload.get("partitions") if isinstance(payload, dict) else payload
    if not isinstance(rows, list) or not rows:
        fail(errors, "offset snapshot is empty")
        return
    if partitions_expected and len(rows) != partitions_expected:
        fail(errors, f"offset snapshot has {len(rows)} partitions, expected {partitions_expected}")
    for row in rows:
        committed = int(row["committed"])
        watermark = int(row.get("highWatermark", row.get("logEnd")))
        partition = row.get("partition")
        if watermark != 0:
            fail(errors, f"partition {partition} high watermark {watermark} is above 0")
            return
        if committed != 0:
            fail(errors, f"partition {partition} offset {committed} is not 0")
            return


def check_partition_count(errors, expected, offsets_payload, dump_rows):
    if not expected:
        return
    if isinstance(offsets_payload, dict) and offsets_payload.get("partitions"):
        got = len(offsets_payload["partitions"])
    elif dump_rows:
        got = 1 + max(int(row.get("partition", 0)) for row in dump_rows)
    else:
        fail(errors, "partition count was not available")
        return
    if got != expected:
        fail(errors, f"partition count is {got}, expected {expected}")


def parse_groups_describe(text, group):
    lines = [line.rstrip() for line in text.splitlines() if line.strip()]
    header_index = None
    for index, line in enumerate(lines):
        if "CURRENT-OFFSET" in line and "LOG-END-OFFSET" in line:
            header_index = index
            break
    if header_index is None:
        raise ValueError("consumer-group describe has no offset header")
    header = lines[header_index].split()
    current_at = header.index("CURRENT-OFFSET")
    end_at = header.index("LOG-END-OFFSET")
    partition_at = header.index("PARTITION")
    group_at = header.index("GROUP") if "GROUP" in header else None
    rows = []
    for line in lines[header_index + 1 :]:
        parts = line.split()
        if group_at is not None and parts[group_at] != group:
            continue
        if parts[current_at] == "-" or parts[end_at] == "-":
            continue
        rows.append(
            {
                "partition": int(parts[partition_at]),
                "committed": int(parts[current_at]),
                "highWatermark": int(parts[end_at]),
            }
        )
    rows.sort(key=lambda row: row["partition"])
    return {"partitions": rows}


def docker_dump(topic):
    project = os.environ.get("COMPOSE_PROJECT", "silo-bug-investigator-kafka")
    service = os.environ.get("KAFKA_DUMP_SERVICE", "mig-producer")
    bootstrap = os.environ.get("KAFKA_BOOTSTRAP_INTERNAL", os.environ.get("KAFKA_BOOTSTRAP", "kafka-1:9092"))
    base = os.environ.get("KAFKA_PROOF_BASE_COMPOSE", "/workspace/silos/bug-investigator/kafka-proof/compose.yaml")
    override = os.environ.get(
        "KAFKA_PROOF_OVERRIDE",
        os.path.join(os.path.dirname(os.path.abspath(__file__)), "compose.migration.yml"),
    )
    script = r"""
import base64, json, os
from kafka import KafkaConsumer, TopicPartition
bootstrap = os.environ["DUMP_BOOTSTRAP"]
topic = os.environ["DUMP_TOPIC"]
consumer = KafkaConsumer(bootstrap_servers=bootstrap, enable_auto_commit=False, consumer_timeout_ms=8000)
parts = consumer.partitions_for_topic(topic) or []
assigned = [TopicPartition(topic, partition) for partition in sorted(parts)]
consumer.assign(assigned)
consumer.seek_to_beginning(*assigned)
while True:
    batches = consumer.poll(timeout_ms=2000, max_records=500)
    if not batches:
        break
    for tp, messages in batches.items():
        for msg in messages:
            headers = []
            for name, value in msg.headers or []:
                headers.append({"name": name, "valueBase64": base64.b64encode(value or b"").decode("ascii")})
            print(json.dumps({
                "partition": msg.partition,
                "offset": msg.offset,
                "timestampMs": msg.timestamp or 0,
                "keyBase64": base64.b64encode(msg.key or b"").decode("ascii"),
                "valueBase64": base64.b64encode(msg.value or b"").decode("ascii"),
                "key": (msg.key or b"").decode("utf-8", "replace"),
                "value": (msg.value or b"").decode("utf-8", "replace"),
                "headers": headers,
            }))
"""
    command = [
        "docker",
        "compose",
        "-p",
        project,
        "-f",
        base,
        "-f",
        override,
        "exec",
        "-T",
        "-i",
        "-e",
        "DUMP_BOOTSTRAP=" + bootstrap,
        "-e",
        "DUMP_TOPIC=" + topic,
        service,
        "python",
        "-u",
        "-",
    ]
    completed = subprocess.run(command, input=script, text=True, capture_output=True)
    if completed.returncode != 0:
        raise RuntimeError(completed.stderr.strip() or "docker dump failed")
    rows = []
    for line in completed.stdout.splitlines():
        if line.startswith("{"):
            rows.append(json.loads(line))
    return rows


def build_parser():
    parser = argparse.ArgumentParser(description="Check a Kafka topic-migration proof run.")
    parser.add_argument("--producer-acks")
    parser.add_argument("--consumer-log", action="append", default=[])
    parser.add_argument("--topic-dump")
    parser.add_argument("--pre-migration")
    parser.add_argument("--offsets")
    parser.add_argument("--groups-describe", action="append", default=[], help="kafka-consumer-groups.sh --describe output.")
    parser.add_argument("--group", action="append", default=[])
    parser.add_argument("--backup-digest")
    parser.add_argument("--backup-dump")
    parser.add_argument("--partitions", type=int)
    parser.add_argument("--require-empty", action="store_true")
    parser.add_argument("--require-backup", action="store_true")
    parser.add_argument("--require-offsets", action="store_true")
    parser.add_argument("--require-digest", action="store_true")
    parser.add_argument("--dump-topic", help="Read this topic through docker compose exec.")
    parser.add_argument("--dump-backup-topic")
    return parser


def main(argv):
    parser = build_parser()
    args = parser.parse_args(argv)
    errors = []
    acks = load_jsonl(args.producer_acks) if args.producer_acks else None
    if acks is not None:
        if not args.consumer_log:
            fail(errors, "producer acks were given without a consumer log")
        for path in args.consumer_log:
            check_loss_and_order(errors, acks, load_jsonl(path), os.path.basename(path))

    dump_rows = load_jsonl(args.topic_dump) if args.topic_dump else None
    if args.dump_topic:
        try:
            dump_rows = docker_dump(args.dump_topic)
        except (OSError, RuntimeError, json.JSONDecodeError) as error:
            fail(errors, f"topic dump failed: {error}")
    pre_rows = load_jsonl(args.pre_migration) if args.pre_migration else None
    if args.require_empty and dump_rows is None:
        fail(errors, "topic dump is required")
    if dump_rows is not None and args.require_empty:
        check_topic_empty(errors, dump_rows)

    offsets_payload = load_json(args.offsets) if args.offsets else None
    if args.groups_describe:
        groups = args.group or ["measureeval"]
        if len(groups) != len(args.groups_describe):
            groups = groups * len(args.groups_describe)
        parsed = []
        for path, group in zip(args.groups_describe, groups):
            with open(path, "r", encoding="utf-8") as handle:
                try:
                    parsed.append(parse_groups_describe(handle.read(), group))
                except (ValueError, IndexError) as error:
                    fail(errors, f"could not parse {path}: {error}")
                    parsed.append(None)
        if offsets_payload is None and parsed and parsed[0] is not None:
            offsets_payload = parsed[0]
        for payload in parsed:
            if payload is not None:
                check_offsets(errors, payload, args.partitions)
    if args.require_offsets and offsets_payload is None and not args.groups_describe:
        fail(errors, "offset snapshot is required")
    if offsets_payload is not None and not args.groups_describe:
        check_offsets(errors, offsets_payload, args.partitions)
    if args.partitions:
        check_partition_count(errors, args.partitions, offsets_payload, dump_rows)

    report = load_json(args.backup_digest) if args.backup_digest else None
    backup_dump = load_jsonl(args.backup_dump) if args.backup_dump else None
    if args.dump_backup_topic:
        try:
            backup_dump = docker_dump(args.dump_backup_topic)
        except (OSError, RuntimeError, json.JSONDecodeError) as error:
            fail(errors, f"backup dump failed: {error}")
    if args.require_backup and pre_rows is None:
        fail(errors, "pre-migration snapshot is required")
    if args.require_backup and backup_dump is None:
        fail(errors, "backup dump is required")
    if pre_rows is not None and backup_dump is not None:
        check_backup_holds(errors, pre_rows, backup_dump)
    if args.require_digest and report is None and backup_dump is None:
        fail(errors, "backup digest is required")
    if report is not None or (backup_dump is not None and args.require_digest):
        check_backup_digest(errors, report or {}, backup_dump)

    if not any(
        [
            args.producer_acks,
            args.consumer_log,
            args.topic_dump,
            args.pre_migration,
            args.offsets,
            args.groups_describe,
            args.backup_digest,
            args.backup_dump,
            args.dump_topic,
            args.dump_backup_topic,
            args.require_empty,
            args.require_backup,
            args.require_offsets,
            args.require_digest,
        ]
    ):
        parser.print_help(sys.stderr)
        print("FAIL no evidence was provided")
        return 2

    if errors:
        print("FAIL")
        return 1
    print("PASS")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
