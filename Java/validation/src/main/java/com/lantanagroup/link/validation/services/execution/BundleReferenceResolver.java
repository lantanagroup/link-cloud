package com.lantanagroup.link.validation.services.execution;

import ca.uhn.fhir.fhirpath.IFhirPathEvaluationContext;
import org.hl7.fhir.instance.model.api.IBase;
import org.hl7.fhir.instance.model.api.IBaseResource;
import org.hl7.fhir.instance.model.api.IIdType;
import org.springframework.stereotype.Component;

import java.util.Collections;
import java.util.HashMap;
import java.util.List;
import java.util.Map;

/**
 * Lets FHIRPath {@code resolve()} follow references to sibling resources in the bundle currently
 * under evaluation.
 *
 * <p>Rubric checks such as {@code ServiceRequest.subject.all(resolve().is(Patient))} rely on
 * {@code resolve()} returning the referenced resource. HAPI's FHIRPath engine evaluates one resource
 * at a time and has no view of the surrounding bundle, so without a resolver {@code resolve()} always
 * returns empty and every reference-target check fails — even when the reference is perfectly valid.
 *
 * <p>The engine is thread-confined ({@link ThreadLocalFhirPath}) and its evaluation context can only
 * be wired once, at startup, so a single instance of this resolver is registered globally. The bundle
 * itself changes per request, so the "current bundle" is held in a {@link ThreadLocal} that
 * {@link com.lantanagroup.link.validation.services.RubricExecutionService} binds around each check
 * (on the same thread that then evaluates FHIRPath) and clears afterwards. That keeps concurrent
 * evaluations — sequential on the request thread, or fanned out across the check pool — isolated from
 * one another and prevents a pooled thread from ever resolving against a stale bundle.
 */
@Component
public class BundleReferenceResolver implements IFhirPathEvaluationContext {

    private final ThreadLocal<Map<String, IBaseResource>> currentBundle = new ThreadLocal<>();

    /**
     * Index a bundle's resources by the reference strings that can point at them — the canonical
     * {@code ResourceType/id} form and the bare {@code id}. First occurrence wins if ids collide.
     */
    public static Map<String, IBaseResource> buildIndex(List<IBaseResource> resources) {
        if (resources == null || resources.isEmpty()) {
            return Collections.emptyMap();
        }
        Map<String, IBaseResource> index = new HashMap<>(resources.size() * 2);
        for (IBaseResource resource : resources) {
            if (resource == null) {
                continue;
            }
            IIdType id = resource.getIdElement();
            if (id != null && id.hasIdPart()) {
                index.putIfAbsent(resource.fhirType() + "/" + id.getIdPart(), resource);
                index.putIfAbsent(id.getIdPart(), resource);
            }
        }
        return index;
    }

    /** Bind the reference index of the bundle this thread is about to evaluate. */
    public void bind(Map<String, IBaseResource> referenceIndex) {
        currentBundle.set(referenceIndex != null ? referenceIndex : Collections.emptyMap());
    }

    /** Release the binding once evaluation completes so a pooled thread never sees a stale bundle. */
    public void clear() {
        currentBundle.remove();
    }

    @Override
    public IBase resolveReference(IIdType theReference, IBase theContext) {
        if (theReference == null) {
            return null;
        }
        Map<String, IBaseResource> index = currentBundle.get();
        if (index == null || index.isEmpty()) {
            return null;
        }
        String ref = theReference.getValue();
        if (ref != null) {
            IBaseResource hit = index.get(ref);
            if (hit != null) {
                return hit;
            }
        }
        // Fall back to the versionless "Type/id" form for absolute or versioned references.
        IIdType versionless = theReference.toUnqualifiedVersionless();
        String key = versionless != null ? versionless.getValue() : null;
        return key != null ? index.get(key) : null;
    }
}
