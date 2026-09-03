package com.lantanagroup.link.validation.services.shadow;

import com.lantanagroup.link.validation.entities.Result;

import java.util.ArrayDeque;
import java.util.ArrayList;
import java.util.Deque;
import java.util.HashMap;
import java.util.List;
import java.util.Map;
import java.util.regex.Pattern;

/**
 * Compares the legacy engine's output against the modern engine's for the same payload. Findings are
 * matched by a {@code (patientId, expression)} key (positionally, since the engines don't share finding
 * ids), then a match only counts once severity and message agree too -- otherwise it's a severity change.
 */
public final class ResultDiff {

    /**
     * Matches a Java {@code Object#toString()} identity hash (e.g. {@code
     * ValueSet$ConceptSetFilterComponent@5a0bb980}) so it can be stripped before matching -- it's a
     * JVM-run-specific artifact that differs between runs even for an identical finding.
     */
    private static final Pattern OBJECT_IDENTITY_HASH = Pattern.compile("@[0-9a-fA-F]{1,8}\\b");

    public record SeverityChange(Result legacy, Result modern) {
    }

    /** A legacy/modern pair matched on {@code (patientId, expression)} with the same severity and message. */
    public record MatchedPair(Result legacy, Result modern) {
    }

    private final List<Result> added;
    private final List<Result> missing;
    private final List<SeverityChange> severityChanged;
    private final List<MatchedPair> matched;

    private ResultDiff(List<Result> added, List<Result> missing, List<SeverityChange> severityChanged, List<MatchedPair> matched) {
        this.added = added;
        this.missing = missing;
        this.severityChanged = severityChanged;
        this.matched = matched;
    }

    public static ResultDiff between(List<Result> legacyResults, List<Result> newResults) {
        Map<String, Deque<Result>> legacyByKey = groupByKey(legacyResults);

        List<Result> added = new ArrayList<>();
        List<SeverityChange> severityChanged = new ArrayList<>();
        List<MatchedPair> matched = new ArrayList<>();

        for (Result newResult : newResults) {
            Deque<Result> candidates = legacyByKey.get(key(newResult));
            Result legacyMatch = (candidates != null) ? candidates.poll() : null;
            if (legacyMatch == null) {
                added.add(newResult);
            } else if (legacyMatch.getSeverity() == newResult.getSeverity()) {
                matched.add(new MatchedPair(legacyMatch, newResult));
            } else {
                severityChanged.add(new SeverityChange(legacyMatch, newResult));
            }
        }

        List<Result> missing = legacyByKey.values().stream()
                .flatMap(Deque::stream)
                .toList();

        return new ResultDiff(added, missing, severityChanged, matched);
    }

    private static Map<String, Deque<Result>> groupByKey(List<Result> results) {
        Map<String, Deque<Result>> map = new HashMap<>();
        for (Result result : results) {
            map.computeIfAbsent(key(result), k -> new ArrayDeque<>()).add(result);
        }
        return map;
    }

    private static String key(Result result) {
        return safe(result.getExpression()) + " " + normalizeMessage(safe(result.getMessage()));
    }

    private static String safe(String value) {
        return value == null ? "" : value;
    }

    private static String normalizeMessage(String message) {
        return OBJECT_IDENTITY_HASH.matcher(message).replaceAll("");
    }

    public boolean isEmpty() {
        return added.isEmpty() && missing.isEmpty() && severityChanged.isEmpty();
    }

    public String summary() {
        return String.format("added=%d missing=%d severityChanged=%d matched=%d",
                added.size(), missing.size(), severityChanged.size(), matched.size());
    }

    public List<Result> getAdded() {
        return added;
    }

    public List<Result> getMissing() {
        return missing;
    }

    public List<SeverityChange> getSeverityChanged() {
        return severityChanged;
    }

    public List<MatchedPair> getMatched() {
        return matched;
    }

    public int getMatchedCount() {
        return matched.size();
    }
}
