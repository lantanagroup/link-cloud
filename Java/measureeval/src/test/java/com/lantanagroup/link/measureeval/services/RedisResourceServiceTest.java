package com.lantanagroup.link.measureeval.services;

import com.lantanagroup.link.measureeval.entities.Resource;
import com.lantanagroup.link.measureeval.exceptions.ResourceCacheUnavailableException;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.Test;
import org.springframework.dao.QueryTimeoutException;
import org.springframework.data.redis.RedisConnectionFailureException;
import org.springframework.data.redis.core.HashOperations;
import org.springframework.data.redis.core.RedisCallback;
import org.springframework.data.redis.core.ScanOptions;
import org.springframework.data.redis.core.StringRedisTemplate;

import java.util.List;
import java.util.Map;

import static org.junit.jupiter.api.Assertions.*;
import static org.mockito.Mockito.*;

/**
 * Guards that a Redis outage during a cache read surfaces as a retryable
 * {@link ResourceCacheUnavailableException} (wrapping the underlying Spring {@code DataAccessException})
 * rather than being swallowed as an empty cache — so the async consumer routes the record to
 * {@code -Retry} (ResourceCacheUnavailableException is not in {@code KafkaConfig.NON_RETRYABLE}; see
 * {@code KafkaConfigTest}). A genuinely missing key still reads as empty.
 */
class RedisResourceServiceTest {

    @SuppressWarnings("rawtypes")
    private HashOperations hashOps;
    private StringRedisTemplate redisTemplate;
    private RedisResourceService service;

    @BeforeEach
    void setUp() {
        redisTemplate = mock(StringRedisTemplate.class);
        hashOps = mock(HashOperations.class);
        when(redisTemplate.opsForHash()).thenReturn(hashOps);
        service = new RedisResourceService(redisTemplate);
    }

    @Test
    @SuppressWarnings("unchecked")
    void readResources_wrapsAsCacheUnavailable_whenRedisDown() {
        RedisConnectionFailureException cause = new RedisConnectionFailureException("redis unavailable");
        when(hashOps.entries("corr")).thenThrow(cause);

        ResourceCacheUnavailableException ex = assertThrows(ResourceCacheUnavailableException.class,
                () -> service.readResources("fac", "corr", "pat"),
                "a Redis outage must surface as a retryable cache-unavailable error, not be swallowed as empty");
        assertSame(cause, ex.getCause(), "the underlying Redis failure must be preserved as the cause");
    }

    @Test
    @SuppressWarnings("unchecked")
    void readResources_wrapsAsCacheUnavailable_onAnyDataAccessException() {
        // Not just connection failures — a command timeout is an outage too, never a "genuinely absent" key.
        when(hashOps.entries("corr")).thenThrow(new QueryTimeoutException("timed out"));

        assertThrows(ResourceCacheUnavailableException.class,
                () -> service.readResources("fac", "corr", "pat"));
    }

    @Test
    @SuppressWarnings("unchecked")
    void readResources_returnsEmpty_whenKeyMissing() {
        when(hashOps.entries("corr")).thenReturn(Map.of());

        List<Resource> resources = service.readResources("fac", "corr", "pat");

        assertTrue(resources.isEmpty(), "a missing key is a legitimately empty cache, not an outage");
    }

    @Test
    @SuppressWarnings("unchecked")
    void readResources_parsesEntries_whenPresent() {
        when(hashOps.entries("corr")).thenReturn(Map.of(
                "Patient/p1", "{\"resourceType\":\"Patient\",\"id\":\"p1\"}"));

        List<Resource> resources = service.readResources("fac", "corr", "pat");

        assertEquals(1, resources.size());
        assertEquals("p1", resources.get(0).getResourceId());
    }

    @Test
    void cleanup_unlinksOnlyTheCorrelationKey() {
        // MeasureEval owns {correlationId}, the normalized entry it reads. The acquisition keys
        // ({correlationId}:{ResourceType}) belong to Normalization, which deletes them after producing
        // and purges them on every terminal failure; any it fails to delete expire with the TTL.
        // Deleting only the one key also keeps every command single-key, which the OSS clustering
        // policy requires: a multi-key UNLINK across hash slots is rejected with CROSSSLOT.
        service.cleanup("corr-1");

        verify(redisTemplate).unlink("corr-1");
        verify(redisTemplate, never()).unlink(anyCollection());
        verify(redisTemplate, never()).executePipelined(any(RedisCallback.class));
        verify(redisTemplate, never()).scan(any(ScanOptions.class));
    }

    // ----- the durable count, read from the same HGETALL as the resources (L7) -----

    @Test
    @SuppressWarnings("unchecked")
    void readEntry_takesTheDurableCountFromTheSameRead() {
        when(hashOps.entries("corr")).thenReturn(Map.of(
                "Patient/p1", "{\"resourceType\":\"Patient\",\"id\":\"p1\"}",
                RedisResourceService.DURABLE_RESOURCE_COUNT_FIELD, "12"));

        RedisCacheEntry entry = service.readEntry("fac", "corr", "pat");

        assertEquals(12, entry.durableCount());
        assertFalse(entry.durableCountUnparseable());
        assertEquals(1, entry.resources().size(), "the count field is metadata, not a resource");
        verify(hashOps, never()).get(anyString(), anyString());
    }

    @Test
    @SuppressWarnings("unchecked")
    void readEntry_noRecordedCount_isNull() {
        // No durable write has landed for the key yet (or the entry predates the field).
        when(hashOps.entries("corr")).thenReturn(Map.of(
                "Patient/p1", "{\"resourceType\":\"Patient\",\"id\":\"p1\"}"));

        RedisCacheEntry entry = service.readEntry("fac", "corr", "pat");

        assertNull(entry.durableCount());
        assertFalse(entry.durableCountUnparseable());
    }

    @Test
    @SuppressWarnings("unchecked")
    void readEntry_countThatIsNotANumber_isNull_andFlagged() {
        when(hashOps.entries("corr")).thenReturn(Map.of(
                "Patient/p1", "{\"resourceType\":\"Patient\",\"id\":\"p1\"}",
                RedisResourceService.DURABLE_RESOURCE_COUNT_FIELD, "not-a-number"));

        RedisCacheEntry entry = service.readEntry("fac", "corr", "pat");

        assertNull(entry.durableCount(), "a malformed count reads as unrecorded so the entry is trusted");
        assertTrue(entry.durableCountUnparseable(), "and is flagged so the reader can count it");
    }

    @Test
    @SuppressWarnings("unchecked")
    void readEntry_emptyKey_hasNoResourcesAndNoCount() {
        when(hashOps.entries("corr")).thenReturn(Map.of());

        RedisCacheEntry entry = service.readEntry("fac", "corr", "pat");

        assertTrue(entry.resources().isEmpty());
        assertNull(entry.durableCount());
    }

    @Test
    @SuppressWarnings("unchecked")
    void readResources_skipsMetadataFields_withoutWarning() {
        // The writers keep metadata describing the entry in the entry's own hash, so one lifetime
        // covers both and deleting the entry clears its metadata with it. Such a field is dropped
        // either way -- it has no '/' -- so the behaviour under test is the absence of the warning:
        // treating it as malformed would log once per correlation read, on the hottest path there is.
        ch.qos.logback.classic.Logger serviceLogger =
                (ch.qos.logback.classic.Logger) org.slf4j.LoggerFactory.getLogger(RedisResourceService.class);
        ch.qos.logback.core.read.ListAppender<ch.qos.logback.classic.spi.ILoggingEvent> appender =
                new ch.qos.logback.core.read.ListAppender<>();
        appender.start();
        serviceLogger.addAppender(appender);

        try {
            when(hashOps.entries("corr")).thenReturn(Map.of(
                    "Patient/p1", "{\"resourceType\":\"Patient\",\"id\":\"p1\"}",
                    RedisResourceService.METADATA_FIELD_PREFIX + "durableResourceCount", "753"));

            List<Resource> resources = service.readResources("fac", "corr", "pat");

            assertEquals(1, resources.size());
            assertEquals("p1", resources.get(0).getResourceId());

            boolean warned = appender.list.stream()
                    .anyMatch(event -> event.getLevel() == ch.qos.logback.classic.Level.WARN);
            org.junit.jupiter.api.Assertions.assertFalse(
                    warned, "metadata fields must not be reported as malformed");
        } finally {
            serviceLogger.detachAppender(appender);
        }
    }
}
