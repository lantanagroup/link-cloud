package com.lantanagroup.link.validation.entities;

import org.hl7.fhir.r4.model.OperationOutcome;
import org.hl7.fhir.utilities.i18n.I18nConstants;
import org.junit.jupiter.api.Test;

import static org.junit.jupiter.api.Assertions.assertEquals;

class ResultCodeTest {

    @Test
    void extensionVersionMessagesAreInvalid() {
        // e.g. "Extension url 'http://hl7.org/fhir/5.0/StructureDefinition/extension-MeasureReport.supplementalData'
        // is not valid (invalid Version '5.0')"
        assertEquals(OperationOutcome.IssueType.INVALID, Result.getCode(I18nConstants.EXTENSION_EXT_VERSION_INVALID));
        assertEquals(OperationOutcome.IssueType.INVALID, Result.getCode(I18nConstants.EXTENSION_EXT_VERSION_INVALIDID));
        assertEquals(OperationOutcome.IssueType.INVALID, Result.getCode(I18nConstants.EXTENSION_EXT_VERSION_NOTALLOWED));
    }

    @Test
    void anUnknownIdIsNull_everyTime() {
        assertEquals(OperationOutcome.IssueType.NULL, Result.getCode("Not_A_Real_Message_Id"));
        assertEquals(OperationOutcome.IssueType.NULL, Result.getCode("Not_A_Real_Message_Id"));
    }
}
