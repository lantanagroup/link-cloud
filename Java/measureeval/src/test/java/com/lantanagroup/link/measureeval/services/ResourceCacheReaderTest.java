package com.lantanagroup.link.measureeval.services;

import com.lantanagroup.link.measureeval.entities.Resource;
import com.lantanagroup.link.measureeval.exceptions.ResourceCacheUnavailableException;
import org.hl7.fhir.r4.model.ResourceType;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.Test;

import java.util.List;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertThrows;
import static org.junit.jupiter.api.Assertions.assertTrue;
import static org.mockito.ArgumentMatchers.anyString;
import static org.mockito.Mockito.mock;
import static org.mockito.Mockito.never;
import static org.mockito.Mockito.verify;
import static org.mockito.Mockito.verifyNoInteractions;
import static org.mockito.Mockito.when;

/**
 * The Redis-first/ABS-fallback contract (LEGLINK-1279): Redis serves when it has data; an absent,
 * empty or unreachable Redis falls back to ABS (the durable source) instead of failing; ABS
 * errors still propagate because an unreachable authority means retry, not empty.
 */
class ResourceCacheReaderTest {

    private static final String FACILITY = "facility-1";
    private static final String CORRELATION = "corr-1";
    private static final String PATIENT = "patient-1";

    private RedisResourceService redis;
    private AbsResourceService abs;
    private ResourceCacheReader reader;

    private static Resource resource(String id) {
        Resource resource = new Resource();
        resource.setResourceType(ResourceType.Patient);
        resource.setResourceId(id);
        return resource;
    }

    @BeforeEach
    void setUp() {
        redis = mock(RedisResourceService.class);
        abs = mock(AbsResourceService.class);
        reader = new ResourceCacheReader(redis, abs);
    }

    @Test
    void redisHit_isServedFromRedis_withoutConsultingAbs() {
        when(redis.readResources(FACILITY, CORRELATION, PATIENT)).thenReturn(List.of(resource("p1")));

        List<Resource> result = reader.readResources(FACILITY, CORRELATION, PATIENT, CORRELATION);

        assertEquals(1, result.size());
        assertEquals("p1", result.get(0).getResourceId());
        verifyNoInteractions(abs);
    }

    @Test
    void redisEmpty_fallsBackToAbs() {
        // An absent key is indistinguishable from an evicted one; ABS is the authority either way.
        when(redis.readResources(FACILITY, CORRELATION, PATIENT)).thenReturn(List.of());
        when(abs.readResources(FACILITY, CORRELATION, PATIENT, CORRELATION)).thenReturn(List.of(resource("p2")));

        List<Resource> result = reader.readResources(FACILITY, CORRELATION, PATIENT, CORRELATION);

        assertEquals(1, result.size());
        assertEquals("p2", result.get(0).getResourceId());
    }

    @Test
    void redisUnavailable_fallsBackToAbs_insteadOfFailing() {
        // A Redis outage must cost latency, never fail the record: the durable copy is in ABS.
        when(redis.readResources(FACILITY, CORRELATION, PATIENT))
                .thenThrow(new ResourceCacheUnavailableException("redis down", new RuntimeException()));
        when(abs.readResources(FACILITY, CORRELATION, PATIENT, CORRELATION)).thenReturn(List.of(resource("p3")));

        List<Resource> result = reader.readResources(FACILITY, CORRELATION, PATIENT, CORRELATION);

        assertEquals(1, result.size());
        assertEquals("p3", result.get(0).getResourceId());
    }

    @Test
    void bothStoresEmpty_returnsEmpty() {
        when(redis.readResources(FACILITY, CORRELATION, PATIENT)).thenReturn(List.of());
        when(abs.readResources(FACILITY, CORRELATION, PATIENT, CORRELATION)).thenReturn(List.of());

        assertTrue(reader.readResources(FACILITY, CORRELATION, PATIENT, CORRELATION).isEmpty());
    }

    @Test
    void absErrors_propagateForRetry() {
        // The durable store being unreachable means the resources are UNKNOWN, not absent: the
        // record must go to the retry ladder rather than evaluate an empty/partial bundle.
        when(redis.readResources(FACILITY, CORRELATION, PATIENT)).thenReturn(List.of());
        when(abs.readResources(anyString(), anyString(), anyString(), anyString()))
                .thenThrow(new RuntimeException("abs outage"));

        assertThrows(RuntimeException.class,
                () -> reader.readResources(FACILITY, CORRELATION, PATIENT, CORRELATION));
    }

    @Test
    void redisHit_usesTheCacheKey_notTheCorrelationId() {
        // For today's consumers cacheKey == correlationId, but the reader must key Redis on the
        // cacheKey argument so it stays correct if a record ever advertises a different key.
        when(redis.readResources(FACILITY, "other-key", PATIENT)).thenReturn(List.of(resource("p4")));

        List<Resource> result = reader.readResources(FACILITY, CORRELATION, PATIENT, "other-key");

        assertEquals("p4", result.get(0).getResourceId());
        verify(redis).readResources(FACILITY, "other-key", PATIENT);
        verify(abs, never()).readResources(anyString(), anyString(), anyString(), anyString());
    }
}
