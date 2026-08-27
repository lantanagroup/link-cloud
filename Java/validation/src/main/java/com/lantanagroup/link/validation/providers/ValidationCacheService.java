package com.lantanagroup.link.validation.providers;

import ca.uhn.fhir.context.support.IValidationSupport;
import com.github.benmanes.caffeine.cache.Cache;
import com.github.benmanes.caffeine.cache.Caffeine;
import com.lantanagroup.link.validation.configs.CacheConfig;
import org.hl7.fhir.instance.model.api.IBaseResource;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.cache.CacheManager;
import org.springframework.cache.interceptor.CacheErrorHandler;
import org.springframework.stereotype.Service;

import java.time.Duration;
import java.util.concurrent.CompletableFuture;
import java.util.concurrent.CompletionException;
import java.util.concurrent.ConcurrentHashMap;
import java.util.function.Supplier;

/**
 * Single-flight + two-level cache in front of every remote terminology HTTP call.
 * <p>
 * Concurrent bundle/chunk validation asks HAPI for the same codes and ValueSets on every
 * worker at once. Spring {@code @Cacheable} (and Redis {@code Cache.get(key, loader)})
 * does not coalesce those misses, and HAPI's chain cache is {@code getIfPresent}/{@code put}.
 * Without a local flight map, four workers become four {@code $validate-code} / {@code GET ValueSet}
 * calls for the same tuple.
 * <p>
 * FHIR {@code ValueSet}/{@code CodeSystem} bodies stay in an in-process Caffeine cache: they are
 * not Redis-safe under the existing Jackson config. Existence booleans and validate/lookup
 * results still go through {@link CacheManager} (Redis/memory/none). Null validate/lookup
 * results are not cached, matching the previous {@code unless = "#result == null"} behaviour.
 */
@Service
public class ValidationCacheService {
    public static final String VALIDATE_CODE_CACHE = "validateCodeCache";
    public static final String LOOKUP_CODE_CACHE = "lookupCodeCache";
    public static final String IS_CODE_SYSTEM_SUPPORTED_CACHE = "isCodeSystemSupportedCache";
    public static final String IS_VALUE_SET_SUPPORTED_CACHE = "isValueSetSupportedCache";

    private static final CachedResource MISSING = new CachedResource(null);

    private final CacheManager cacheManager;
    private final CacheErrorHandler cacheErrorHandler;
    private final Cache<String, CachedResource> resourceCache;
    private final ConcurrentHashMap<String, CompletableFuture<?>> inflight = new ConcurrentHashMap<>();

    public ValidationCacheService() {
        this(null, null, null);
    }

    @Autowired
    public ValidationCacheService(
            CacheManager cacheManager,
            CacheErrorHandler cacheErrorHandler,
            CacheConfig cacheConfig) {
        this.cacheManager = cacheManager;
        this.cacheErrorHandler = cacheErrorHandler;
        long ttlSeconds = cacheConfig != null && cacheConfig.getValidateCode() != null
                ? cacheConfig.getValidateCode().getTtl()
                : 3600;
        this.resourceCache = Caffeine.newBuilder()
                .expireAfterWrite(Duration.ofSeconds(Math.max(1, ttlSeconds)))
                .maximumSize(5000)
                .build();
    }

    public IValidationSupport.CodeValidationResult cachedValidateCode(
            RemoteTermServiceValidation delegate,
            String codeSystem,
            String code,
            String display,
            String valueSetUrl
    ) {
        String key = validateCodeCacheKey(codeSystem, code, display, valueSetUrl);
        return computeOnce("validateCode:" + key, () -> {
            IValidationSupport.CodeValidationResult cached =
                    getFromCache(VALIDATE_CODE_CACHE, key, IValidationSupport.CodeValidationResult.class);
            if (cached != null) {
                return cached;
            }
            IValidationSupport.CodeValidationResult result =
                    delegate.invokeRemoteValidateCode(codeSystem, code, display, valueSetUrl, (IBaseResource) null);
            putInCache(VALIDATE_CODE_CACHE, key, result);
            return result;
        });
    }

    public String validateCodeCacheKey(
            String codeSystem,
            String code,
            String display,
            String valueSetUrl
    ) {
        return encodeKeyComponent(codeSystem)
                + encodeKeyComponent(code)
                + encodeKeyComponent(display)
                + encodeKeyComponent(valueSetUrl);
    }

    private static String encodeKeyComponent(String value) {
        return value == null ? "N;" : "V" + value.length() + ":" + value;
    }

    /**
     * Existence check shares {@link #cachedFetchCodeSystem}: HAPI probes {@code isCodeSystemSupported}
     * and later {@code fetchCodeSystem} for the same URL; one GET satisfies both.
     */
    public boolean cachedIsCodeSystemSupported(RemoteTermServiceValidation delegate, String codeSystem) {
        return cachedFetchCodeSystem(delegate, codeSystem) != null;
    }

    /**
     * Existence check shares {@link #cachedFetchValueSet}: HAPI probes {@code isValueSetSupported}
     * and later {@code fetchValueSet} for the same URL; one GET satisfies both.
     */
    public boolean cachedIsValueSetSupported(RemoteTermServiceValidation delegate, String valueSetUrl) {
        return cachedFetchValueSet(delegate, valueSetUrl) != null;
    }

    public IBaseResource cachedFetchValueSet(RemoteTermServiceValidation delegate, String valueSetUrl) {
        return cachedFetchResource("ValueSet", valueSetUrl, IS_VALUE_SET_SUPPORTED_CACHE,
                () -> delegate.invokeFetchValueSet(valueSetUrl));
    }

    public IBaseResource cachedFetchCodeSystem(RemoteTermServiceValidation delegate, String codeSystem) {
        return cachedFetchResource("CodeSystem", codeSystem, IS_CODE_SYSTEM_SUPPORTED_CACHE,
                () -> delegate.invokeFetchCodeSystem(codeSystem));
    }

    public IValidationSupport.LookupCodeResult cachedLookupCode(
            RemoteTermServiceValidation delegate,
            String code,
            String system,
            String displayLanguage,
            String propertyNames
    ) {
        String key = validateCodeCacheKey(code, system, displayLanguage, propertyNames);
        return computeOnce("lookupCode:" + key, () -> {
            IValidationSupport.LookupCodeResult cached =
                    getFromCache(LOOKUP_CODE_CACHE, key, IValidationSupport.LookupCodeResult.class);
            if (cached != null) {
                return cached;
            }
            IValidationSupport.LookupCodeResult result =
                    delegate.invokeLookupCode(code, system, displayLanguage, propertyNames);
            putInCache(LOOKUP_CODE_CACHE, key, result);
            return result;
        });
    }

    private IBaseResource cachedFetchResource(
            String type,
            String url,
            String existenceCacheName,
            Supplier<IBaseResource> loader) {
        return computeOnce("fetch:" + type + ":" + url, () -> {
            String resourceKey = type + ":" + url;
            CachedResource local = resourceCache.getIfPresent(resourceKey);
            if (local != null) {
                return local.resource();
            }

            Boolean known = getFromCache(existenceCacheName, url, Boolean.class);
            IBaseResource resource;
            if (Boolean.FALSE.equals(known)) {
                resource = null;
            } else {
                resource = loader.get();
                putInCache(existenceCacheName, url, resource != null);
            }
            resourceCache.put(resourceKey, resource == null ? MISSING : new CachedResource(resource));
            return resource;
        });
    }

    @SuppressWarnings("unchecked")
    private <T> T computeOnce(String key, Supplier<T> loader) {
        CompletableFuture<T> created = new CompletableFuture<>();
        CompletableFuture<T> existing = (CompletableFuture<T>) inflight.putIfAbsent(key, created);
        if (existing == null) {
            try {
                created.complete(loader.get());
            } catch (Throwable t) {
                created.completeExceptionally(t);
            } finally {
                inflight.remove(key, created);
            }
            return join(created);
        }
        return join(existing);
    }

    private static <T> T join(CompletableFuture<T> future) {
        try {
            return future.join();
        } catch (CompletionException e) {
            Throwable cause = e.getCause() != null ? e.getCause() : e;
            if (cause instanceof RuntimeException runtimeException) {
                throw runtimeException;
            }
            if (cause instanceof Error error) {
                throw error;
            }
            throw e;
        }
    }

    private <T> T getFromCache(String cacheName, String key, Class<T> type) {
        if (cacheManager == null) {
            return null;
        }
        org.springframework.cache.Cache named = cacheManager.getCache(cacheName);
        if (named == null) {
            return null;
        }
        try {
            org.springframework.cache.Cache.ValueWrapper wrapper = named.get(key);
            if (wrapper == null || wrapper.get() == null) {
                return null;
            }
            Object value = wrapper.get();
            return type.isInstance(value) ? type.cast(value) : null;
        } catch (RuntimeException ex) {
            if (cacheErrorHandler != null) {
                cacheErrorHandler.handleCacheGetError(ex, named, key);
            }
            return null;
        }
    }

    private void putInCache(String cacheName, String key, Object value) {
        if (value == null || cacheManager == null) {
            return;
        }
        org.springframework.cache.Cache named = cacheManager.getCache(cacheName);
        if (named == null) {
            return;
        }
        try {
            named.put(key, value);
        } catch (RuntimeException ex) {
            if (cacheErrorHandler != null) {
                cacheErrorHandler.handleCachePutError(ex, named, key, value);
            }
        }
    }

    private record CachedResource(IBaseResource resource) {
    }
}
