package com.lantanagroup.link.shared.kafka;

import com.fasterxml.jackson.databind.JsonNode;
import com.fasterxml.jackson.databind.ObjectMapper;
import org.junit.jupiter.api.Test;

import java.io.IOException;
import java.nio.file.Files;
import java.nio.file.Path;

import static org.junit.jupiter.api.Assertions.assertEquals;

class KafkaKeyGoldenTest {

    @Test
    void fixtureMatchesCanonicalBytesAndMurmur2Partitions() throws Exception {
        Path root = findRepoRoot();
        JsonNode rows = new ObjectMapper().readTree(goldenFixture(root).toFile());
        StringBuilder mismatches = new StringBuilder();
        for (JsonNode row : rows) {
            String facilityId = row.get("facilityId").asText();
            JsonNode patientNode = row.get("patientId");
            String patientId = patientNode == null || patientNode.isNull() ? null : patientNode.asText();
            String expectedKey = row.get("key").asText();
            String actualKey = patientId == null || patientId.isEmpty()
                    ? KafkaKeys.forFacility(facilityId)
                    : KafkaKeys.forPatient(facilityId, patientId);
            if (!expectedKey.equals(actualKey)) {
                mismatches.append(row.get("name").asText()).append(" key expected ").append(expectedKey)
                        .append(" actual ").append(actualKey).append("; ");
            }
            for (int partitionCount : new int[] {3, 6, 12, 24}) {
                int actualPartition = KafkaMurmur2.partition(actualKey, partitionCount);
                int expectedPartition = row.get("partitions").get(Integer.toString(partitionCount)).asInt();
                if (expectedPartition != actualPartition) {
                    mismatches.append(row.get("name").asText()).append(" p").append(partitionCount)
                            .append(" expected ").append(expectedPartition)
                            .append(" actual ").append(actualPartition).append("; ");
                }
            }
        }
        assertEquals("", mismatches.toString());
    }

    private static Path findRepoRoot() {
        Path dir = Path.of("").toAbsolutePath();
        while (dir != null) {
            if (Files.exists(dir.resolve("topics.txt"))) {
                return dir;
            }
            dir = dir.getParent();
        }
        throw new IllegalStateException("Could not find the repository root.");
    }

    private static Path goldenFixture(Path root) throws IOException {
        Path tests = child(root, "tests");
        Path fixtures = child(tests, "fixtures");
        try (var files = Files.list(fixtures)) {
            return files
                    .filter(path -> path.getFileName().toString().equalsIgnoreCase("kafka-key-golden.json"))
                    .findFirst()
                    .orElseThrow(() -> new IllegalStateException("Kafka key golden fixture was not found under the repository root."));
        }
    }

    private static Path child(Path parent, String name) throws IOException {
        try (var children = Files.list(parent)) {
            return children
                    .filter(path -> Files.isDirectory(path) && path.getFileName().toString().equalsIgnoreCase(name))
                    .findFirst()
                    .orElseThrow(() -> new IllegalStateException("Could not find " + name + " under " + parent));
        }
    }
}
