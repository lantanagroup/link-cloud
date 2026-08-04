package com.lantanagroup.link.validation.exceptions;

import java.util.UUID;

public class FacilityOverrideNotFoundException extends RuntimeException {
    public FacilityOverrideNotFoundException(UUID overrideId) {
        super("Facility override not found: " + overrideId);
    }
}
