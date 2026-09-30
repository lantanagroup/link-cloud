package com.lantanagroup.link.measureeval.services;

import com.lantanagroup.link.measureeval.entities.Resource;
import com.lantanagroup.link.measureeval.exceptions.ResourceCacheUnavailableException;
import org.hl7.fhir.r4.model.ResourceType;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.Test;
import org.mockito.ArgumentCaptor;
import org.springframework.dao.QueryTimeoutException;
import org.springframework.data.redis.RedisConnectionFailureException;
import org.springframework.data.redis.core.HashOperations;
import org.springframework.data.redis.core.ScanOptions;
import org.springframework.data.redis.core.StringRedisTemplate;

import java.util.Collection;
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
    @SuppressWarnings("unchecked")
    void cleanup_unlinksTheCorrelationAndEveryAcquisitionKeyItCouldHave() {
        // ABS cleanup sweeps everything under the correlation prefix; the Redis side must forget
        // the correlation just as completely — the bare key plus any surviving
        // {correlationId}:{ResourceType} acquisition keys a dead-lettered correlation left behind.
        // The acquisition keys are enumerable (the FHIR resource types are a closed set), so they
        // are built and unlinked in one round trip rather than discovered with SCAN, which walks
        // the whole shared keyspace once per correlation.
        ArgumentCaptor<Collection<String>> keys = ArgumentCaptor.forClass(Collection.class);

        service.cleanup("corr-1");

        verify(redisTemplate).unlink(keys.capture());
        verify(redisTemplate, never()).scan(any(ScanOptions.class));
        Collection<String> unlinked = keys.getValue();
        assertTrue(unlinked.contains("corr-1"), "the bare correlation key");
        assertTrue(unlinked.contains("corr-1:Encounter"), "an acquisition key");
        assertTrue(unlinked.contains("corr-1:Patient"), "an acquisition key");
        assertTrue(unlinked.contains("corr-1:__cacheType"), "the Hybrid cache-type memo");
        assertEquals(ResourceType.values().length + 2, unlinked.size(),
                "one key per FHIR resource type plus the bare key and the memo, nothing else");
    }

    @Test
    @SuppressWarnings("unchecked")
    void readDurableResourceCount_returnsTheRecordedCount() {
        when(hashOps.get("corr", RedisResourceService.DURABLE_RESOURCE_COUNT_FIELD)).thenReturn("12");

        assertEquals(12, service.readDurableResourceCount("corr"));
    }

    @Test
    @SuppressWarnings("unchecked")
    void readDurableResourceCount_returnsNull_whenNoneRecorded() {
        // No durable write has landed for the key yet (or the entry predates the field).
        when(hashOps.get("corr", RedisResourceService.DURABLE_RESOURCE_COUNT_FIELD)).thenReturn(null);

        assertNull(service.readDurableResourceCount("corr"));
    }

    @Test
    @SuppressWarnings("unchecked")
    void readDurableResourceCount_returnsNull_whenTheValueIsNotANumber() {
        when(hashOps.get("corr", RedisResourceService.DURABLE_RESOURCE_COUNT_FIELD)).thenReturn("not-a-number");

        assertNull(service.readDurableResourceCount("corr"),
                "a malformed count must read as unrecorded so the entry is trusted rather than rejected");
    }

    @Test
    @SuppressWarnings("unchecked")
    void readDurableResourceCount_wrapsAsCacheUnavailable_whenRedisDown() {
        RedisConnectionFailureException cause = new RedisConnectionFailureException("redis unavailable");
        when(hashOps.get("corr", RedisResourceService.DURABLE_RESOURCE_COUNT_FIELD)).thenThrow(cause);

        ResourceCacheUnavailableException ex = assertThrows(ResourceCacheUnavailableException.class,
                () -> service.readDurableResourceCount("corr"));
        assertSame(cause, ex.getCause());
    }
}
