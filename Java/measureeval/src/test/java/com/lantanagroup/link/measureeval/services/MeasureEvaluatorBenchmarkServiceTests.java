package com.lantanagroup.link.measureeval.services;

import ca.uhn.fhir.context.FhirContext;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;

import java.io.IOException;

public class MeasureEvaluatorBenchmarkServiceTests {
    // -------------------------------------------------
    //                CONFIG & CONSTANTS
    // -------------------------------------------------

    private static final String MEASURE_PACKAGE_PATH =
            "/Users/christopherschuler/Documents/workspace/Lantana/nhsn-measures/bundles/measure/NHSNdQMAcuteCareHospitalInitialPopulation/NHSNdQMAcuteCareHospitalInitialPopulation-bundle.json";

    private static final String PATIENT_DATA_DIRECTORY =
            "/Users/christopherschuler/Documents/workspace/Lantana/synthea/output/fhir";

    private static final FhirContext FHIR_CONTEXT = FhirContext.forR4Cached();

    @Test
    @DisplayName("Initialize, evaluate and generate the performance report")
    void testEvaluateMeasure() throws IOException {
        MeasureEvaluatorBenchmarkService service = new MeasureEvaluatorBenchmarkService(MEASURE_PACKAGE_PATH, PATIENT_DATA_DIRECTORY, FHIR_CONTEXT);
        service.initialize();
        service.evaluateAllPatients();
        service.generatePerformanceReport();
    }
}
