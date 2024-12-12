package com.lantanagroup.link.measureeval.repositories;

import ca.uhn.fhir.context.FhirContext;
import org.hl7.fhir.instance.model.api.IBaseBundle;
import org.hl7.fhir.r4.model.Bundle;
import org.opencds.cqf.fhir.utility.repository.InMemoryFhirRepository;

import java.util.Map;

public class LinkInMemoryFhirRepository extends InMemoryFhirRepository {
    public LinkInMemoryFhirRepository(FhirContext context) {
        super(context);
    }

    public LinkInMemoryFhirRepository(FhirContext context, IBaseBundle bundle) {
        super(context, bundle);
    }

    @Override
    public <B extends IBaseBundle> B transaction(B transaction, Map<String, String> headers) {
        Bundle bundle = (Bundle) transaction;

        for (Bundle.BundleEntryComponent entry : bundle.getEntry()) {
            if (entry.hasResource()) {
                // Ensure each resource has an ID, or create a GUID for them
                if (!entry.getResource().hasId()) {
                    entry.getResource().setId(java.util.UUID.randomUUID().toString());
                }

                this.update(entry.getResource());
            }
        }

        return transaction;
    }
}
