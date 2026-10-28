package com.lantanagroup.link.shared.kafka.records.reportstatus;

import com.fasterxml.jackson.annotation.JsonProperty;

/** Patient reporting lifecycle. An event identifies the stage that completed. */
public enum PatientReportingStatus {
    @JsonProperty("PatientIdentified")
    PATIENT_IDENTIFIED,

    @JsonProperty("InitialAcquisition")
    INITIAL_ACQUISITION,

    @JsonProperty("InitialNormalization")
    INITIAL_NORMALIZATION,

    @JsonProperty("InitialEvaluation")
    INITIAL_EVALUATION,

    @JsonProperty("SupplementalAcquisition")
    SUPPLEMENTAL_ACQUISITION,

    @JsonProperty("SupplementalNormalization")
    SUPPLEMENTAL_NORMALIZATION,

    @JsonProperty("SupplementalEvaluation")
    SUPPLEMENTAL_EVALUATION,

    @JsonProperty("Validation")
    VALIDATION,

    @JsonProperty("Submission")
    SUBMISSION,

    @JsonProperty("Submitted")
    SUBMITTED,

    @JsonProperty("Failed")
    FAILED,

    @JsonProperty("NotReportable")
    NOT_REPORTABLE,

    @JsonProperty("NotSubmitted")
    NOT_SUBMITTED
}

