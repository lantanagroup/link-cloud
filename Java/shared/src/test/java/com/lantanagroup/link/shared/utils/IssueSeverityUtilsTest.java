package com.lantanagroup.link.shared.utils;

import org.hl7.fhir.r4.model.OperationOutcome;
import org.junit.jupiter.api.Test;

import java.util.EnumSet;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertThrows;

public class IssueSeverityUtilsTest {
    @Test
    public void atLeastAsSevere_warningIncludesFatalErrorAndWarning() {
        assertEquals(
                EnumSet.of(
                        OperationOutcome.IssueSeverity.FATAL,
                        OperationOutcome.IssueSeverity.ERROR,
                        OperationOutcome.IssueSeverity.WARNING),
                IssueSeverityUtils.atLeastAsSevere(OperationOutcome.IssueSeverity.WARNING));
    }

    @Test
    public void atLeastAsSevere_fatalIsOnlyFatal() {
        assertEquals(
                EnumSet.of(OperationOutcome.IssueSeverity.FATAL),
                IssueSeverityUtils.atLeastAsSevere(OperationOutcome.IssueSeverity.FATAL));
    }

    @Test
    public void atLeastAsSevere_rejectsNullThreshold() {
        assertThrows(IllegalArgumentException.class,
                () -> IssueSeverityUtils.atLeastAsSevere(OperationOutcome.IssueSeverity.NULL));
        assertThrows(IllegalArgumentException.class, () -> IssueSeverityUtils.atLeastAsSevere(null));
    }
}
