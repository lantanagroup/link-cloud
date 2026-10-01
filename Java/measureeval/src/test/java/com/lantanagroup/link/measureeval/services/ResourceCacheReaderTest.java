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
import static org.mockito.ArgumentMatchers.anyDouble;
import static org.mockito.ArgumentMatchers.anyString;
import static org.mockito.ArgumentMatchers.eq;
import static org.mockito.ArgumentMatchers.isNull;
import static org.mockito.Mockito.mock;
import static org.mockito.Mockito.never;
import static org.mockito.Mockito.times;
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
    private MeasureEvalMetrics metrics;
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
        metrics = mock(MeasureEvalMetrics.class);
        reader = new ResourceCacheReader(redis, abs, metrics);
    }

    // ----- read-outcome metrics -----
    // Same instrument and outcome values as the .NET HybridResourceCache records, so one dashboard
    // covers both runtimes. MeasureEval's read is the one that feeds CQL, so it is where a rejected
    // partial entry matters most -- the fallback reason is what makes that visible.

    @Test
    void metrics_redisHit_recordsHit_withNoFallbackReason() {
        when(redis.readResources(FACILITY, CORRELATION, PATIENT)).thenReturn(List.of(resource("p1")));

        reader.readResources(FACILITY, CORRELATION, PATIENT, CORRELATION);

        verify(metrics).recordResourceCacheRead(eq(ResourceCacheReader.OUTCOME_HIT), isNull(), anyDouble());
    }

    @Test
    void metrics_redisMiss_recordsFallback_withMissReason() {
        when(redis.readResources(FACILITY, CORRELATION, PATIENT)).thenReturn(List.of());
        when(abs.readResources(FACILITY, CORRELATION, PATIENT, CORRELATION)).thenReturn(List.of(resource("p1")));

        reader.readResources(FACILITY, CORRELATION, PATIENT, CORRELATION);

        verify(metrics).recordResourceCacheRead(
                eq(ResourceCacheReader.OUTCOME_FALLBACK), eq(ResourceCacheReader.REASON_MISS), anyDouble());
    }

    @Test
    void metrics_partialEntry_recordsFallback_withPartialReason() {
        when(redis.readResources(FACILITY, CORRELATION, PATIENT)).thenReturn(List.of(resource("p1")));
        when(redis.readDurableResourceCount(CORRELATION)).thenReturn(3);
        when(abs.readResources(FACILITY, CORRELATION, PATIENT, CORRELATION))
                .thenReturn(List.of(resource("p1"), resource("p2"), resource("p3")));

        reader.readResources(FACILITY, CORRELATION, PATIENT, CORRELATION);

        verify(metrics).recordResourceCacheRead(
                eq(ResourceCacheReader.OUTCOME_FALLBACK), eq(ResourceCacheReader.REASON_PARTIAL), anyDouble());
    }

    @Test
    void metrics_redisUnavailable_recordsFallback_withUnavailableReason() {
        when(redis.readResources(FACILITY, CORRELATION, PATIENT))
                .thenThrow(new ResourceCacheUnavailableException("redis down", new RuntimeException()));
        when(abs.readResources(FACILITY, CORRELATION, PATIENT, CORRELATION)).thenReturn(List.of(resource("p1")));

        reader.readResources(FACILITY, CORRELATION, PATIENT, CORRELATION);

        verify(metrics).recordResourceCacheRead(
                eq(ResourceCacheReader.OUTCOME_FALLBACK), eq(ResourceCacheReader.REASON_UNAVAILABLE), anyDouble());
    }

    @Test
    void metrics_bothStoresEmpty_recordsEmpty_withTheReasonRedisMissed() {
        when(redis.readResources(FACILITY, CORRELATION, PATIENT)).thenReturn(List.of());
        when(abs.readResources(FACILITY, CORRELATION, PATIENT, CORRELATION)).thenReturn(List.of());

        reader.readResources(FACILITY, CORRELATION, PATIENT, CORRELATION);

        verify(metrics).recordResourceCacheRead(
                eq(ResourceCacheReader.OUTCOME_EMPTY), eq(ResourceCacheReader.REASON_MISS), anyDouble());
    }

    @Test
    void metrics_absError_recordsNothing_andStillPropagates() {
        // The read did not produce an outcome -- the record is retried -- so counting it as a hit,
        // fallback or empty would misreport what the cache served.
        when(redis.readResources(FACILITY, CORRELATION, PATIENT)).thenReturn(List.of());
        when(abs.readResources(anyString(), anyString(), anyString(), anyString()))
                .thenThrow(new RuntimeException("abs outage"));

        assertThrows(RuntimeException.class,
                () -> reader.readResources(FACILITY, CORRELATION, PATIENT, CORRELATION));

        verify(metrics, never()).recordResourceCacheRead(anyString(), anyString(), anyDouble());
        verify(metrics, never()).recordResourceCacheRead(anyString(), isNull(), anyDouble());
    }

    @Test
    void metrics_eachRead_recordsExactlyOnce() {
        when(redis.readResources(FACILITY, CORRELATION, PATIENT)).thenReturn(List.of(resource("p1")));

        reader.readResources(FACILITY, CORRELATION, PATIENT, CORRELATION);
        reader.readResources(FACILITY, CORRELATION, PATIENT, CORRELATION);

        verify(metrics, times(2)).recordResourceCacheRead(anyString(), isNull(), anyDouble());
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

    @Test
    void redisHit_holdingFewerThanTheDurableCount_fallsBackToAbs() {
        // A cache write is a merge that recreates an evicted key, so an entry rebuilt by the
        // supplemental append alone is non-empty yet missing the initial pass. The durable count
        // the .NET writer records is what exposes it; ABS is the whole record.
        when(redis.readResources(FACILITY, CORRELATION, PATIENT)).thenReturn(List.of(resource("p1")));
        when(redis.readDurableResourceCount(CORRELATION)).thenReturn(3);
        when(abs.readResources(FACILITY, CORRELATION, PATIENT, CORRELATION))
                .thenReturn(List.of(resource("p1"), resource("p2"), resource("p3")));

        List<Resource> result = reader.readResources(FACILITY, CORRELATION, PATIENT, CORRELATION);

        assertEquals(3, result.size());
        verify(abs).readResources(FACILITY, CORRELATION, PATIENT, CORRELATION);
    }

    @Test
    void redisHit_withNoRecordedDurableCount_isTrusted() {
        // No count means no durable write has landed for the key, so ABS has nothing more to
        // offer; falling back would turn a usable entry into an empty read.
        when(redis.readResources(FACILITY, CORRELATION, PATIENT)).thenReturn(List.of(resource("p1")));
        when(redis.readDurableResourceCount(CORRELATION)).thenReturn(null);

        List<Resource> result = reader.readResources(FACILITY, CORRELATION, PATIENT, CORRELATION);

        assertEquals("p1", result.get(0).getResourceId());
        verifyNoInteractions(abs);
    }

    @Test
    void redisHit_meetingOrExceedingTheDurableCount_isTrusted() {
        // Holding more than the recorded count is the cache running ahead of a durable write
        // still in flight — the ordinary state between the two writes, not a partial entry.
        when(redis.readResources(FACILITY, CORRELATION, PATIENT)).thenReturn(List.of(resource("p1"), resource("p2")));
        when(redis.readDurableResourceCount(CORRELATION)).thenReturn(1);

        List<Resource> result = reader.readResources(FACILITY, CORRELATION, PATIENT, CORRELATION);

        assertEquals(2, result.size());
        verifyNoInteractions(abs);
    }

    @Test
    void redisHit_whenTheDurableCountCannotBeRead_isTrusted() {
        // Failing to read the count must not demote a usable hit to an ABS read: the entry is
        // treated as whole, as the .NET reader does.
        when(redis.readResources(FACILITY, CORRELATION, PATIENT)).thenReturn(List.of(resource("p1")));
        when(redis.readDurableResourceCount(CORRELATION))
                .thenThrow(new ResourceCacheUnavailableException("redis down", new RuntimeException()));

        List<Resource> result = reader.readResources(FACILITY, CORRELATION, PATIENT, CORRELATION);

        assertEquals("p1", result.get(0).getResourceId());
        verifyNoInteractions(abs);
    }
}
