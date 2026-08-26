package com.lantanagroup.link.validation.providers;

import ca.uhn.fhir.context.support.IValidationSupport;
import com.lantanagroup.link.validation.services.ValidationMetrics;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.Test;
import org.springframework.cache.Cache;
import org.springframework.cache.CacheManager;
import org.springframework.cache.caffeine.CaffeineCacheManager;

import static org.junit.jupiter.api.Assertions.assertNull;
import static org.junit.jupiter.api.Assertions.assertSame;
import static org.mockito.ArgumentMatchers.any;
import static org.mockito.ArgumentMatchers.anyString;
import static org.mockito.Mockito.mock;
import static org.mockito.Mockito.never;
import static org.mockito.Mockito.times;
import static org.mockito.Mockito.verify;
import static org.mockito.Mockito.verifyNoMoreInteractions;
import static org.mockito.Mockito.when;

class ValidationCacheServiceTest {
    private CacheManager cacheManager;
    private ValidationMetrics metrics;
    private RemoteTermServiceValidation delegate;
    private ValidationCacheService service;

    @BeforeEach
    void setUp() {
        cacheManager = new CaffeineCacheManager(ValidationCacheService.CACHE_NAME);
        metrics = mock(ValidationMetrics.class);
        delegate = mock(RemoteTermServiceValidation.class);
        service = new ValidationCacheService(cacheManager, metrics);
    }

    @Test
    void missPopulatesCacheAndIncrementsMiss() {
        IValidationSupport.CodeValidationResult expected = new IValidationSupport.CodeValidationResult();
        when(delegate.invokeRemoteValidateCode(anyString(), anyString(), anyString(), anyString(), any()))
                .thenReturn(expected);

        IValidationSupport.CodeValidationResult actual =
                service.cachedValidateCode(delegate, "sys", "code", "display", "vs");

        assertSame(expected, actual);
        verify(metrics, times(1)).incrementValidateCodeCacheMiss();
        verify(metrics, never()).incrementValidateCodeCacheHit();
        verify(delegate, times(1)).invokeRemoteValidateCode(anyString(), anyString(), anyString(), anyString(), any());

        Cache cache = cacheManager.getCache(ValidationCacheService.CACHE_NAME);
        assertSame(expected, cache.get(java.util.Objects.hash("sys", "code", "display", "vs")).get());
    }

    @Test
    void hitReturnsCachedResultWithoutCallingDelegate() {
        IValidationSupport.CodeValidationResult expected = new IValidationSupport.CodeValidationResult();
        cacheManager.getCache(ValidationCacheService.CACHE_NAME)
                .put(java.util.Objects.hash("sys", "code", "display", "vs"), expected);

        IValidationSupport.CodeValidationResult actual =
                service.cachedValidateCode(delegate, "sys", "code", "display", "vs");

        assertSame(expected, actual);
        verify(metrics, times(1)).incrementValidateCodeCacheHit();
        verify(metrics, never()).incrementValidateCodeCacheMiss();
        verifyNoMoreInteractions(delegate);
    }

    @Test
    void nullResultDoesNotPoisonCache() {
        when(delegate.invokeRemoteValidateCode(anyString(), anyString(), anyString(), anyString(), any()))
                .thenReturn(null);

        IValidationSupport.CodeValidationResult first =
                service.cachedValidateCode(delegate, "sys", "code", "display", "vs");
        IValidationSupport.CodeValidationResult second =
                service.cachedValidateCode(delegate, "sys", "code", "display", "vs");

        assertNull(first);
        assertNull(second);
        verify(metrics, times(2)).incrementValidateCodeCacheMiss();
        verify(metrics, never()).incrementValidateCodeCacheHit();
        verify(delegate, times(2)).invokeRemoteValidateCode(anyString(), anyString(), anyString(), anyString(), any());
    }
}
