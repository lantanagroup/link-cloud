package com.lantanagroup.link.measureeval.services;

import org.springframework.data.redis.connection.RedisConnection;
import org.springframework.data.redis.connection.RedisConnectionFactory;
import org.springframework.data.redis.core.ScanOptions;
import org.springframework.data.redis.core.StringRedisTemplate;
import org.springframework.data.redis.core.script.DefaultRedisScript;
import org.springframework.stereotype.Service;

import jakarta.annotation.PostConstruct;
import jakarta.annotation.PreDestroy;

import java.nio.charset.StandardCharsets;
import java.util.ArrayList;
import java.util.List;

import org.slf4j.Logger;
import org.slf4j.LoggerFactory;

@Service
public class ReportResourceCache {
    private static final Logger logger = LoggerFactory.getLogger(ReportResourceCache.class);
    private static final String KEY_PREFIX_EVAL_PERSISTED = "reportResources:evalPersisted:";
    private static final String KEY_PREFIX_EVAL_PERSISTENCE_REMAINING = "reportResources:evalPersistenceRemaining:";
    private static final String KEY_PREFIX_REPORT_RESOURCES_LIST = "reportResources:list:";
    private final RedisConnectionFactory connectionFactory;
    private RedisConnection connection;
    private final StringRedisTemplate redis;
    private final DefaultRedisScript<Long> persistedRemainingScript;

    public ReportResourceCache(RedisConnectionFactory connectionFactory, StringRedisTemplate redis) {
        this.connectionFactory = connectionFactory;
        this.redis = redis;

        String lua =
                """
local rem = redis.call('DECR', KEYS[1])
if rem == 0 then
  local alreadyDone = redis.call('GET', KEYS[2])
  if alreadyDone ~= 'true' then
    redis.call('SET', KEYS[2], 'true')
    redis.call('PUBLISH', KEYS[2], 'evaluate')
  end
end
return rem
                """;

        this.persistedRemainingScript = new DefaultRedisScript<>();
        this.persistedRemainingScript.setScriptText(lua);
        this.persistedRemainingScript.setResultType(Long.class);
    }

    @PostConstruct
    public void init() {
        try {
            this.connection = connectionFactory.getConnection();
        } catch (Exception e) {
            logger.error("Error initializing Redis connection", e);
            throw new RuntimeException(e);
        }

        try {
            // Scan for existing keys
            var scanArgs = ScanOptions.scanOptions()
                    .match(KEY_PREFIX_EVAL_PERSISTED + "*")
                    .count(1000)
                    .build();

            try (var cursor = connection.keyCommands().scan(scanArgs)) {
                while (cursor.hasNext()) {
                    String key = new String(cursor.next(), StandardCharsets.UTF_8);
                    if (key.startsWith("reportResources:evalPersisted:")) {
                        String correlationId = key.substring("reportResources:evalPersisted:".length());
                        logger.debug("Found existing evalPersisted for correlationId {}", correlationId);
                    }
                }
            }

            // Subscribe to future changes
            this.connection.pSubscribe((message, pattern) -> {
                String channel = new String(message.getChannel(), StandardCharsets.UTF_8);
                String correlationId = channel.substring("reportResources:evalPersisted:".length());
                logger.debug("Persistence of resources for correlationId {} complete", correlationId);
            }, "reportResources:evalPersisted:*".getBytes());
        } catch (Exception e) {
            logger.error("Error initializing Redis subscription for resource persistence", e);
        }
    }

    @PreDestroy
    public void cleanup() {
        if (this.connection != null) {
            this.connection.close();
        }
    }

    public String getSerializedReportResources(String correlationId) {
        String key = KEY_PREFIX_REPORT_RESOURCES_LIST + correlationId;

        byte[] value = connection.stringCommands().get(key.getBytes());

        if (value != null) {
            return new String(value, StandardCharsets.UTF_8);
        }

        return null;
    }

    public List<String> getReportResources(String correlationId) {
        String serializedResources = this.getSerializedReportResources(correlationId);
        String[] lines = serializedResources.split("\n");
        return new ArrayList<>(List.of(lines));
    }

    public void resourcePersisted(String correlationId) {
        String remainingKey = KEY_PREFIX_EVAL_PERSISTENCE_REMAINING + correlationId;
        String doneKey = KEY_PREFIX_EVAL_PERSISTED + correlationId;

        Long remaining;
        try {
            remaining = redis.execute(
                    this.persistedRemainingScript,
                    List.of(remainingKey, doneKey),             // KEYS
                    correlationId                               // ARGS[1]
            );
            logger.debug("Remaining resources to persist for correlationId {}: {}", correlationId, remaining);
        } catch (Exception e) {
            logger.error("Error executing Redis script for correlationId: {}", correlationId, e);
            throw new RuntimeException("Failed to execute Redis script", e);
        }
    }

    public void reportEvaluated(String correlationId) {
        String evalPersistedKey = KEY_PREFIX_EVAL_PERSISTED + correlationId;
        String evalPersistenceRemainingKey = KEY_PREFIX_EVAL_PERSISTENCE_REMAINING + correlationId;

        try {
            connection.keyCommands().del(evalPersistedKey.getBytes());
            logger.debug("Deleted evalPersisted key for correlationId: {}", correlationId);
        } catch (Exception e) {
            logger.error("Error deleting evalPersisted key for correlationId: {}", correlationId, e);
        }

        try {
            connection.keyCommands().del(evalPersistenceRemainingKey.getBytes());
            logger.debug("Deleted evalPersistenceRemaining key for correlationId: {}", correlationId);
        } catch (Exception e) {
            logger.error("Error deleting evalPersistenceRemaining key for correlationId: {}", correlationId, e);
        }
    }
}
