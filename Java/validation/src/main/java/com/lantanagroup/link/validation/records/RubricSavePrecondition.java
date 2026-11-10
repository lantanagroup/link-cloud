package com.lantanagroup.link.validation.records;

/**
 * What a caller saving a rubric version says the version's state is, so that a save made on stale
 * knowledge is refused instead of silently replacing someone else's work.
 *
 * @param expectedChecksum the checksum of the draft the caller read (HTTP {@code If-Match}); the save is
 *                         refused when the stored draft has another, or is gone. Null: not checked
 * @param mustNotExist     the caller is creating the version (HTTP {@code If-None-Match: *}); the save is
 *                         refused when the version already exists in any status
 */
public record RubricSavePrecondition(String expectedChecksum, boolean mustNotExist) {

    /** No precondition: a draft is replaced whatever it holds, as before preconditions existed. */
    public static final RubricSavePrecondition NONE = new RubricSavePrecondition(null, false);

    public boolean isNone() {
        return expectedChecksum == null && !mustNotExist;
    }
}
