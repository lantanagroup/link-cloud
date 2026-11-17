package com.lantanagroup.link.validation.services.execution.executors;

import java.util.Set;
import java.util.regex.Pattern;

/** Which resources a check's FHIRPath expression can select anything from. */
final class ExpressionTargets {

    /**
     * Abstract types an expression may start with. FHIRPath matches a leading type name against the resource's type
     * and every type it derives from, so {@code Resource.meta} selects from every resource and
     * {@code DomainResource.text} from nearly every one: neither names one type to narrow the entries to.
     */
    private static final Set<String> ABSTRACT_TYPES = Set.of("Resource", "DomainResource", "MetadataResource");

    /** Operators and functions whose operands can each start from a different type. */
    private static final Pattern COMBINES_EXPRESSIONS = Pattern.compile("\\||\\b(?:union|combine|iif)\\s*\\(");

    private ExpressionTargets() {
    }

    /**
     * The resource type an expression starts with ({@code Observation} for {@code Observation.code} or
     * {@code (Observation.code)}), or null when it doesn't start with one ({@code code}, {@code %resource.code}) or
     * starts with an abstract type ({@code Resource.meta}), which does not narrow it to one type.
     */
    static String leadingResourceType(String expression) {
        int i = 0;
        while (i < expression.length()
                && (Character.isWhitespace(expression.charAt(i)) || expression.charAt(i) == '(')) {
            i++;
        }
        int start = i;
        while (i < expression.length()
                && (Character.isLetterOrDigit(expression.charAt(i)) || expression.charAt(i) == '_')) {
            i++;
        }

        if (start == i || i >= expression.length() || expression.charAt(i) != '.') {
            return null;
        }
        String token = expression.substring(start, i);
        return Character.isUpperCase(token.charAt(0)) && !ABSTRACT_TYPES.contains(token) ? token : null;
    }

    /**
     * The only resource type a path can select from, or null when it may select from several. A path starting
     * with a type name selects nothing from a resource of another type, unless it combines a second expression
     * ({@code |}, {@code union(}, {@code combine(} or {@code iif(}), which may start from another type.
     */
    static String onlyResourceType(String path) {
        String type = leadingResourceType(path);
        if (type == null) {
            return null;
        }
        String outsideLiterals = path.replaceAll("'(?:[^'\\\\]|\\\\.)*'", "''");
        return COMBINES_EXPRESSIONS.matcher(outsideLiterals).find() ? null : type;
    }
}
