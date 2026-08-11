package com.lantanagroup.link.validation.providers;

import ca.uhn.fhir.context.support.IValidationSupport;
import org.hl7.fhir.instance.model.api.IBaseResource;
import org.springframework.cache.annotation.Cacheable;
import org.springframework.stereotype.Service;


@Service
public class ValidationCacheService {
    /**
     * Cache wrapper for RemoteTermServiceValidation.invokeRemoteValidateCode().
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
            key = "T(java.util.Objects).hash(#code, #system, #displayLanguage, #propertyNames)",
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
