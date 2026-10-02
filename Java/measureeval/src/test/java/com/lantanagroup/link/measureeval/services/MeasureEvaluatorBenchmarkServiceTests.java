package com.lantanagroup.link.measureeval.services;

import ca.uhn.fhir.context.FhirContext;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;

import java.io.IOException;
import java.nio.file.Paths;
import java.util.Objects;

public class MeasureEvaluatorBenchmarkServiceTests {
    // -------------------------------------------------
    //                CONFIG & CONSTANTS
    // -------------------------------------------------

    private static final String ACH_MEASURE_PACKAGE_PATH = Objects.requireNonNull(
            MeasureEvaluatorBenchmarkServiceTests.class.getClassLoader().getResource(
                    "NHSNdQMAcuteCareHospitalInitialPopulation-bundle.json")).getPath();

    private static final String BCS_MEASURE_PACKAGE_PATH = Objects.requireNonNull(
            MeasureEvaluatorBenchmarkServiceTests.class.getClassLoader().getResource(
                    "BreastCancerScreeningFHIR-bundle.json")).getPath();

    private static final String ACH_PATIENT_DATA_DIRECTORY = Paths.get("src","test", "resources", "measure-ach-population-synthea").toFile().getAbsolutePath();

    private static final String BCS_PATIENT_DATA_DIRECTORY = Paths.get("src","test", "resources", "measure-bcs-population-synthea").toFile().getAbsolutePath();

    private static final FhirContext FHIR_CONTEXT = FhirContext.forR4Cached();

    @Test
    @DisplayName("Initialize, evaluate and generate the performance report for the ACH measure")
    void testACHMeasure() throws IOException {
        MeasureEvaluatorBenchmarkService service = new MeasureEvaluatorBenchmarkService(ACH_MEASURE_PACKAGE_PATH, ACH_PATIENT_DATA_DIRECTORY, "2022-01-01", "2022-12-31", FHIR_CONTEXT, 1000);
        service.initialize();
        service.evaluateAllPatients(service.getSyntheaBundles());
        service.generatePerformanceReport();
    }

    @Test
    @DisplayName("Initialize, evaluate and generate the performance report for the BCS measure")
    void testBCSMeasure() throws IOException {
        MeasureEvaluatorBenchmarkService service = new MeasureEvaluatorBenchmarkService(BCS_MEASURE_PACKAGE_PATH, BCS_PATIENT_DATA_DIRECTORY, "2022-01-01", "2022-12-31", FHIR_CONTEXT, 1000);
        service.initialize();
        service.evaluateAllPatients();
        service.generatePerformanceReport();
    }
}
