# Kafka key proof

This directory starts a KRaft broker and runs the key proof tests against it. The compose file publishes no host ports and sets no container names. Join the `link-kafka-key-proof` network from the test runner, or set `KAFKA_BOOTSTRAP` to an address that runner can reach.

Single broker:

```
docker compose -f Scripts/kafka-key-proof/docker-compose.yml up -d
```

Three brokers (its own file, so the single-broker file stays a one-node quorum):

```
docker compose -f Scripts/kafka-key-proof/docker-compose.cluster.yml up -d
```

Run the proof:

```
KAFKA_BOOTSTRAP=kafka-1:9092 Scripts/kafka-key-proof/run-proof.sh
```

`run-proof.sh` runs `DotNet/KafkaKeyProof.Tests`, which does not reference Automation. Tests that need a broker return immediately when `KAFKA_BOOTSTRAP` is unset. With the variable set, `JAVA_HOME` and `MAVEN_HOME` are required, and the compare fails unless both the .NET and Java result rows are present and agree. A .NET producer using `Murmur2Random` and a Java producer using the platform default must place the same `DataAcquisitionRequested` key on the same partition for 3, 6, and 12 partitions. The key is the canonical JSON object `{"facilityId":"...","patientId":"..."}`. Expected bytes and Murmur2 partitions for 3, 6, 12, and 24 partitions are in `Tests/fixtures/kafka-key-golden.json`. The proof tests locate that file from the repository root by directory name, so the lookup does not depend on the casing of the path.

The same project covers retry topic names, cooperative revoke of a subset of partitions, and partition growth of a keyed topic when the broker is available. Retry and redrive topics are named `{topic}-Retry-{service}` and `{topic}-Redrive-{service}`. A failed message is produced to the service retry topic and, after backoff, to that service's redrive topic. It is not written back to the shared main topic.

Deploy note: changing the partitioner and the key moves existing messages onto new partitions. Each .NET consumer group must take the cooperative-sticky release on all of its pods together. Java consumers can roll one pod at a time.
