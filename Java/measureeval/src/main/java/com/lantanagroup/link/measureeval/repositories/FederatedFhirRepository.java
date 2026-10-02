package com.lantanagroup.link.measureeval.repositories;

import ca.uhn.fhir.context.FhirContext;
import ca.uhn.fhir.repository.IRepository;
import ca.uhn.fhir.rest.client.api.IGenericClient;
import com.google.common.collect.Multimap;
import org.hl7.fhir.instance.model.api.IBaseBundle;
import org.hl7.fhir.instance.model.api.IBaseResource;
import org.hl7.fhir.instance.model.api.IIdType;
import org.hl7.fhir.r4.model.CodeSystem;
import org.hl7.fhir.r4.model.ValueSet;
import org.opencds.cqf.fhir.utility.repository.RestRepository;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;

import java.util.List;
import java.util.Map;

/**
 * IRepository that routes {@link ValueSet} and {@link CodeSystem} lookups to a remote FHIR
 * terminology service and every other resource type to the in-memory bundle store.
 *
 * <p><strong>TS-authoritative semantics.</strong> When this repository is constructed, a remote
 * terminology client is present by definition (see {@link com.lantanagroup.link.measureeval.services.MeasureEvaluator#buildRepository()}).
 * In that mode the remote is the single source of truth for terminology:
 * <ul>
 *   <li>Terminology {@code read} / {@code search} calls go straight to the remote client.
 *       The in-memory bundle is not consulted for {@link ValueSet} or {@link CodeSystem} even
 *       when it carries an embedded copy.</li>
 *   <li>A remote miss (null from {@code read}, empty from {@code search}) is returned as-is.
 *       Downstream CQL evaluation then sees an unresolved binding and fails naturally, the
 *       same way it would if no ValueSet were bound at all.</li>
 *   <li>A remote error (network failure, non-2xx, timeout) propagates. There is no local
 *       fallback — "only look in the TS" means we don't silently downgrade to bundle
 *       expansions when the TS is unreachable.</li>
 *   <li>All other resource types (Patient, Observation, Measure, Library, ...) are served
 *       from the in-memory bundle. Patient data is never fetched from a remote terminology
 *       server.</li>
 * </ul>
 *
 * <p>This shape was chosen deliberately over the earlier bundle-first design so operators can
 * point a deployment at a single authoritative terminology service and know that measure
 * evaluations reflect that server's current expansions rather than whatever was baked into
 * the measure bundle at authoring time.
 *
 * <p>Internally the remote calls go through CQF's {@link RestRepository}, which handles the
 * HAPI client bridging (search-parameter passthrough, response parsing). We take ownership
 * of only the routing.
 *
 * <h4>Why not CQF's {@code FederatedRepository}?</h4>
 *
 * <p>{@code FederatedRepository.search(...)} submits a {@link java.util.concurrent.CompletableFuture}
 * per constituent repository, joins all of them, and merges the returned entries into a single
 * searchset bundle. That would merge remote and local ValueSet copies into one result, which
 * violates the "TS is authoritative" invariant when the two diverge. Owning the routing here
 * keeps the semantic tight and skips the merge cost when the bundle would only be a source of
 * noise for terminology types.
 *
 * <p>Constructed by {@link com.lantanagroup.link.measureeval.services.MeasureEvaluator} only
 * when a remote terminology client is available; otherwise the plain
 * {@link LinkInMemoryFhirRepository} is used and this class stays out of the picture.
 */
public class FederatedFhirRepository extends LinkInMemoryFhirRepository {
    private static final Logger logger = LoggerFactory.getLogger(FederatedFhirRepository.class);

    private final IRepository remoteRepo;

    public FederatedFhirRepository(FhirContext context, IBaseBundle bundle, IGenericClient remoteClient) {
        super(context, bundle);
        this.remoteRepo = new RestRepository(remoteClient);
    }

    @Override
    public <T extends IBaseResource, I extends IIdType> T read(Class<T> resourceType, I id, Map<String, String> headers) {
        if (!isTerminologyType(resourceType)) {
            return super.read(resourceType, id, headers);
        }
        logger.debug("Remote-only read: {}/{}", resourceType.getSimpleName(), id.getIdPart());
        return remoteRepo.read(resourceType, id, headers);
    }

    @Override
    public <B extends IBaseBundle, T extends IBaseResource> B search(
            Class<B> bundleType,
            Class<T> resourceType,
            Multimap<String, List<ca.uhn.fhir.model.api.IQueryParameterType>> searchParameters,
            Map<String, String> headers) {
        if (!isTerminologyType(resourceType)) {
            return super.search(bundleType, resourceType, searchParameters, headers);
        }
        logger.debug("Remote-only search: {}", resourceType.getSimpleName());
        return remoteRepo.search(bundleType, resourceType, searchParameters, headers);
    }

    private boolean isTerminologyType(Class<?> resourceType) {
        return ValueSet.class.isAssignableFrom(resourceType) || CodeSystem.class.isAssignableFrom(resourceType);
    }
}
