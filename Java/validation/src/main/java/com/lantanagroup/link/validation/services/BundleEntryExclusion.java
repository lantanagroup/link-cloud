package com.lantanagroup.link.validation.services;

import org.hl7.fhir.r4.model.Bundle;
import org.hl7.fhir.r4.model.Resource;

import java.util.ArrayList;
import java.util.Collection;
import java.util.List;
import java.util.Map;
import java.util.Set;
import java.util.TreeMap;
import java.util.regex.Matcher;
import java.util.regex.Pattern;
import java.util.stream.Collectors;

/**
 * A patient bundle with the entries of some resource types left out, for validating only what is wanted
 * ({@code link.validation-excluded-resource-types}), and the means to put each finding's location back on the
 * entry's position in the original bundle.
 *
 * <p>The legacy validator locates findings by position ({@code Bundle.entry[21].resource...}), and those expressions
 * are written into the submission (the pre-qualification OperationOutcome), so a finding must name the entry's
 * position in the bundle as submitted, not in the filtered copy. The copy shares the original's entry objects: nothing
 * is cloned, and the original bundle is left as it was.
 */
final class BundleEntryExclusion {

    private static final Pattern LEADING_ENTRY_INDEX = Pattern.compile("^Bundle\\.entry\\[(\\d+)]");

    private final Bundle filtered;
    private final int[] originalIndex;
    private final Map<String, Integer> skipped;
    private final int originalCount;

    private BundleEntryExclusion(Bundle filtered, int[] originalIndex, Map<String, Integer> skipped, int originalCount) {
        this.filtered = filtered;
        this.originalIndex = originalIndex;
        this.skipped = skipped;
        this.originalCount = originalCount;
    }

    /**
     * Leaves out the entries whose resource type is in {@code excludedTypes} (matched ignoring case). With nothing to
     * leave out, {@link #bundle()} is the original bundle itself.
     */
    static BundleEntryExclusion of(Bundle bundle, Collection<String> excludedTypes) {
        Set<String> excluded = excludedTypes == null ? Set.of() : excludedTypes.stream()
                .filter(type -> type != null && !type.isBlank())
                .map(type -> type.trim().toLowerCase())
                .collect(Collectors.toSet());
        List<Bundle.BundleEntryComponent> entries = bundle.getEntry();
        Map<String, Integer> skipped = new TreeMap<>();
        List<Integer> kept = new ArrayList<>(entries.size());
        for (int index = 0; index < entries.size(); index++) {
            Resource resource = entries.get(index).getResource();
            String type = resource == null ? null : resource.fhirType();
            if (type != null && excluded.contains(type.toLowerCase())) {
                skipped.merge(type, 1, Integer::sum);
            } else {
                kept.add(index);
            }
        }
        if (skipped.isEmpty()) {
            return new BundleEntryExclusion(bundle, null, Map.of(), entries.size());
        }

        Bundle filtered = new Bundle();
        filtered.setIdElement(bundle.getIdElement());
        filtered.setMeta(bundle.getMeta());
        filtered.setLanguageElement(bundle.getLanguageElement());
        filtered.setIdentifier(bundle.getIdentifier());
        filtered.setTypeElement(bundle.getTypeElement());
        filtered.setTimestampElement(bundle.getTimestampElement());
        filtered.setLink(bundle.getLink());
        int[] originalIndex = new int[kept.size()];
        for (int position = 0; position < kept.size(); position++) {
            originalIndex[position] = kept.get(position);
            filtered.addEntry(entries.get(kept.get(position)));
        }
        return new BundleEntryExclusion(filtered, originalIndex, skipped, entries.size());
    }

    /** The bundle to validate: the filtered copy, or the original when nothing was left out. */
    Bundle bundle() {
        return filtered;
    }

    /** True when entries were left out. */
    boolean excludedAny() {
        return originalIndex != null;
    }

    /** How many entries of each resource type were left out. */
    Map<String, Integer> skipped() {
        return skipped;
    }

    int originalCount() {
        return originalCount;
    }

    /**
     * Rewrites a leading {@code Bundle.entry[k]} from the filtered copy's position to the original bundle's. Anything
     * else, including null, is returned unchanged.
     */
    String toOriginalPath(String path) {
        if (originalIndex == null || path == null) {
            return path;
        }
        Matcher matcher = LEADING_ENTRY_INDEX.matcher(path);
        if (!matcher.find()) {
            return path;
        }
        int position = Integer.parseInt(matcher.group(1));
        if (position < 0 || position >= originalIndex.length) {
            return path;
        }
        return "Bundle.entry[" + originalIndex[position] + "]" + path.substring(matcher.end());
    }
}
