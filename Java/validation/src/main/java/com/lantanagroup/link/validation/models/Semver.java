package com.lantanagroup.link.validation.models;

import com.lantanagroup.link.validation.entities.RubricVersion;

import java.util.Comparator;
import java.util.regex.Matcher;
import java.util.regex.Pattern;

// Semantic ordering in Java — the DB stores semver as a string, and lexically 1.10.0 < 1.9.0.
public record Semver(long major, long minor, long patch, String preRelease) implements Comparable<Semver> {

    public static final Pattern PATTERN =
            Pattern.compile("^(\\d+)\\.(\\d+)\\.(\\d+)(?:-([0-9A-Za-z][0-9A-Za-z.-]*))?$");

    public static Semver parse(String value) {
        if (value == null) {
            throw new IllegalArgumentException("semver is null");
        }
        Matcher m = PATTERN.matcher(value.trim());
        if (!m.matches()) {
            throw new IllegalArgumentException("'" + value + "' is not a valid semantic version");
        }
        try {
            // long, not int — timestamp-style components like 1.0.1753017600000 must not overflow
            return new Semver(
                    Long.parseLong(m.group(1)),
                    Long.parseLong(m.group(2)),
                    Long.parseLong(m.group(3)),
                    m.group(4));
        } catch (NumberFormatException e) {
            throw new IllegalArgumentException("'" + value + "' has a numeric component out of range");
        }
    }

    public static boolean isValid(String value) {
        if (value == null) {
            return false;
        }
        try {
            parse(value);
            return true;
        } catch (IllegalArgumentException e) {
            return false;
        }
    }

    // pre-validation rows may hold unparseable values; sort those lowest instead of throwing
    public static Comparator<RubricVersion> versionComparator() {
        return Comparator.comparing(v -> safeParse(v.getSemver()),
                Comparator.nullsFirst(Comparator.naturalOrder()));
    }

    private static Semver safeParse(String value) {
        try {
            return value != null ? parse(value) : null;
        } catch (IllegalArgumentException e) {
            return null;
        }
    }

    // canonical form with leading zeros stripped, e.g. "01.02.3" -> "1.2.3" — used so
    // storage/lookup treat numerically-equal segments as the same version
    public String toCanonicalString() {
        return major + "." + minor + "." + patch + (preRelease != null ? "-" + preRelease : "");
    }

    // canonical form if the value parses, otherwise the value unchanged. This is the single
    // normalization entry point every semver-keyed storage/lookup goes through, so "01.2.3" and
    // "1.2.3" resolve to one row; an unparseable value is passed through so it still 404s/400s
    // exactly as it did before normalization instead of failing here.
    public static String normalize(String value) {
        try {
            return parse(value).toCanonicalString();
        } catch (IllegalArgumentException e) {
            return value;
        }
    }

    @Override
    public int compareTo(Semver o) {
        int c = Long.compare(major, o.major);
        if (c != 0) return c;
        c = Long.compare(minor, o.minor);
        if (c != 0) return c;
        c = Long.compare(patch, o.patch);
        if (c != 0) return c;
        if (preRelease == null) return o.preRelease == null ? 0 : 1;
        if (o.preRelease == null) return -1;
        return preRelease.compareTo(o.preRelease);
    }
}
