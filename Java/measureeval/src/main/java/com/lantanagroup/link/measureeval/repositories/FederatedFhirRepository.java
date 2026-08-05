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
 * IRepository that federates the in-memory bundle store with a remote FHIR terminology
 * service using <em>bundle-first, fall-through-on-empty</em> semantics.
 *
 * <p>Behavior:
 * <ul>
 *   <li>For {@link ValueSet} and {@link CodeSystem} <b>reads</b>, the in-memory store is
 *       consulted first. If it returns null (or throws because the resource isn't
 *       present), the request falls through to the remote client. First non-null wins.</li>
 *   <li>For {@link ValueSet} and {@link CodeSystem} <b>searches</b>, the in-memory store
 *       is consulted first. If it returns a non-empty bundle, that bundle is returned
 *       and the remote is never called. Only if the local search is empty does the
 *       request fall through to the remote.</li>
 *   <li>All other resource types (Patient, Observation, Measure, Library, ...) delegate
 *       straight to the in-memory store. Patient data is never fetched from a remote
 *       terminology server. Remote failures on federated calls degrade gracefully: they
 *       are caught and the local result is returned instead.</li>
 * </ul>
 *
 * <p>Internally the remote calls go through CQF's {@link RestRepository}, which handles
 * the HAPI client bridging (search-parameter passthrough, response parsing). We take
 * ownership of only the federation semantics.
 *
 * <h4>Why not CQF's {@code FederatedRepository}?</h4>
 *
 * <p>An earlier iteration of this composition used CQF's built-in
 * {@code Repositories.proxy(bundleRepo, bundleRepo, FederatedRepository(bundleRepo, RestRepository(client)))}.
 * Reading the cqf-fhir 4.5.1 bytecode reveals that
 * {@code FederatedRepository.search(...)} does <em>not</em> implement fall-through
 * semantics: it submits a {@link java.util.concurrent.CompletableFuture} per
 * constituent repository, joins all of them, and merges the returned entries into a
 * single searchset bundle. That means the remote TS is always called even when the
 * bundle has the ValueSet, and any remote failure throws a {@code CompletionException}
 * that propagates through the joined future — CQL evaluation crashes on a transient
 * remote error rather than degrading to bundle-only.
 *
 * <p>For measure evaluation specifically, bundle-first semantics matter for
 * reproducibility: measure bundles embed the specific ValueSet expansions the measure
 * author validated with, and a remote TS with a fresher (or slightly different)
 * expansion could silently change measure results. This class preserves those
 * bundle-embedded expansions as authoritative.
 *
 * <p>{@code FederatedRepository.read(...)} does implement first-non-null-wins
 * semantics; we replicate that here for consistency across read and search.
 *
 * <p>Constructed by {@link com.lantanagroup.link.measureeval.services.MeasureEvaluator}
 * only when a remote terminology client is available; otherwise the plain
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
        T local = tryLocalRead(resourceType, id, headers);
        if (local != null || !isTerminologyType(resourceType)) {
            return local;
        }
        logger.debug("Federating read: {}/{} — not in bundle, fetching from remote TS", resourceType.getSimpleName(), id.getIdPart());
        try {
            return remoteRepo.read(resourceType, id, headers);
        } catch (Exception ex) {
            logger.warn("Remote read of {}/{} failed: {}", resourceType.getSimpleName(), id.getIdPart(), ex.getMessage());
            return null;
        }
    }

    private <T extends IBaseResource, I extends IIdType> T tryLocalRead(Class<T> resourceType, I id, Map<String, String> headers) {
        try {
            return super.read(resourceType, id, headers);
        } catch (Exception ex) {
            // InMemoryFhirRepository throws when the resource isn't present. Treat as null so we can fall through.
            return null;
        }
    }

    @Override
    public <B extends IBaseBundle, T extends IBaseResource> B search(
            Class<B> bundleType,
            Class<T> resourceType,
            Multimap<String, List<ca.uhn.fhir.model.api.IQueryParameterType>> searchParameters,
            Map<String, String> headers) {
        B local = super.search(bundleType, resourceType, searchParameters, headers);
        if (!isTerminologyType(resourceType) || !isEmpty(local)) {
            return local;
        }
        logger.debug("Federating search: {} — empty locally, querying remote TS", resourceType.getSimpleName());
        try {
            return remoteRepo.search(bundleType, resourceType, searchParameters, headers);
        } catch (Exception ex) {
            logger.warn("Remote search of {} failed: {} — falling back to (empty) local result", resourceType.getSimpleName(), ex.getMessage());
            return local;
        }
    }

    private boolean isTerminologyType(Class<?> resourceType) {
        return ValueSet.class.isAssignableFrom(resourceType) || CodeSystem.class.isAssignableFrom(resourceType);
    }

    private boolean isEmpty(IBaseBundle bundle) {
        if (bundle == null) return true;
        if (bundle instanceof org.hl7.fhir.r4.model.Bundle b) {
            return !b.hasEntry() || b.getEntry().isEmpty();
        }
        return false;
    }
}
