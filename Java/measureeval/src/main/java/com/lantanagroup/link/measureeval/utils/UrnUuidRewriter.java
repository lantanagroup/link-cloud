package com.lantanagroup.link.measureeval.utils;

import ca.uhn.fhir.context.FhirContext;
import org.hl7.fhir.instance.model.api.IBase;
import org.hl7.fhir.instance.model.api.IBaseReference;
import org.hl7.fhir.r4.model.Bundle;
import org.hl7.fhir.r4.model.Resource;

import java.util.HashMap;
import java.util.Map;

/**
 * A HAPI FHIR utility class (for FHIR R4) that:
 * 1. Builds a map from "urn:uuid:XYZ" -> "ResourceType/XYZ".
 * 2. Rewrites:
 *    - Bundle.entry.fullUrl from "urn:uuid:XYZ" to "ResourceType/XYZ"
 *    - Resource IDs from "urn:uuid:XYZ" to "ResourceType/XYZ", or from "XYZ" to "ResourceType-XYZ"
 *    - Any references (e.g. subject.reference, encounter.reference) that start with "urn:uuid:"
 *      to "ResourceType/XYZ".
 * Usage Example:
 *   FhirContext ctx = FhirContext.forR4();
 *   IParser parser = ctx.newJsonParser();
 *   // 1) Parse a Bundle from JSON
 *   Bundle bundle = (Bundle) parser.parseResource(new FileReader("SyntheaBundle.json"));
 *   // 2) Rewrite
 *   UrnUuidRewriter.rewriteUrnUuids(bundle, ctx);
 *   // 3) Output or further process
 *   String updatedJson = parser.setPrettyPrint(true).encodeResourceToString(bundle);
 *   System.out.println(updatedJson);
 */
public class UrnUuidRewriter {

    private static final String URN_UUID = "urn:uuid:";

    /**
     * Rewrite all "urn:uuid:XYZ" references in the given Bundle:
     *  - Updates Bundle.entry.fullUrl
     *  - Updates resource.id
     *  - Updates all internal references
     *
     * @param bundle The Bundle to be modified in place
     * @param ctx    FhirContext for R4
     * @return The same Bundle, after rewriting
     */
    public static Bundle rewriteUrnUuids(Bundle bundle, FhirContext ctx) {
        var uuidMap = buildUuidMap(bundle);

        // Rewrite fullUrl in each entry
        for (var entry : bundle.getEntry()) {
            var fullUrl = entry.getFullUrl();
            if (uuidMap.containsKey(fullUrl)) {
                entry.setFullUrl(uuidMap.get(fullUrl));
            }
        }

        // Rewrite each resource's ID and references
        for (var entry : bundle.getEntry()) {
            var resource = entry.getResource();
            if (resource != null) {
                rewriteResourceId(resource, uuidMap);
                rewriteResourceReferences(resource, uuidMap, ctx);
            }
        }

        return bundle;
    }

    /**
     * Build a map from "urn:uuid:XYZ" -> "ResourceType/XYZ".
     */
    private static Map<String, String> buildUuidMap(Bundle bundle) {
        var uuidMap = new HashMap<String, String>();
        for (var entry : bundle.getEntry()) {
            var resource = entry.getResource();
            if (resource == null) continue;

            var resourceType = resource.fhirType();
            var resourceId = resource.getId();
            var fullUrl = entry.getFullUrl();

            // If fullUrl is "urn:uuid:XYZ", map -> "ResourceType/XYZ"
            if (fullUrl != null && fullUrl.startsWith(URN_UUID)) {
                var tail = fullUrl.substring(URN_UUID.length());
                var newRef = resourceType + "/" + tail;
                uuidMap.put(fullUrl, newRef);
            }

            // If resource ID is "urn:uuid:XYZ", also map
            if (resourceId != null && resourceId.startsWith(URN_UUID)) {
                var tail = resourceId.substring(URN_UUID.length());
                var newRef = resourceType + "/" + tail;
                uuidMap.put(resourceId, newRef);
            }
        }
        return uuidMap;
    }

    /**
     * Rewrite the resource's own ID from:
     *   - "urn:uuid:XYZ" -> "ResourceType/XYZ", or
     *   - "XYZ" -> "ResourceType-XYZ"
     */
    private static void rewriteResourceId(Resource resource, Map<String, String> uuidMap) {
        var resourceType = resource.fhirType();
        var originalIdValue = resource.getIdElement().getValue(); // e.g. "urn:uuid:XYZ" or "XYZ"

        if (originalIdValue == null) return;

        if (uuidMap.containsKey(originalIdValue)) {
            // e.g. "urn:uuid:XYZ"
            resource.setId(uuidMap.get(originalIdValue));
        } else {
            // If it's just "XYZ", rename it "ResourceType-XYZ" or "ResourceType/XYZ"
            var idType = resource.getIdElement(); // e.g. "Patient/123" or "123"
            var shortId = idType.getIdPart();     // e.g. "123"
            if (!shortId.startsWith(resourceType)) {
                // e.g. "Patient-123"
                resource.setId(resourceType + "-" + shortId);
            }
        }
    }

    /**
     * Use HAPI FHIR Terser to find all reference elements (subject, encounter, etc.)
     * and rewrite urn:uuid references.
     */
    private static void rewriteResourceReferences(Resource resource, Map<String, String> uuidMap, FhirContext ctx) {
        var terser = ctx.newTerser();
        // Get all references in the resource
        var allRefs = terser.getAllPopulatedChildElementsOfType(resource, IBase.class);

        for (var ref : allRefs) {
            if (ref instanceof IBaseReference iRef) {
                var oldRefVal = iRef.getReferenceElement().getValue();
                if (oldRefVal != null && oldRefVal.startsWith(URN_UUID) && uuidMap.containsKey(oldRefVal)) {
                    (iRef).setReference(uuidMap.get(oldRefVal));
                }
            }
        }
    }
}

