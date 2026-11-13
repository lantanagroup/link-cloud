package com.lantanagroup.link.validation.services.execution.executors;

/** Which resources a check's FHIRPath expression can select anything from. */
final class ExpressionTargets {

    private ExpressionTargets() {
    }

    /**
     * The resource type an expression starts with ({@code Observation} for {@code Observation.code} or
     * {@code (Observation.code)}), or null when it doesn't start with one ({@code code}, {@code %resource.code}).
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
        return Character.isUpperCase(token.charAt(0)) ? token : null;
    }

    /**
     * The only resource type a path can select from, or null when it may select from several. A path starting
     * with a type name selects nothing from a resource of another type, unless it combines a second expression
     * ({@code |} or {@code union(}), which may start from another type.
     */
    static String onlyResourceType(String path) {
        String type = leadingResourceType(path);
        if (type == null) {
            return null;
        }
        String outsideLiterals = path.replaceAll("'(?:[^'\\\\]|\\\\.)*'", "''");
        return outsideLiterals.contains("|") || outsideLiterals.contains("union(") ? null : type;
    }
}
