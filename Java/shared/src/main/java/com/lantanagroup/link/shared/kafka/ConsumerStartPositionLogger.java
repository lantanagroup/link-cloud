package com.lantanagroup.link.shared.kafka;

import org.apache.kafka.clients.consumer.Consumer;
import org.apache.kafka.clients.consumer.ConsumerConfig;
import org.apache.kafka.clients.consumer.OffsetAndMetadata;
import org.apache.kafka.common.TopicPartition;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.kafka.listener.ConsumerAwareRebalanceListener;

import java.time.Duration;
import java.util.Collection;
import java.util.List;
import java.util.Map;
import java.util.Set;
import java.util.TreeMap;
import java.util.stream.Collectors;

/**
 * Says where a consumer group starts reading each time partitions are assigned to it: the committed
 * offset it resumes from, or, for a partition with none, the {@code auto.offset.reset} position it falls
 * back to. A group with no committed offset that starts at {@code latest} silently skips whatever is
 * already on the topic, so that case is logged as a warning. The .NET services log the same from the
 * shared consumer factory.
 */
public class ConsumerStartPositionLogger implements ConsumerAwareRebalanceListener {

    private static final Logger logger = LoggerFactory.getLogger(ConsumerStartPositionLogger.class);

    private final String offsetReset;

    /** @param offsetReset the consumers' {@code auto.offset.reset}; null means Kafka's default, {@code latest}. */
    public ConsumerStartPositionLogger(String offsetReset) {
        this.offsetReset = offsetReset == null || offsetReset.isBlank() ? "latest" : offsetReset;
    }

    /** Reads {@code auto.offset.reset} from a consumer factory's configuration. */
    public static ConsumerStartPositionLogger forConfig(Map<String, Object> consumerConfig) {
        Object reset = consumerConfig == null ? null : consumerConfig.get(ConsumerConfig.AUTO_OFFSET_RESET_CONFIG);
        return new ConsumerStartPositionLogger(reset == null ? null : reset.toString());
    }

    @Override
    public void onPartitionsAssigned(Consumer<?, ?> consumer, Collection<TopicPartition> partitions) {
        if (partitions == null || partitions.isEmpty()) {
            return;
        }
        String groupId = consumer.groupMetadata() != null ? consumer.groupMetadata().groupId() : "-";
        try {
            Map<TopicPartition, OffsetAndMetadata> committed = consumer.committed(Set.copyOf(partitions), Duration.ofSeconds(5));
            Map<String, List<TopicPartition>> byTopic = partitions.stream()
                    .collect(Collectors.groupingBy(TopicPartition::topic, TreeMap::new, Collectors.toList()));

            List<TopicPartition> withoutCommitted = partitions.stream().filter(tp -> committed.get(tp) == null).toList();
            Map<TopicPartition, Long> beginning = withoutCommitted.isEmpty() ? Map.of()
                    : consumer.beginningOffsets(withoutCommitted, Duration.ofSeconds(5));
            Map<TopicPartition, Long> end = withoutCommitted.isEmpty() ? Map.of()
                    : consumer.endOffsets(withoutCommitted, Duration.ofSeconds(5));

            for (Map.Entry<String, List<TopicPartition>> topic : byTopic.entrySet()) {
                String committedText = topic.getValue().stream()
                        .filter(tp -> committed.get(tp) != null)
                        .map(tp -> tp.partition() + "@" + committed.get(tp).offset())
                        .collect(Collectors.joining(", "));
                if (committedText.isEmpty()) {
                    committedText = "none";
                }

                // A partition with no committed offset only loses messages if it already holds some, so the
                // warning is kept for those; an empty partition starts the same wherever it starts.
                List<TopicPartition> unset = topic.getValue().stream().filter(tp -> committed.get(tp) == null).toList();
                String holding = unset.stream()
                        .filter(tp -> end.getOrDefault(tp, 0L) > beginning.getOrDefault(tp, 0L))
                        .map(tp -> Integer.toString(tp.partition())).collect(Collectors.joining(","));
                String all = unset.stream().map(tp -> Integer.toString(tp.partition())).collect(Collectors.joining(","));

                if (unset.isEmpty()) {
                    logger.info("Consumer group {} assigned {}: resuming from committed offsets {}",
                            groupId, topic.getKey(), committedText);
                } else if (!holding.isEmpty() && "latest".equalsIgnoreCase(offsetReset)) {
                    logger.warn("Consumer group {} assigned {}: partitions {} hold messages but have no committed offset, and start at {}, skipping the messages already there; committed offsets {}",
                            groupId, topic.getKey(), holding, offsetReset, committedText);
                } else {
                    logger.info("Consumer group {} assigned {}: partitions {} have no committed offset and start at {}{}; committed offsets {}",
                            groupId, topic.getKey(), all, offsetReset, holding.isEmpty() ? " (they are empty)" : "", committedText);
                }
            }
        } catch (Exception e) {
            logger.warn("Could not read committed offsets for consumer group {}", groupId, e);
        }
    }
}
