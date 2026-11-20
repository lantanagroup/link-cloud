package com.lantanagroup.link.validation.services;

import org.hl7.fhir.r4.model.Bundle;
import org.hl7.fhir.r4.model.Resource;

import java.util.regex.Matcher;
import java.util.regex.Pattern;

/**
 * Inserts the entry's resource id into a HAPI validation location, so it names an instance and not just
 * a position: {@code Bundle.entry[14].resource.ofType(Condition).code} becomes
 * {@code Bundle.entry[14].resource.ofType(Condition).where(id = 'cond-1').code}. The entry index is kept
 * because {@code categories.json} rules match on it. Returns the input unchanged when no id is usable.
 */
public final class InstanceAnchoredExpression {

    private static final Pattern ENTRY_PREFIX =
            Pattern.compile("^Bundle\\.entry\\[(\\d{1,9})]\\.resource\\.ofType\\(\\w+\\)");

    /** No quote can match, so splicing an id into the FHIRPath literal below needs no escaping. */
    private static final Pattern FHIR_ID = Pattern.compile("[A-Za-z0-9\\-.]{1,64}");

    /** Must stay a prefix of ID_FILTER_OPEN, or anchoring stops being idempotent. */
    private static final String ID_FILTER_PREFIX = ".where(id ";
    private static final String ID_FILTER_OPEN = ".where(id = '";
    private static final String ID_FILTER_CLOSE = "')";

    private InstanceAnchoredExpression() {
    }

    public static String anchor(String expression, Bundle bundle) {
        if (expression == null || bundle == null) {
            return expression;
        }
        Matcher matcher = ENTRY_PREFIX.matcher(expression);
        if (!matcher.find()) {
            return expression;
        }
        int prefixEnd = matcher.end();
        if (expression.startsWith(ID_FILTER_PREFIX, prefixEnd)) {
            return expression;
        }
        var entries = bundle.getEntry();
        int index = Integer.parseInt(matcher.group(1));
        if (index >= entries.size()) {
            return expression;
        }
        Resource resource = entries.get(index).getResource();
        if (resource == null) {
            return expression;
        }
        // getIdPart(), not getId(): the id element may hold a qualified id or fullUrl.
        String id = resource.getIdElement().getIdPart();
        if (id == null || !FHIR_ID.matcher(id).matches()) {
            return expression;
        }
        return new StringBuilder(
                expression.length() + id.length() + ID_FILTER_OPEN.length() + ID_FILTER_CLOSE.length())
                .append(expression, 0, prefixEnd)
                .append(ID_FILTER_OPEN).append(id).append(ID_FILTER_CLOSE)
                .append(expression, prefixEnd, expression.length())
                .toString();
    }
}
