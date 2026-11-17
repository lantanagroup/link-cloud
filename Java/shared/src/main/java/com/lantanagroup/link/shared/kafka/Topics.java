package com.lantanagroup.link.shared.kafka;

public class Topics {
    public static final String DATA_ACQUISITION_REQUESTED = "DataAcquisitionRequested";
    public static final String READY_FOR_VALIDATION = "ReadyForValidation";
    public static final String READY_FOR_VALIDATION_RETRY = "ReadyForValidation-Retry";
    public static final String READY_FOR_VALIDATION_ERROR = "ReadyForValidation-Error";
    public static final String RESOURCES_ACQUIRED_ERROR = "ResourcesAcquired-Error";
    public static final String MEASURE_REPORT_GENERATED = "MeasureReportGenerated";
    public static final String RESOURCES_NORMALIZED = "ResourcesNormalized";
    public static final String RESOURCES_NORMALIZED_ERROR = "ResourcesNormalized-Error";
    public static final String RESOURCES_NORMALIZED_RETRY = "ResourcesNormalized-Retry";
    public static final String EVALUATION_REQUESTED = "EvaluationRequested";
    public static final String EVALUATION_REQUESTED_ERROR = "EvaluationRequested-Error";
    public static final String EVALUATION_REQUESTED_RETRY = "EvaluationRequested-Retry";
    public static final String VALIDATION_COMPLETE = "ValidationComplete";
    public static final String RETENTION_CHECK_REQUESTED = "RetentionCheckRequested";
    public static final String RETENTION_CHECK_REQUESTED_RETRY = "RetentionCheckRequested-Retry";
    public static final String RETENTION_CHECK_REQUESTED_ERROR = "RetentionCheckRequested-Error";
    public static final String RETENTION_CHECK_COMPLETE = "RetentionCheckComplete";
    public static final String RETENTION_CHECK_COMPLETE_RETRY = "RetentionCheckComplete-Retry";
    public static final String RETENTION_CHECK_COMPLETE_ERROR = "RetentionCheckComplete-Error";
    public static final String SERVICE_HEALTH_CHECK = "Service-Healthcheck";
    public static final String SHADOW_COMPARE_EVENT = "ShadowCompareEvent";
}
