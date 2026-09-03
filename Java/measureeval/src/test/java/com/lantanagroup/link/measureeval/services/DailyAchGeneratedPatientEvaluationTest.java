package com.lantanagroup.link.measureeval.services;

import ca.uhn.fhir.context.FhirContext;
import com.lantanagroup.link.measureeval.reportability.IsInInitialPopulation;
import org.hl7.fhir.r4.model.Bundle;
import org.hl7.fhir.r4.model.DateTimeType;
import org.hl7.fhir.r4.model.MeasureReport;
import org.hl7.fhir.r4.model.Resource;
import org.hl7.fhir.r4.model.StringType;
import org.junit.jupiter.api.Assertions;
import org.junit.jupiter.api.Assumptions;
import org.junit.jupiter.api.Test;

import java.nio.file.Files;
import java.nio.file.Path;

class DailyAchGeneratedPatientEvaluationTest {
    private final FhirContext fhirContext = FhirContext.forR4Cached();

    @Test
    void generatedDailyEncounterQualifiesForInitialPopulation() throws Exception {
        var parser = fhirContext.newJsonParser();
        var repoRoot = Path.of("").toAbsolutePath();
        while (repoRoot != null && !Files.exists(repoRoot.resolve("DotNet/Automation/measures/NHSNAcuteCareHospitalDailyInitialPopulation.json"))) {
            repoRoot = repoRoot.getParent();
        }
        Assumptions.assumeTrue(repoRoot != null, "Skipping: could not locate repo root from " + Path.of("").toAbsolutePath());

        var measurePath = repoRoot.resolve("DotNet/Automation/measures/NHSNAcuteCareHospitalDailyInitialPopulation.json");
        var dataDir = repoRoot.resolve("artifacts/daily-ach-eval");
        Assumptions.assumeTrue(Files.exists(dataDir), "Skipping: local FHIR fixtures not present at " + dataDir);

        var measurePackage = parser.parseResource(Bundle.class, Files.readString(measurePath));
        var evaluator = MeasureEvaluator.compile(fhirContext, measurePackage, false);

        var data = new Bundle();
        data.setType(Bundle.BundleType.COLLECTION);
        try (var files = Files.list(dataDir)) {
            files.filter(p -> p.toString().endsWith(".json")).forEach(p -> {
                try {
                    data.addEntry().setResource(parser.parseResource(Resource.class, Files.readString(p)));
                } catch (Exception e) {
                    throw new RuntimeException(p.toString(), e);
                }
            });
        }

        var report = evaluator.evaluate(
                new DateTimeType("2023-01-15T00:00:00Z"),
                new DateTimeType("2023-01-15T23:59:59Z"),
                new StringType("Patient/Patient-ea70b91a-001"),
                data);

        var pops = report.getGroupFirstRep().getPopulation();
        var dump = new StringBuilder();
        dump.append("groupCount=").append(report.getGroup().size()).append('\n');
        for (var pop : pops) {
            var coding = pop.getCode().getCodingFirstRep();
            dump.append("pop system=").append(coding.getSystem())
                    .append(" code=").append(coding.getCode())
                    .append(" count=").append(pop.getCount())
                    .append('\n');
        }
        dump.append("evaluated=").append(report.getEvaluatedResource().size()).append('\n');
        dump.append("reportable=").append(new IsInInitialPopulation().test(report)).append('\n');

        Assertions.assertFalse(pops.isEmpty(), dump.toString());
        var ip = pops.stream()
                .filter(p -> "initial-population".equals(p.getCode().getCodingFirstRep().getCode()))
                .findFirst()
                .orElseThrow(() -> new AssertionError("No initial-population:\n" + dump));
        Assertions.assertEquals(1, ip.getCount(), dump.toString());
        Assertions.assertTrue(new IsInInitialPopulation().test(report), dump.toString());
    }
}
