package com.lantanagroup.link.measureeval.kafka;

public class Topics {
    public static final String DATA_ACQUISITION_REQUESTED = "DataAcquisitionRequested";
    public static final String RESOURCE_ACQUIRED = "ResourceAcquired";
    public static final String RESOURCE_ACQUIRED_ERROR = "ResourceAcquired-Error";
    public static final String RESOURCE_EVALUATED = "ResourceEvaluated";
    public static final String RESOURCE_NORMALIZED = "ResourceNormalized";
    public static String error(String topic) {
        return String.format("%s-Error", topic);
    }
    public static String retry(String topic) {
        return String.format("%s-Retry", topic);
    }
}
