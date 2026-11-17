package com.lantanagroup.link.validation.services.execution;

import ca.uhn.fhir.context.support.IValidationSupport.CodeValidationIssue;
import ca.uhn.fhir.context.support.IValidationSupport.CodeValidationIssueCoding;
import ca.uhn.fhir.context.support.IValidationSupport.CodeValidationResult;

import java.util.List;

/**
 * Decides whether a terminology-validation result describes an <em>unresolvable binding</em> — the
 * bound value set or code system could not be resolved, expanded, or loaded — as opposed to a
 * genuine data problem (a code that really is not a member of a value set that <em>was</em>
 * resolved, or an invalid code).
 *
 * <p>When the binding cannot be resolved the check has not actually been evaluated. Standard FHIR
 * validation treats an unresolvable required binding as a warning rather than an error, so we
 * surface it as a "not evaluated" (INCONCLUSIVE) finding that score aggregation ignores, instead of
 * inflating error counts with an environment/terminology gap that disappears once the value sets are
 * loaded in dev/stage/prod.
 *
 * <p><strong>The structured HAPI signal is primary.</strong> HAPI 8.x tags each validation issue
 * with a {@link CodeValidationIssueCoding}: {@code NOT_FOUND} means the value set / code system could
 * not be resolved (not evaluated), while {@code NOT_IN_VS} / {@code INVALID_CODE} /
 * {@code INVALID_DISPLAY} are genuine data problems. Message-text matching is only a last-resort
 * fallback for validators that populate no structured issues, and its markers are deliberately narrow
 * so a genuine "code is not in the value set" failure is never misread as "not evaluated".
 */
public final class UnresolvedBindingClassifier {

    private UnresolvedBindingClassifier() {
    }

    // Only phrases that unambiguously mean the value set / code system RESOURCE could not be resolved
    // or expanded. Deliberately excludes ambiguous fragments like "unable to validate code", "not
    // supported", or "is not known" — HAPI emits those for genuine membership failures and for
    // structural (non-terminology) problems too, so matching them would hide real defects. The exact
    // phrasings below were captured from real HAPI 8.10 output (see UnresolvedBindingClassifierTest);
    // "not found" is safe despite being broad because the membership guard below runs first and
    // excludes the "... not found in the value set ..." membership wording.
    private static final List<String> UNRESOLVED_MARKERS = List.of(
            "could not be resolved",
            "unable to resolve",
            "unable to expand",
            "error expanding",
            "failed to expand",
            "could not be found",                  // "... code system could not be found"
            "not found",                           // "ValueSet '<url>' not found" (Terminology_TX_ValueSet_NotFound)
            "is unknown and can't be validated");  // "CodeSystem is unknown and can't be validated: <url>"

    // Membership / validity phrasing: the value set (or code system) IS resolved and the CODE is the
    // one that is absent or invalid. These are genuine data errors, never "not evaluated" — and HAPI
    // phrases them "... was not found in the value set 'X'" / "... is not in the value set", which
    // would otherwise be caught by the "was not found" / "could not be found" markers above. This
    // guard is checked first so those markers only fire when the value set/code system itself is the
    // missing subject.
    private static final List<String> MEMBERSHIP_MARKERS = List.of(
            "in the value set",
            "in value set",
            "in the code system",
            "in code system");

    // HAPI's InstanceValidator i18n message-id keys are stable and locale-proof, unlike the (English)
    // message text. On the FHIR-conformance path the message id is the only structured discriminator
    // HAPI exposes (the OperationOutcome issue.code is a generic PROCESSING), so classification keys on
    // it first. Contains-matched against the lowercased id.

    // Unambiguous "the value set RESOURCE could not be found" key, e.g. Terminology_TX_ValueSet_NotFound.
    private static final List<String> UNRESOLVED_MESSAGE_ID_MARKERS = List.of("valueset_notfound");

    // "No valid code / none of the codings are in the (required) value set" family, e.g.
    // Terminology_TX_NoValid_1_CC / _2_CC / ...
    private static final List<String> MEMBERSHIP_MESSAGE_ID_MARKERS = List.of("novalid");

    /**
     * Structured-first classification of a terminology {@link CodeValidationResult}. A single genuine
     * issue (NOT_IN_VS / INVALID_CODE / INVALID_DISPLAY) means "not unresolvable" even if a NOT_FOUND
     * is also present. Only when there is no conclusive structured signal do we fall back to the
     * message heuristic.
     */
    public static boolean isUnresolvable(CodeValidationResult result) {
        if (result == null) {
            return false;
        }
        List<CodeValidationIssue> issues = result.getCodeValidationIssues();
        if (issues != null && !issues.isEmpty()) {
            boolean anyGenuine = false;
            boolean anyNotFound = false;
            for (CodeValidationIssue issue : issues) {
                CodeValidationIssueCoding coding = issue.getCoding();
                if (coding == CodeValidationIssueCoding.NOT_IN_VS
                        || coding == CodeValidationIssueCoding.INVALID_CODE
                        || coding == CodeValidationIssueCoding.INVALID_DISPLAY) {
                    anyGenuine = true;
                } else if (coding == CodeValidationIssueCoding.NOT_FOUND) {
                    anyNotFound = true;
                }
            }
            if (anyGenuine) {
                return false;   // a real membership/validity problem is present -> keep as a finding
            }
            if (anyNotFound) {
                return true;    // only resolution failures -> not evaluated
            }
            // issues present but none carried a conclusive coding -> fall through to the message text
        }
        return isUnresolvable(result.getMessage());
    }

    /**
     * Conformance-path classification: does this validator message mean the bound value set or code
     * system could not be resolved / expanded (i.e. the binding was <em>not evaluated</em>)? Keys on the
     * stable HAPI message id first, then falls back to the text heuristic. Pass only terminology
     * messages (the caller gates on the message id containing "terminology").
     */
    public static boolean isUnresolvedConformanceMessage(String messageId, String message) {
        if (matchesId(messageId, UNRESOLVED_MESSAGE_ID_MARKERS)) {
            return true;
        }
        return isUnresolvable(message);
    }

    /**
     * True when the message is a "code is not a valid member of the value set" verdict — by HAPI message
     * id or by membership phrasing. When the value set actually resolved this is a genuine data error;
     * when it did not (a co-located resolution failure), the conformance executor downgrades it as a
     * downstream consequence, since HAPI phrases "required binding failed because the value set could
     * not be expanded" identically to a real membership miss.
     */
    public static boolean isMembershipFailure(String messageId, String message) {
        if (matchesId(messageId, MEMBERSHIP_MESSAGE_ID_MARKERS)) {
            return true;
        }
        if (message == null || message.isBlank()) {
            return false;
        }
        String lower = message.toLowerCase();
        return MEMBERSHIP_MARKERS.stream().anyMatch(lower::contains);
    }

    private static boolean matchesId(String messageId, List<String> markers) {
        if (messageId == null) {
            return false;
        }
        String id = messageId.toLowerCase();
        return markers.stream().anyMatch(id::contains);
    }

    /** Last-resort message-text heuristic, used only when no structured signal is available. */
    public static boolean isUnresolvable(String message) {
        if (message == null || message.isBlank()) {
            return false;
        }
        String lower = message.toLowerCase();
        // A membership/validity failure is a genuine data error even when its text contains a marker
        // ("... was not found in the value set 'X'"), so exclude it before matching the markers.
        if (MEMBERSHIP_MARKERS.stream().anyMatch(lower::contains)) {
            return false;
        }
        return UNRESOLVED_MARKERS.stream().anyMatch(lower::contains);
    }
}
