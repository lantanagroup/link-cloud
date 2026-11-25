package com.lantanagroup.link.shared.audit;

import com.fasterxml.jackson.annotation.JsonProperty;
import com.fasterxml.jackson.annotation.JsonPropertyOrder;

/**
 * One changed property of an audit event, matching the .NET {@code PropertyChangeModel} on the wire.
 *
 * @param propertyName         the changed property's name
 * @param initialPropertyValue the value before the change, as text
 * @param newPropertyValue     the value after the change, as text
 * @param entity               the entity type whose row changed; null on a change a handler added itself
 * @param rowKey               the primary key of the changed row; null where the producer did not know it
 */
@JsonPropertyOrder({"PropertyName", "InitialPropertyValue", "NewPropertyValue", "Entity", "RowKey"})
public record PropertyChange(
        @JsonProperty("PropertyName") String propertyName,
        @JsonProperty("InitialPropertyValue") String initialPropertyValue,
        @JsonProperty("NewPropertyValue") String newPropertyValue,
        @JsonProperty("Entity") String entity,
        @JsonProperty("RowKey") String rowKey) {

    /** A change with no entity or row key, as a handler-added change has in .NET. */
    public PropertyChange(String propertyName, String initialPropertyValue, String newPropertyValue) {
        this(propertyName, initialPropertyValue, newPropertyValue, null, null);
    }
}
