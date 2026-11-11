"""Kafka collector (confluent-kafka): watermarks, topic facts, sampled message sizes and consumer lag.

Messages produced during a run are the change in the summed high watermarks, which retention can't reduce;
messages held now are high minus low. Message sizes come from reading the newest messages with an assign-only
consumer that never subscribes or commits, so no consumer group is created or moved.
"""

from __future__ import annotations

import uuid


def client_config(kafka: dict) -> dict:
    # log_level 2 keeps librdkafka's own connect retries off stderr; failures surface as `unavailable` entries.
    conf = {"bootstrap.servers": kafka["bootstrap_servers"], "socket.timeout.ms": 10000, "log_level": 2}
    if kafka.get("security_protocol"):
        conf["security.protocol"] = kafka["security_protocol"]
    if kafka.get("sasl_mechanism"):
        conf["sasl.mechanism"] = kafka["sasl_mechanism"]
    if kafka.get("username"):
        conf["sasl.username"] = kafka["username"]
        conf["sasl.password"] = kafka.get("password", "")
    return conf


def clients(kafka: dict):
    from confluent_kafka import Consumer  # noqa: PLC0415 - optional dependency
    from confluent_kafka.admin import AdminClient  # noqa: PLC0415
    conf = client_config(kafka)
    consumer = Consumer({**conf, "group.id": f"capacity-snapshot-{uuid.uuid4()}", "enable.auto.commit": False,
                         "enable.partition.eof": False, "auto.offset.reset": "latest"})
    return AdminClient(conf), consumer


def topic_metadata(admin, timeout: float = 10) -> dict[str, dict]:
    """{topic: {partitions: [ids], replication}} for every non-internal topic."""
    md = admin.list_topics(timeout=timeout)
    topics = {}
    for name, t in md.topics.items():
        if name.startswith("__") or t.error is not None:
            continue
        parts = sorted(t.partitions)
        replication = len(t.partitions[parts[0]].replicas) if parts else 0
        topics[name] = {"partitions": parts, "replication": replication}
    return topics


def retention_ms(admin, topics: list[str], timeout: float = 10) -> dict[str, int | None]:
    from confluent_kafka.admin import ConfigResource, ResourceType  # noqa: PLC0415
    futures = admin.describe_configs([ConfigResource(ResourceType.TOPIC, t) for t in topics],
                                     request_timeout=timeout)
    result = {}
    for resource, future in futures.items():
        entry = future.result().get("retention.ms")
        result[resource.name] = int(entry.value) if entry is not None and entry.value is not None else None
    return result


def watermarks(consumer, topic: str, partitions: list[int], timeout: float = 10) -> dict[int, tuple[int, int]]:
    from confluent_kafka import TopicPartition  # noqa: PLC0415
    return {p: consumer.get_watermark_offsets(TopicPartition(topic, p), timeout=timeout, cached=False)
            for p in partitions}


def summarize_watermarks(marks: dict[int, tuple[int, int]]) -> dict:
    return {"high_watermark": sum(h for _, h in marks.values()),
            "messages_held": sum(max(0, h - lo) for lo, h in marks.values())}


def message_bytes(msg) -> int:
    headers = msg.headers() or []
    return (len(msg.key() or b"") + len(msg.value() or b"")
            + sum(len(k.encode() if isinstance(k, str) else k) + len(v or b"") for k, v in headers))


def sample_sizes(consumer, topic: str, marks: dict[int, tuple[int, int]], per_partition: int = 50,
                 timeout: float = 10) -> dict:
    """Average bytes over the newest `per_partition` messages of each partition."""
    from confluent_kafka import TopicPartition  # noqa: PLC0415
    targets = {p: h - 1 for p, (lo, h) in marks.items() if h > lo}
    if not targets:
        return {"sampled": 0, "avg_message_bytes": None}
    consumer.assign([TopicPartition(topic, p, max(marks[p][0], marks[p][1] - per_partition)) for p in targets])
    sizes, done = [], set()
    try:
        while len(done) < len(targets):
            batch = consumer.consume(num_messages=500, timeout=timeout)
            if not batch:
                break
            for msg in batch:
                if msg.error():
                    continue
                sizes.append(message_bytes(msg))
                if msg.offset() >= targets[msg.partition()]:
                    done.add(msg.partition())
    finally:
        consumer.unassign()
    return {"sampled": len(sizes), "avg_message_bytes": sum(sizes) / len(sizes) if sizes else None}


def consumer_lag(admin, high_by_topic_partition: dict[tuple[str, int], int], timeout: float = 10) -> dict:
    """{group: {topic: lag}}, comparing committed offsets with the high watermarks just read."""
    from confluent_kafka import ConsumerGroupTopicPartitions  # noqa: PLC0415
    listing = admin.list_consumer_groups(request_timeout=timeout).result()
    lag: dict[str, dict[str, int]] = {}
    for group in sorted(g.group_id for g in listing.valid):
        if group.startswith("capacity-snapshot-"):
            continue
        futures = admin.list_consumer_group_offsets([ConsumerGroupTopicPartitions(group)], request_timeout=timeout)
        for future in futures.values():
            for tp in future.result().topic_partitions:
                high = high_by_topic_partition.get((tp.topic, tp.partition))
                if high is None or tp.offset < 0:
                    continue
                lag.setdefault(group, {})
                lag[group][tp.topic] = lag[group].get(tp.topic, 0) + max(0, high - tp.offset)
    return lag
