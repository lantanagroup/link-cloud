package com.lantanagroup.link.shared.logging;

import ch.qos.logback.core.boolex.PropertyConditionBase;
import lombok.Getter;
import lombok.Setter;

/**
 * A logback {@code <condition>} that is true when a property equals a value, ignoring case. Logback's own
 * {@code PropertyEqualityCondition} is case-sensitive, so {@code loki.enabled=TRUE} would not match
 * {@code true}, unlike the {@code equalsIgnoreCase} expression it replaces and Spring's own boolean binding.
 */
@Getter
@Setter
public class PropertyEqualsIgnoreCaseCondition extends PropertyConditionBase {

    private String key;

    private String value;

    @Override
    public void start() {
        if (key == null || value == null) {
            addError("PropertyEqualsIgnoreCaseCondition needs both a key and a value");
            return;
        }
        super.start();
    }

    @Override
    public boolean evaluate() {
        if (key == null || value == null) {
            return false;
        }
        return value.equalsIgnoreCase(property(key));
    }
}
