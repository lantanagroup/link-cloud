package com.lantanagroup.link.shared.kafka;

public class Topics {
    public static final String DATA_ACQUISITION_REQUESTED = "DataAcquisitionRequested";
    public static final String READY_FOR_VALIDATION = "ReadyForValidation";
    public static final String RESOURCE_ACQUIRED_ERROR = "ResourceAcquired-Error";
    public static final String RESOURCE_EVALUATED = "ResourceEvaluated";
    public static final String RESOURCE_NORMALIZED = "ResourceNormalized";
    public static final String RESOURCE_NORMALIZED_ERROR = "ResourceNormalized-Error";
    public static final String RESOURCE_NORMALIZED_RETRY = "ResourceNormalized-Retry";
    public static final String EVALUATION_REQUESTED = "EvaluationRequested";
    public static final String EVALUATION_REQUESTED_ERROR = "EvaluationRequested-Error";
    public static final String EVALUATION_REQUESTED_RETRY = "EvaluationRequested-Retry";
    public static final String VALIDATION_COMPLETE = "ValidationComplete";
}
