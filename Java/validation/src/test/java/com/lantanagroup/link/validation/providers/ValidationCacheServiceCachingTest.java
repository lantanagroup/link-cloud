package com.lantanagroup.link.validation.providers;

import ca.uhn.fhir.context.support.IValidationSupport;
import com.github.benmanes.caffeine.cache.Caffeine;
import org.junit.jupiter.api.Test;
import org.springframework.cache.Cache;
import org.springframework.cache.CacheManager;
import org.springframework.cache.caffeine.CaffeineCacheManager;
import org.springframework.context.annotation.AnnotationConfigApplicationContext;
import org.springframework.context.annotation.Bean;
import org.springframework.context.annotation.Configuration;
import org.springframework.data.redis.cache.RedisCacheConfiguration;
import org.springframework.data.redis.cache.RedisCacheManager;
import org.springframework.data.redis.cache.RedisCacheWriter;

import org.hl7.fhir.r4.model.ValueSet;

import java.time.Duration;
import java.util.ArrayList;
import java.util.Base64;
import java.util.HashMap;
import java.util.List;
import java.util.Map;
import java.util.Objects;
import java.util.Set;
import java.util.concurrent.CountDownLatch;
import java.util.concurrent.ExecutorService;
import java.util.concurrent.Executors;
import java.util.concurrent.Future;
import java.util.concurrent.TimeUnit;
import java.util.concurrent.atomic.AtomicInteger;
import java.util.concurrent.atomic.AtomicReference;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertNotEquals;
import static org.junit.jupiter.api.Assertions.assertNotNull;
import static org.junit.jupiter.api.Assertions.assertNull;
import static org.junit.jupiter.api.Assertions.assertSame;
import static org.junit.jupiter.api.Assertions.assertTrue;
import static org.mockito.ArgumentMatchers.any;
import static org.mockito.ArgumentMatchers.anyString;
import static org.mockito.ArgumentMatchers.isNull;
import static org.mockito.Mockito.doAnswer;
import static org.mockito.Mockito.mock;
import static org.mockito.Mockito.times;
import static org.mockito.Mockito.verify;
import static org.mockito.Mockito.when;

class ValidationCacheServiceCachingTest {
    private static final String CODE_SYSTEM = "http://example.org/system";
    private static final String DISPLAY = "Example display";
    private static final String VALUE_SET_URL = "http://example.org/ValueSet/example";

    @Test
    void validateCodeCacheKey_keepsObjectsHashCollisionSeparateInBothCacheBackends() {
        ValidationCacheService cacheService = new ValidationCacheService();
        String aaKey = cacheService.validateCodeCacheKey(CODE_SYSTEM, "Aa", DISPLAY, VALUE_SET_URL);
        String bbKey = cacheService.validateCodeCacheKey(CODE_SYSTEM, "BB", DISPLAY, VALUE_SET_URL);

        assertEquals(
                Objects.hash(CODE_SYSTEM, "Aa", DISPLAY, VALUE_SET_URL),
                Objects.hash(CODE_SYSTEM, "BB", DISPLAY, VALUE_SET_URL),
                "fixed regression inputs must collide under the former Objects.hash cache key");
        assertNotEquals(aaKey, bbKey);

        CaffeineCacheManager caffeineCacheManager = new CaffeineCacheManager("validateCodeCache");
        caffeineCacheManager.setCaffeine(Caffeine.newBuilder());
        assertStoresSeparateEntries(caffeineCacheManager.getCache("validateCodeCache"), aaKey, bbKey);

        Map<String, byte[]> redisEntries = new HashMap<>();
        RedisCacheWriter redisCacheWriter = mock(RedisCacheWriter.class);
        when(redisCacheWriter.get(anyString(), any(byte[].class))).thenAnswer(invocation ->
                redisEntries.get(serializeKey(invocation.getArgument(1))));
        doAnswer(invocation -> {
            redisEntries.put(serializeKey(invocation.getArgument(1)), invocation.getArgument(2));
            return null;
        }).when(redisCacheWriter).put(anyString(), any(byte[].class), any(byte[].class), any(Duration.class));

        CacheManager redisCacheManager = RedisCacheManager.builder(redisCacheWriter)
                .cacheDefaults(RedisCacheConfiguration.defaultCacheConfig())
                .build();
        assertStoresSeparateEntries(redisCacheManager.getCache("validateCodeCache"), aaKey, bbKey);
        assertEquals(2, redisEntries.size(), "Redis must receive distinct serialized keys");
    }

    @Test
    void validateCodeCacheKey_distinguishesNullEmptyAndLiteralNullForNullableInputs() {
        ValidationCacheService cacheService = new ValidationCacheService();
        assertDistinct(
            cacheService.validateCodeCacheKey(null, "code", DISPLAY, VALUE_SET_URL),
            cacheService.validateCodeCacheKey("", "code", DISPLAY, VALUE_SET_URL),
            cacheService.validateCodeCacheKey("null", "code", DISPLAY, VALUE_SET_URL));
        assertDistinct(
            cacheService.validateCodeCacheKey(CODE_SYSTEM, "code", null, VALUE_SET_URL),
            cacheService.validateCodeCacheKey(CODE_SYSTEM, "code", "", VALUE_SET_URL),
            cacheService.validateCodeCacheKey(CODE_SYSTEM, "code", "null", VALUE_SET_URL));
        assertDistinct(
            cacheService.validateCodeCacheKey(CODE_SYSTEM, "code", DISPLAY, null),
            cacheService.validateCodeCacheKey(CODE_SYSTEM, "code", DISPLAY, ""),
            cacheService.validateCodeCacheKey(CODE_SYSTEM, "code", DISPLAY, "null"));
    }

    @Test
    void cachedValidateCode_cachesNonNullNegativeResultsAndHitsForRepeatedTuples() {
        try (AnnotationConfigApplicationContext context = new AnnotationConfigApplicationContext(CaffeineCacheTestConfiguration.class)) {
            ValidationCacheService cacheService = context.getBean(ValidationCacheService.class);
            RemoteTermServiceValidation delegate = mock(RemoteTermServiceValidation.class);
            when(delegate.invokeRemoteValidateCode(anyString(), anyString(), anyString(), anyString(), isNull()))
                    .thenAnswer(invocation -> negativeResult(invocation.getArgument(1)));

            IValidationSupport.CodeValidationResult aaResult =
                    cacheService.cachedValidateCode(delegate, CODE_SYSTEM, "Aa", DISPLAY, VALUE_SET_URL);
            IValidationSupport.CodeValidationResult bbResult =
                    cacheService.cachedValidateCode(delegate, CODE_SYSTEM, "BB", DISPLAY, VALUE_SET_URL);
            IValidationSupport.CodeValidationResult repeatedAaResult =
                    cacheService.cachedValidateCode(delegate, CODE_SYSTEM, "Aa", DISPLAY, VALUE_SET_URL);

            assertEquals(IValidationSupport.IssueSeverity.ERROR, aaResult.getSeverity());
            assertEquals(IValidationSupport.IssueSeverity.ERROR, bbResult.getSeverity());
            assertNotEquals(aaResult.getCode(), bbResult.getCode());
            assertSame(aaResult, repeatedAaResult, "the same tuple must be returned from the cache");
            verify(delegate, times(2)).invokeRemoteValidateCode(
                    anyString(), anyString(), anyString(), anyString(), isNull());
        }
    }

    @Test
    void cachedValidateCode_doesNotCacheNullResults() {
        try (AnnotationConfigApplicationContext context = new AnnotationConfigApplicationContext(CaffeineCacheTestConfiguration.class)) {
            ValidationCacheService cacheService = context.getBean(ValidationCacheService.class);
            RemoteTermServiceValidation delegate = mock(RemoteTermServiceValidation.class);
            when(delegate.invokeRemoteValidateCode(anyString(), anyString(), anyString(), anyString(), isNull()))
                    .thenReturn(null);

            assertNull(cacheService.cachedValidateCode(delegate, CODE_SYSTEM, "code", DISPLAY, VALUE_SET_URL));
            assertNull(cacheService.cachedValidateCode(delegate, CODE_SYSTEM, "code", DISPLAY, VALUE_SET_URL));
            verify(delegate, times(2)).invokeRemoteValidateCode(
                    anyString(), anyString(), anyString(), anyString(), isNull());
        }
    }

    @Test
    void cachedValidateCode_coalescesConcurrentMissesIntoOneRemoteCall() throws Exception {
        try (AnnotationConfigApplicationContext context = new AnnotationConfigApplicationContext(CaffeineCacheTestConfiguration.class)) {
            ValidationCacheService cacheService = context.getBean(ValidationCacheService.class);
            RemoteTermServiceValidation delegate = mock(RemoteTermServiceValidation.class);
            AtomicInteger remoteCalls = new AtomicInteger();
            when(delegate.invokeRemoteValidateCode(anyString(), anyString(), anyString(), anyString(), isNull()))
                    .thenAnswer(invocation -> {
                        remoteCalls.incrementAndGet();
                        Thread.sleep(150);
                        return negativeResult(invocation.getArgument(1));
                    });

            int workers = 8;
            ExecutorService pool = Executors.newFixedThreadPool(workers);
            CountDownLatch start = new CountDownLatch(1);
            List<Future<IValidationSupport.CodeValidationResult>> futures = new ArrayList<>();
            for (int i = 0; i < workers; i++) {
                futures.add(pool.submit(() -> {
                    start.await();
                    return cacheService.cachedValidateCode(delegate, CODE_SYSTEM, "code", DISPLAY, VALUE_SET_URL);
                }));
            }
            start.countDown();
            for (Future<IValidationSupport.CodeValidationResult> future : futures) {
                assertEquals("code", future.get(5, TimeUnit.SECONDS).getCode());
            }
            pool.shutdownNow();

            assertEquals(1, remoteCalls.get(), "concurrent cache misses for one tuple must share a single terminology call");
            verify(delegate, times(1)).invokeRemoteValidateCode(
                    anyString(), anyString(), anyString(), anyString(), isNull());
        }
    }

    @Test
    void isValueSetSupportedAndFetchValueSet_shareOneRemoteGet() {
        try (AnnotationConfigApplicationContext context = new AnnotationConfigApplicationContext(CaffeineCacheTestConfiguration.class)) {
            ValidationCacheService cacheService = context.getBean(ValidationCacheService.class);
            RemoteTermServiceValidation delegate = mock(RemoteTermServiceValidation.class);
            ValueSet valueSet = new ValueSet();
            valueSet.setUrl(VALUE_SET_URL);
            when(delegate.invokeFetchValueSet(VALUE_SET_URL)).thenReturn(valueSet);

            assertTrue(cacheService.cachedIsValueSetSupported(delegate, VALUE_SET_URL));
            assertSame(valueSet, cacheService.cachedFetchValueSet(delegate, VALUE_SET_URL));
            verify(delegate, times(1)).invokeFetchValueSet(VALUE_SET_URL);
        }
    }

    @Test
    void missingValueSet_isNegativelyCached() {
        try (AnnotationConfigApplicationContext context = new AnnotationConfigApplicationContext(CaffeineCacheTestConfiguration.class)) {
            ValidationCacheService cacheService = context.getBean(ValidationCacheService.class);
            RemoteTermServiceValidation delegate = mock(RemoteTermServiceValidation.class);
            when(delegate.invokeFetchValueSet(VALUE_SET_URL)).thenReturn(null);

            assertNull(cacheService.cachedFetchValueSet(delegate, VALUE_SET_URL));
            assertNull(cacheService.cachedFetchValueSet(delegate, VALUE_SET_URL));
            verify(delegate, times(1)).invokeFetchValueSet(VALUE_SET_URL);
        }
    }

    @Test
    void cachedValidateCode_resolvesItsKeyWithoutApplicationClassInThreadContext() throws InterruptedException {
        try (AnnotationConfigApplicationContext context = new AnnotationConfigApplicationContext(CaffeineCacheTestConfiguration.class)) {
            ValidationCacheService cacheService = context.getBean(ValidationCacheService.class);
            RemoteTermServiceValidation delegate = mock(RemoteTermServiceValidation.class);
            when(delegate.invokeRemoteValidateCode(anyString(), anyString(), anyString(), anyString(), isNull()))
                    .thenReturn(negativeResult("code"));

            AtomicReference<Throwable> failure = new AtomicReference<>();
            Thread worker = new Thread(() -> {
                try {
                    cacheService.cachedValidateCode(delegate, CODE_SYSTEM, "code", DISPLAY, VALUE_SET_URL);
                } catch (Throwable exception) {
                    failure.set(exception);
                }
            }, "validation-cache-worker");
            worker.setContextClassLoader(ClassLoader.getPlatformClassLoader());
            worker.start();
            worker.join();

            assertNull(failure.get());
        }
    }

    private static void assertStoresSeparateEntries(Cache cache, String aaKey, String bbKey) {
        assertNotNull(cache);
        cache.put(aaKey, "Aa result");
        cache.put(bbKey, "BB result");
        assertEquals("Aa result", cache.get(aaKey, String.class));
        assertEquals("BB result", cache.get(bbKey, String.class));
    }

    private static void assertDistinct(String... keys) {
        assertEquals(keys.length, Set.of(keys).size());
    }

    private static IValidationSupport.CodeValidationResult negativeResult(String code) {
        IValidationSupport.CodeValidationResult result = new IValidationSupport.CodeValidationResult();
        result.setCode(code);
        result.setSeverity(IValidationSupport.IssueSeverity.ERROR);
        return result;
    }

    private static String serializeKey(byte[] key) {
        return Base64.getEncoder().encodeToString(key);
    }

    @Configuration(proxyBeanMethods = false)
    static class CaffeineCacheTestConfiguration {
        @Bean
        CacheManager cacheManager() {
            CaffeineCacheManager cacheManager = new CaffeineCacheManager(
                    "validateCodeCache",
                    "lookupCodeCache",
                    "isCodeSystemSupportedCache",
                    "isValueSetSupportedCache");
            cacheManager.setCaffeine(Caffeine.newBuilder());
            return cacheManager;
        }

        @Bean
        ValidationCacheService validationCacheService(CacheManager cacheManager) {
            return new ValidationCacheService(cacheManager, null, null);
        }
    }
}