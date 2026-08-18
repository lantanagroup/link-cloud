package com.lantanagroup.link.validation.providers;

import ca.uhn.fhir.context.support.IValidationSupport;
import com.lantanagroup.link.validation.services.ValidationMetrics;
import org.hl7.fhir.instance.model.api.IBaseResource;
import org.springframework.cache.Cache;
import org.springframework.cache.CacheManager;
import org.springframework.cache.annotation.Cacheable;
import org.springframework.stereotype.Service;

@Service
public class ValidationCacheService {
    static final String CACHE_NAME = "validateCodeCache";

    private final CacheManager cacheManager;
    private final ValidationMetrics validationMetrics;

    public ValidationCacheService(CacheManager cacheManager, ValidationMetrics validationMetrics) {
        this.cacheManager = cacheManager;
        this.validationMetrics = validationMetrics;
    }

    /**
     * Cache wrapper for {@link RemoteTermServiceValidation#invokeRemoteValidateCode}.
     * Explicit cache lookup (rather than {@code @Cacheable}) so we can record hit / miss
     * counters via {@link ValidationMetrics}; the previous annotation-based path hid the
     * lookup inside Spring's cache proxy and gave us no way to observe it.
     *
     * <p>Uses {@link #validateCodeCacheKey} for a collision-free string key (aligned with the
     * other {@code @Cacheable} methods below), rather than {@code Objects.hash} which is
     * int-space and prone to collisions.
     *
     * NOTE: This assumes {@code RemoteTermServiceValidation} is stateless for this call
     * (safe to inject or pass).
     */
    public IValidationSupport.CodeValidationResult cachedValidateCode(
            RemoteTermServiceValidation delegate,
            String codeSystem,
            String code,
            String display,
            String valueSetUrl
    ) {
        Cache cache = cacheManager.getCache(CACHE_NAME);
        String key = validateCodeCacheKey(codeSystem, code, display, valueSetUrl);

        if (cache != null) {
            Cache.ValueWrapper wrapper = cache.get(key);
            if (wrapper != null) {
                validationMetrics.incrementValidateCodeCacheHit();
                return (IValidationSupport.CodeValidationResult) wrapper.get();
            }
        }

        validationMetrics.incrementValidateCodeCacheMiss();
        IValidationSupport.CodeValidationResult result =
                delegate.invokeRemoteValidateCode(codeSystem, code, display, valueSetUrl, (IBaseResource) null);
        if (cache != null && result != null) {
            cache.put(key, result);
        }
        return result;
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
     * Cache wrapper for {@link RemoteTermServiceValidation#invokeIsCodeSystemSupported(String)}.
     * HAPI's ValidationSupportChain probes {@code isCodeSystemSupported} per system per traversal,
     * so an uncached implementation hits the remote TS on every coded element. Both {@code true}
     * and {@code false} results are cached (unknown systems shouldn't be reprobed either).
     */
    @Cacheable(value = "isCodeSystemSupportedCache", key = "#codeSystem")
    public boolean cachedIsCodeSystemSupported(RemoteTermServiceValidation delegate, String codeSystem) {
        return delegate.invokeIsCodeSystemSupported(codeSystem);
    }

    /**
     * Cache wrapper for {@link RemoteTermServiceValidation#invokeIsValueSetSupported(String)}.
     * See {@link #cachedIsCodeSystemSupported} for rationale — identical caching model applies.
     */
    @Cacheable(value = "isValueSetSupportedCache", key = "#valueSetUrl")
    public boolean cachedIsValueSetSupported(RemoteTermServiceValidation delegate, String valueSetUrl) {
        return delegate.invokeIsValueSetSupported(valueSetUrl);
    }

    /**
     * Cache wrapper for {@link RemoteTermServiceValidation#invokeLookupCode(String, String, String, String)}.
     * Lookup results are stable for a given code/system/language/properties combination and are expensive
     * network calls, so caching prevents redundant remote round-trips during validation.
     */
    @Cacheable(
            value = "lookupCodeCache",
            key = "#root.target.validateCodeCacheKey(#code, #system, #displayLanguage, #propertyNames)",
            unless = "#result == null"
    )
    public IValidationSupport.LookupCodeResult cachedLookupCode(
            RemoteTermServiceValidation delegate,
            String code,
            String system,
            String displayLanguage,
            String propertyNames
    ) {
        return delegate.invokeLookupCode(code, system, displayLanguage, propertyNames);
    }
}
