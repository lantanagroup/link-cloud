package com.lantanagroup.link.validation.services;

import ca.uhn.fhir.validation.ResultSeverityEnum;
import ca.uhn.fhir.validation.SingleValidationMessage;
import lombok.Getter;
import org.hl7.fhir.utilities.validation.ValidationMessage;

/**
 * A {@link SingleValidationMessage} that retains the core {@link ValidationMessage} it was converted from, so the
 * profile that produced the finding survives HAPI's conversion. See LEGLINK-1172.
 * <p>
 * HAPI's own conversion ({@code BaseValidatorBridge.doValidate}) drops the core message's
 * {@code validationContext}. {@link #create} reproduces that conversion field for field, so every inherited field
 * is identical to what HAPI would have produced.
 */
public class ProfileAttributedValidationMessage extends SingleValidationMessage {
    @Getter
    private final ValidationMessage coreMessage;

    private ProfileAttributedValidationMessage(ValidationMessage coreMessage) {
        this.coreMessage = coreMessage;
    }

    public static ProfileAttributedValidationMessage create(ValidationMessage coreMessage) {
        ProfileAttributedValidationMessage message = new ProfileAttributedValidationMessage(coreMessage);
        if (coreMessage.getCol() != -1) {
            message.setLocationCol(coreMessage.getCol());
        }
        if (coreMessage.getLine() != -1) {
            message.setLocationLine(coreMessage.getLine());
        }
        message.setLocationString(coreMessage.getLocation());
        message.setMessage(coreMessage.getMessage());
        if (coreMessage.getLevel() != null) {
            message.setSeverity(ResultSeverityEnum.fromCode(coreMessage.getLevel().toCode()));
        }
        // As in HAPI: a messageId containing a space is a raw message (e.g., from a terminology service), not a key.
        if (coreMessage.getMessageId() != null && !coreMessage.getMessageId().contains(" ")) {
            message.setMessageId(coreMessage.getMessageId());
        }
        if (coreMessage.hasSliceInfo()) {
            message.setSliceMessages(coreMessage.getSliceInfo().stream()
                    .map(ValidationMessage::getMessage)
                    .toList());
        }
        return message;
    }

    /**
     * The versioned canonical of the StructureDefinition being evaluated when this finding was raised, or null for
     * findings that belong to no profile.
     * <p>
     * This is accurate only because each bundle entry is validated standalone, which requires concurrent bundle
     * validation (see {@link ValidationService}). Validating a whole bundle in one pass leaves stale contexts on
     * later entries' messages.
     */
    public String getProfile() {
        return coreMessage.getValidationContext();
    }
}
