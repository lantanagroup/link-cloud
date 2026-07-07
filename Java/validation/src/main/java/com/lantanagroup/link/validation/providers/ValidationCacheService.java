package com.lantanagroup.link.validation.providers;

import ca.uhn.fhir.context.support.IValidationSupport;
import com.lantanagroup.link.validation.services.ValidationMetrics;
import org.hl7.fhir.instance.model.api.IBaseResource;
import org.springframework.cache.Cache;
import org.springframework.cache.CacheManager;
import org.springframework.stereotype.Service;

import java.util.Objects;

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
        int key = Objects.hash(codeSystem, code, display, valueSetUrl);

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
}
