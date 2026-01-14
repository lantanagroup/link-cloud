package com.lantanagroup.link.validation.providers;

import ca.uhn.fhir.context.support.IValidationSupport;
import org.hl7.fhir.instance.model.api.IBaseResource;
import org.springframework.cache.annotation.Cacheable;
import org.springframework.stereotype.Service;

@Service
public class ValidationCacheService {
    /**
     * Cache wrapper for RemoteTermServiceValidation.invokeRemoteValidateCode().
     *
     * NOTE: This assumes `RemoteTermServiceValidation` is stateless for this call (safe to inject or pass).
     */
    @Cacheable(
            value = "validateCodeCache",
            key = "T(java.util.Objects).hash(#codeSystem, #code, #display, #valueSetUrl)",
            unless = "#result == null"
    )
    public IValidationSupport.CodeValidationResult cachedValidateCode(
            RemoteTermServiceValidation delegate,
            String codeSystem,
            String code,
            String display,
            String valueSetUrl
    ) {
        return delegate.invokeRemoteValidateCode(codeSystem, code, display, valueSetUrl, (IBaseResource) null);
    }
}
