package com.lantanagroup.link.measureeval.repositories;

import ca.uhn.fhir.context.FhirContext;
import org.hl7.fhir.instance.model.api.IBaseBundle;
import org.hl7.fhir.r4.model.Bundle;
import org.opencds.cqf.fhir.utility.repository.InMemoryFhirRepository;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;

import java.util.Map;

/**
 * This class extends the InMemoryFhirRepository to provide a transaction method that will update the resources in the repository.
 * This implementation is primarily used to avoid the exception stack trace that is thrown when the InMemoryFhirRepository.transaction method is called by measure eval, but is not implemented
 * in the default InMemoryFhirRepository.
 */
public class LinkInMemoryFhirRepository extends InMemoryFhirRepository {
    private static final Logger logger = LoggerFactory.getLogger(LinkInMemoryFhirRepository.class);

    public LinkInMemoryFhirRepository(FhirContext context) {
        super(context);
    }

    public LinkInMemoryFhirRepository(FhirContext context, IBaseBundle bundle) {
        super(context, bundle);
    }

    @Override
    public <B extends IBaseBundle> B transaction(B transaction, Map<String, String> headers) {
        if (!(transaction instanceof Bundle)) {
            return transaction;
        }

        Bundle bundle = (Bundle) transaction;

        if (bundle.getEntry() == null || bundle.getEntry().size() == 0) {
            return transaction;
        }

        for (Bundle.BundleEntryComponent entry : bundle.getEntry()) {
            if (entry != null && entry.hasResource()) {
                try {
                    // Ensure each resource has an ID, or create a GUID for them
                    if (!entry.getResource().hasId()) {
                        entry.getResource().setId(java.util.UUID.randomUUID().toString());
                    }

                    this.update(entry.getResource());
                } catch (Exception ex) {
                    logger.warn("Failed to process resource in transaction", ex);
                }
            }
        }

        return transaction;
    }
}
