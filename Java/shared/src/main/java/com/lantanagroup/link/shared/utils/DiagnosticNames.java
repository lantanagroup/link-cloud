package com.lantanagroup.link.shared.utils;

public class DiagnosticNames {
    public static final String CORRELATION_ID = "correlation.id";
    public static final String FACILITY_ID = "facility.id";
    public static final String ISSUE_COUNT_ACCEPTABLE = "issue.count.acceptable";
    public static final String ISSUE_COUNT_TOTAL = "issue.count.total";
    public static final String ISSUE_COUNT_UNACCEPTABLE = "issue.count.unacceptable";
    public static final String ISSUE_COUNT_UNCATEGORIZED = "issue.count.uncategorized";
    public static final String PATIENT_ID = "patient.id";
    public static final String PHASE = "phase";
    public static final String REPORT_TRACKING_ID = "report.tracking.id";
    public static final String REPORT_TYPE = "report.types";
    public static final String RESOURCE_COUNT = "resource.count";
    public static final String VALIDATION_OUTCOME = "validation.outcome";

    public static String normalizePhase(String phase) {
        if (phase == null || phase.length() < 2) {
            return phase;
        }
        return phase.substring(0, 1).toUpperCase() + phase.substring(1).toLowerCase();
    }
}
