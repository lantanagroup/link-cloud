package com.lantanagroup.link.shared.mongo;

import org.bson.Document;
import org.springframework.data.mongodb.core.aggregation.AggregationOperation;

public class CustomAggregationOperation implements AggregationOperation {
    private final Document operation;

    public CustomAggregationOperation(Document operation) {
        this.operation = operation;
    }

    @Override
    public Document toDocument(org.springframework.data.mongodb.core.aggregation.AggregationOperationContext context) {
        return context.getMappedObject(operation);
    }
}