package com.lantanagroup.link.measureeval.services;

import org.hl7.fhir.r4.model.Library;

public interface LibraryResolver {
    Library resolve(String libraryId);
}
