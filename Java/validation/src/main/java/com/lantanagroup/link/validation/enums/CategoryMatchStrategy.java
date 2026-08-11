package com.lantanagroup.link.validation.enums;

public enum CategoryMatchStrategy {

    /**
     * Combines every matching category, per field, taking the worst value of each:
     * {@code acceptable=false} beats {@code acceptable=true}, and a higher severity beats a
     * lower one. The two fields are combined independently, so the resulting pair may be a
     * combination that no single matching category declares — that is intentional, and is the
     * conservative reading of "worst of".
     */
    WORST_OF,

    /**
     * Uses only the first matching category in sequence order (see
     * {@code CategorySequenceProvider}), ignoring any others.
     */
    FIRST_MATCH
}
