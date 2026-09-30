package com.lantanagroup.link.measureeval.services;

import com.lantanagroup.link.measureeval.entities.Resource;
import com.lantanagroup.link.measureeval.exceptions.ResourceCacheUnavailableException;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.Test;
import org.springframework.dao.QueryTimeoutException;
import org.springframework.data.redis.RedisConnectionFailureException;
import org.springframework.data.redis.core.Cursor;
import org.springframework.data.redis.core.HashOperations;
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
    @SuppressWarnings("unchecked")
    void cleanup_sweepsTheCorrelationAndItsAcquisitionKeys() {
        // ABS cleanup sweeps everything under the correlation prefix; the Redis side must forget
        // the correlation just as completely — the bare key plus any surviving
        // {correlationId}:{ResourceType} acquisition keys a dead-lettered correlation left behind.
        Cursor<String> cursor = mock(Cursor.class);
        // forEachRemaining is a default method, which Mockito intercepts like any other — stub it
        // directly rather than hasNext/next, since it is what cleanup() actually calls.
        doAnswer(invocation -> {
            java.util.function.Consumer<String> action = invocation.getArgument(0);
            action.accept("corr-1:Patient");
            action.accept("corr-1:Encounter");
            return null;
        }).when(cursor).forEachRemaining(any());
        when(redisTemplate.scan(any(ScanOptions.class))).thenReturn(cursor);

        service.cleanup("corr-1");

        verify(redisTemplate).unlink(List.of("corr-1", "corr-1:Patient", "corr-1:Encounter"));
    }

    @Test
    @SuppressWarnings("unchecked")
    void cleanup_unlinksTheBareKey_whenNoAcquisitionKeysSurvive() {
        Cursor<String> cursor = mock(Cursor.class);
        when(cursor.hasNext()).thenReturn(false);
        when(redisTemplate.scan(any(ScanOptions.class))).thenReturn(cursor);

        service.cleanup("corr-1");

        verify(redisTemplate).unlink(List.of("corr-1"));
    }
}
