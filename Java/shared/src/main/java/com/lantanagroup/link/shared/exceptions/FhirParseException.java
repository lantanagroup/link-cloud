package com.lantanagroup.link.shared.exceptions;

import ca.uhn.fhir.parser.DataFormatException;
import com.fasterxml.jackson.core.JsonProcessingException;

public class FhirParseException extends JsonProcessingException {
    public FhirParseException(DataFormatException cause) {
        super(cause.getMessage(), cause);
    }
}
