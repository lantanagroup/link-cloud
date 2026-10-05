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
import static org.mockito.ArgumentMatchers.any;
import static org.mockito.ArgumentMatchers.anyDouble;
import static org.mockito.ArgumentMatchers.anyString;
import static org.mockito.ArgumentMatchers.eq;
import static org.mockito.ArgumentMatchers.isNull;
import static org.mockito.Mockito.mock;
import static org.mockito.Mockito.never;
import static org.mockito.Mockito.times;
import static org.mockito.Mockito.verify;
import static org.mockito.Mockito.verifyNoInteractions;
import static org.mockito.Mockito.verifyNoMoreInteractions;
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

    /** A Redis read result with the given resources and recorded durable count (null = none recorded). */
    private static RedisCacheEntry entry(Integer durableCount, Resource... resources) {
        return new RedisCacheEntry(List.of(resources), durableCount, false);
    }

    /** A Redis read result whose recorded durable count could not be parsed. */
    private static RedisCacheEntry unparseableCount(Resource... resources) {
        return new RedisCacheEntry(List.of(resources), null, true);
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
        when(redis.readEntry(FACILITY, CORRELATION, PATIENT)).thenReturn(entry(null, resource("p1")));

        reader.readResources(FACILITY, CORRELATION, PATIENT, CORRELATION);

        verify(metrics).recordResourceCacheRead(eq(ResourceCacheReader.OUTCOME_HIT), isNull(), isNull(), anyDouble());
    }

    @Test
    void metrics_redisMiss_recordsFallback_withMissReason() {
        when(redis.readEntry(FACILITY, CORRELATION, PATIENT)).thenReturn(entry(null));
        when(abs.readResources(FACILITY, CORRELATION, PATIENT, CORRELATION)).thenReturn(List.of(resource("p1")));

        reader.readResources(FACILITY, CORRELATION, PATIENT, CORRELATION);

        verify(metrics).recordResourceCacheRead(
                eq(ResourceCacheReader.OUTCOME_FALLBACK), eq(ResourceCacheReader.REASON_MISS), isNull(), anyDouble());
    }

    @Test
    void metrics_partialEntry_recordsFallback_withPartialReason() {
        when(redis.readEntry(FACILITY, CORRELATION, PATIENT)).thenReturn(entry(3, resource("p1")));
        when(abs.readResources(FACILITY, CORRELATION, PATIENT, CORRELATION))
                .thenReturn(List.of(resource("p1"), resource("p2"), resource("p3")));

        reader.readResources(FACILITY, CORRELATION, PATIENT, CORRELATION);

        verify(metrics).recordResourceCacheRead(
                eq(ResourceCacheReader.OUTCOME_FALLBACK), eq(ResourceCacheReader.REASON_PARTIAL), isNull(), anyDouble());
    }

    @Test
    void metrics_redisUnavailable_recordsFallback_withUnavailableReason() {
        when(redis.readEntry(FACILITY, CORRELATION, PATIENT))
                .thenThrow(new ResourceCacheUnavailableException("redis down", new RuntimeException()));
        when(abs.readResources(FACILITY, CORRELATION, PATIENT, CORRELATION)).thenReturn(List.of(resource("p1")));

        reader.readResources(FACILITY, CORRELATION, PATIENT, CORRELATION);

        verify(metrics).recordResourceCacheRead(
                eq(ResourceCacheReader.OUTCOME_FALLBACK), eq(ResourceCacheReader.REASON_UNAVAILABLE), isNull(), anyDouble());
    }

    @Test
    void metrics_bothStoresEmpty_recordsEmpty_withTheReasonRedisMissed() {
        when(redis.readEntry(FACILITY, CORRELATION, PATIENT)).thenReturn(entry(null));
        when(abs.readResources(FACILITY, CORRELATION, PATIENT, CORRELATION)).thenReturn(List.of());

        reader.readResources(FACILITY, CORRELATION, PATIENT, CORRELATION);

        verify(metrics).recordResourceCacheRead(
                eq(ResourceCacheReader.OUTCOME_EMPTY), eq(ResourceCacheReader.REASON_MISS), isNull(), anyDouble());
    }

    @Test
    void metrics_absError_recordsNothing_andStillPropagates() {
        // The read did not produce an outcome -- the record is retried -- so counting it as a hit,
        // fallback or empty would misreport what the cache served.
        when(redis.readEntry(FACILITY, CORRELATION, PATIENT)).thenReturn(entry(null));
        when(abs.readResources(anyString(), anyString(), anyString(), anyString()))
                .thenThrow(new RuntimeException("abs outage"));

        assertThrows(RuntimeException.class,
                () -> reader.readResources(FACILITY, CORRELATION, PATIENT, CORRELATION));

        verify(metrics, never()).recordResourceCacheRead(anyString(), anyString(), isNull(), anyDouble());
        verify(metrics, never()).recordResourceCacheRead(anyString(), isNull(), isNull(), anyDouble());
    }

    @Test
    void metrics_eachRead_recordsExactlyOnce() {
        when(redis.readEntry(FACILITY, CORRELATION, PATIENT)).thenReturn(entry(null, resource("p1")));

        reader.readResources(FACILITY, CORRELATION, PATIENT, CORRELATION);
        reader.readResources(FACILITY, CORRELATION, PATIENT, CORRELATION);

        verify(metrics, times(2)).recordResourceCacheRead(anyString(), isNull(), isNull(), anyDouble());
    }

    // ----- pass (phase) tag -----
    // A bad read in the supplemental pass feeds the submitted report; in the initial pass it only
    // feeds the reportability check. The tag is what lets an alert tell the two apart.

    @Test
    void metrics_recordTheReadsPass_onAHit() {
        when(redis.readEntry(FACILITY, CORRELATION, PATIENT)).thenReturn(entry(null, resource("p1")));

        reader.readResources(FACILITY, CORRELATION, PATIENT, CORRELATION, "Supplemental");

        verify(metrics).recordResourceCacheRead(
                eq(ResourceCacheReader.OUTCOME_HIT), isNull(), eq("Supplemental"), anyDouble());
    }

    @Test
    void metrics_recordTheReadsPass_onAPartialFallback() {
        when(redis.readEntry(FACILITY, CORRELATION, PATIENT)).thenReturn(entry(3, resource("p1")));
        when(abs.readResources(FACILITY, CORRELATION, PATIENT, CORRELATION))
                .thenReturn(List.of(resource("p1"), resource("p2"), resource("p3")));

        reader.readResources(FACILITY, CORRELATION, PATIENT, CORRELATION, "Initial");

        verify(metrics).recordResourceCacheRead(
                eq(ResourceCacheReader.OUTCOME_FALLBACK), eq(ResourceCacheReader.REASON_PARTIAL),
                eq("Initial"), anyDouble());
    }

    // ----- unusable durable counts -----
    // The count arrives in the same read as the resources, so the only way it can be unusable is a
    // value that does not parse. The reader then trusts the entry -- right for one read, but if it
    // happens often the partial-entry protection is effectively off, so each occurrence is counted.

    @Test
    void redisRead_isASingleCall_returningTheResourcesAndTheirCount() {
        // L7: the count used to be a second HGET. An eviction between the two calls made a partial
        // entry look count-less, so it was trusted. One read closes that gap.
        when(redis.readEntry(FACILITY, CORRELATION, PATIENT)).thenReturn(entry(3, resource("p1")));
        when(abs.readResources(FACILITY, CORRELATION, PATIENT, CORRELATION))
                .thenReturn(List.of(resource("p1"), resource("p2"), resource("p3")));

        reader.readResources(FACILITY, CORRELATION, PATIENT, CORRELATION);

        verify(redis, times(1)).readEntry(FACILITY, CORRELATION, PATIENT);
        verifyNoMoreInteractions(redis);
    }

    @Test
    void metrics_unparseableCount_isCounted_withThePass_andTheHitIsStillServed() {
        when(redis.readEntry(FACILITY, CORRELATION, PATIENT)).thenReturn(unparseableCount(resource("p1")));

        List<Resource> result = reader.readResources(FACILITY, CORRELATION, PATIENT, CORRELATION, "Supplemental");

        assertEquals("p1", result.get(0).getResourceId());
        verify(metrics).incrementDurableCountReadFailure("Supplemental");
        verify(metrics).recordResourceCacheRead(
                eq(ResourceCacheReader.OUTCOME_HIT), isNull(), eq("Supplemental"), anyDouble());
    }

    @Test
    void metrics_countReadSuccess_isNotCountedAsAFailure() {
        when(redis.readEntry(FACILITY, CORRELATION, PATIENT)).thenReturn(entry(1, resource("p1")));

        reader.readResources(FACILITY, CORRELATION, PATIENT, CORRELATION, "Supplemental");

        verify(metrics, never()).incrementDurableCountReadFailure(any());
    }

    @Test
    void redisHit_isServedFromRedis_withoutConsultingAbs() {
        when(redis.readEntry(FACILITY, CORRELATION, PATIENT)).thenReturn(entry(null, resource("p1")));

        List<Resource> result = reader.readResources(FACILITY, CORRELATION, PATIENT, CORRELATION);

        assertEquals(1, result.size());
        assertEquals("p1", result.get(0).getResourceId());
        verifyNoInteractions(abs);
    }

    @Test
    void redisEmpty_fallsBackToAbs() {
        // An absent key is indistinguishable from an evicted one; ABS is the authority either way.
        when(redis.readEntry(FACILITY, CORRELATION, PATIENT)).thenReturn(entry(null));
        when(abs.readResources(FACILITY, CORRELATION, PATIENT, CORRELATION)).thenReturn(List.of(resource("p2")));

        List<Resource> result = reader.readResources(FACILITY, CORRELATION, PATIENT, CORRELATION);

        assertEquals(1, result.size());
        assertEquals("p2", result.get(0).getResourceId());
    }

    @Test
    void redisUnavailable_fallsBackToAbs_insteadOfFailing() {
        // A Redis outage must cost latency, never fail the record: the durable copy is in ABS.
        when(redis.readEntry(FACILITY, CORRELATION, PATIENT))
                .thenThrow(new ResourceCacheUnavailableException("redis down", new RuntimeException()));
        when(abs.readResources(FACILITY, CORRELATION, PATIENT, CORRELATION)).thenReturn(List.of(resource("p3")));

        List<Resource> result = reader.readResources(FACILITY, CORRELATION, PATIENT, CORRELATION);

        assertEquals(1, result.size());
        assertEquals("p3", result.get(0).getResourceId());
    }

    @Test
    void bothStoresEmpty_returnsEmpty() {
        when(redis.readEntry(FACILITY, CORRELATION, PATIENT)).thenReturn(entry(null));
        when(abs.readResources(FACILITY, CORRELATION, PATIENT, CORRELATION)).thenReturn(List.of());

        assertTrue(reader.readResources(FACILITY, CORRELATION, PATIENT, CORRELATION).isEmpty());
    }

    @Test
    void absErrors_propagateForRetry() {
        // The durable store being unreachable means the resources are UNKNOWN, not absent: the
        // record must go to the retry ladder rather than evaluate an empty/partial bundle.
        when(redis.readEntry(FACILITY, CORRELATION, PATIENT)).thenReturn(entry(null));
        when(abs.readResources(anyString(), anyString(), anyString(), anyString()))
                .thenThrow(new RuntimeException("abs outage"));

        assertThrows(RuntimeException.class,
                () -> reader.readResources(FACILITY, CORRELATION, PATIENT, CORRELATION));
    }

    @Test
    void redisHit_usesTheCacheKey_notTheCorrelationId() {
        // For today's consumers cacheKey == correlationId, but the reader must key Redis on the
        // cacheKey argument so it stays correct if a record ever advertises a different key.
        when(redis.readEntry(FACILITY, "other-key", PATIENT)).thenReturn(entry(null, resource("p4")));

        List<Resource> result = reader.readResources(FACILITY, CORRELATION, PATIENT, "other-key");

        assertEquals("p4", result.get(0).getResourceId());
        verify(redis).readEntry(FACILITY, "other-key", PATIENT);
        verify(abs, never()).readResources(anyString(), anyString(), anyString(), anyString());
    }

    @Test
    void redisHit_holdingFewerThanTheDurableCount_fallsBackToAbs() {
        // A cache write is a merge that recreates an evicted key, so an entry rebuilt by the
        // supplemental append alone is non-empty yet missing the initial pass. The durable count
        // the .NET writer records is what exposes it; ABS is the whole record.
        when(redis.readEntry(FACILITY, CORRELATION, PATIENT)).thenReturn(entry(3, resource("p1")));
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
        when(redis.readEntry(FACILITY, CORRELATION, PATIENT)).thenReturn(entry(null, resource("p1")));

        List<Resource> result = reader.readResources(FACILITY, CORRELATION, PATIENT, CORRELATION);

        assertEquals("p1", result.get(0).getResourceId());
        verifyNoInteractions(abs);
    }

    @Test
    void redisHit_meetingOrExceedingTheDurableCount_isTrusted() {
        // Holding more than the recorded count is the cache running ahead of a durable write
        // still in flight — the ordinary state between the two writes, not a partial entry.
        when(redis.readEntry(FACILITY, CORRELATION, PATIENT)).thenReturn(entry(1, resource("p1"), resource("p2")));

        List<Resource> result = reader.readResources(FACILITY, CORRELATION, PATIENT, CORRELATION);

        assertEquals(2, result.size());
        verifyNoInteractions(abs);
    }

    @Test
    void redisHit_whenTheDurableCountDoesNotParse_isTrusted() {
        // An unusable count must not demote a usable hit to an ABS read: the entry is treated as
        // whole, as the .NET reader does when it cannot read the count.
        when(redis.readEntry(FACILITY, CORRELATION, PATIENT)).thenReturn(unparseableCount(resource("p1")));

        List<Resource> result = reader.readResources(FACILITY, CORRELATION, PATIENT, CORRELATION);

        assertEquals("p1", result.get(0).getResourceId());
        verifyNoInteractions(abs);
    }
}
