package com.lantanagroup.link.validation.services.execution.spi;

import ca.uhn.fhir.context.FhirContext;
import ca.uhn.fhir.fhirpath.IFhirPath;
import com.fasterxml.jackson.databind.ObjectMapper;
import com.lantanagroup.link.validation.entities.RubricCheck;
import com.lantanagroup.link.validation.enums.PiqiDimension;
import com.lantanagroup.link.validation.enums.Severity;
import com.lantanagroup.link.validation.models.ExecutionContext;
import com.lantanagroup.link.validation.models.RawFinding;
import org.hl7.fhir.r4.model.DateTimeType;
import org.hl7.fhir.r4.model.DateType;
import org.hl7.fhir.r4.model.Observation;
import org.hl7.fhir.r4.model.Patient;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;

import java.time.Instant;
import java.time.LocalDate;
import java.time.ZoneOffset;
import java.time.temporal.ChronoUnit;
import java.util.Date;
import java.util.List;
import java.util.TimeZone;

import static org.assertj.core.api.Assertions.assertThat;

class FutureDateCustomCheckTest {

    private static final FhirContext FHIR_CONTEXT = FhirContext.forR4();
    private static final IFhirPath FHIR_PATH = FHIR_CONTEXT.newFhirPath();

    private final FutureDateCustomCheck check = new FutureDateCustomCheck(FHIR_PATH, new ObjectMapper());

    private static RubricCheck rubricCheck(String path) {
        return RubricCheck.builder()
                .checkLocalId("future-date-1")
                .dimension(PiqiDimension.PLAUSIBILITY)
                .parametersJson("{\"path\":\"" + path + "\"}")
                .build();
    }

    private static ExecutionContext context(org.hl7.fhir.instance.model.api.IBaseResource resource) {
        return ExecutionContext.builder().resource(resource).build();
    }

    @Test
    @DisplayName("a date equal to today (UTC) is not flagged")
    void sameDayDateIsNotFlagged() {
        Patient patient = new Patient();
        patient.setBirthDateElement(new DateType(LocalDate.now(ZoneOffset.UTC).toString()));

        List<RawFinding> findings = check.run(rubricCheck("Patient.birthDate"), context(patient));

        assertThat(findings).isEmpty();
    }

    @Test
    @DisplayName("a date after today (UTC) is flagged")
    void futureDateIsFlagged() {
        Patient patient = new Patient();
        // +2 days so the result holds regardless of the JVM's zone offset from UTC
        patient.setBirthDateElement(new DateType(LocalDate.now(ZoneOffset.UTC).plusDays(2).toString()));

        List<RawFinding> findings = check.run(rubricCheck("Patient.birthDate"), context(patient));

        assertThat(findings).hasSize(1);
        assertThat(findings.get(0).getCode()).isEqualTo("future-date");
        assertThat(findings.get(0).getSeverity()).isEqualTo(Severity.WARNING);
    }

    @Test
    @DisplayName("a future date is flagged even when the JVM zone is east of UTC")
    void futureDateFlaggedWithJvmZoneEastOfUtc() {
        TimeZone original = TimeZone.getDefault();
        try {
            TimeZone.setDefault(TimeZone.getTimeZone("Etc/GMT-14"));
            Patient patient = new Patient();
            patient.setBirthDateElement(new DateType(LocalDate.now(ZoneOffset.UTC).plusDays(1).toString()));

            List<RawFinding> findings = check.run(rubricCheck("Patient.birthDate"), context(patient));

            assertThat(findings).hasSize(1);
        } finally {
            TimeZone.setDefault(original);
        }
    }

    @Test
    @DisplayName("malformed parameters JSON -> no findings")
    void malformedParametersJson() {
        RubricCheck malformed = RubricCheck.builder()
                .checkLocalId("future-date-1")
                .dimension(PiqiDimension.PLAUSIBILITY)
                .parametersJson("{not json")
                .build();

        assertThat(check.run(malformed, context(new Patient()))).isEmpty();
    }

    @Test
    @DisplayName("missing 'path' parameter -> no findings")
    void missingPathParameter() {
        RubricCheck noPath = RubricCheck.builder()
                .checkLocalId("future-date-1")
                .dimension(PiqiDimension.PLAUSIBILITY)
                .parametersJson("{}")
                .build();

        assertThat(check.run(noPath, context(new Patient()))).isEmpty();
    }

    @Test
    @DisplayName("invalid FHIRPath expression is skipped without throwing")
    void invalidFhirPathIgnored() {
        Patient patient = new Patient();
        patient.setBirthDateElement(new DateType(LocalDate.now(ZoneOffset.UTC).plusDays(2).toString()));

        assertThat(check.run(rubricCheck("!!!not a path!!!"), context(patient))).isEmpty();
    }

    @Test
    @DisplayName("bundle entries are scanned instead of the resource when present")
    void bundleEntriesTakePrecedence() {
        Patient future = new Patient();
        future.setBirthDateElement(new DateType(LocalDate.now(ZoneOffset.UTC).plusDays(2).toString()));
        Patient current = new Patient();
        current.setBirthDateElement(new DateType(LocalDate.now(ZoneOffset.UTC).toString()));
        Patient resourceAlsoFuture = new Patient();
        resourceAlsoFuture.setBirthDateElement(new DateType(LocalDate.now(ZoneOffset.UTC).plusDays(2).toString()));

        ExecutionContext context = ExecutionContext.builder()
                .resource(resourceAlsoFuture)
                .bundleEntries(List.of(future, current))
                .build();

        // one finding from the bundle entries; the resource is not scanned as well
        assertThat(check.run(rubricCheck("Patient.birthDate"), context)).hasSize(1);
    }

    @Test
    @DisplayName("context with neither resource nor bundle entries -> no findings, no NPE")
    void noTargetsReturnsEmpty() {
        List<RawFinding> findings = check.run(rubricCheck("Patient.birthDate"), new ExecutionContext());

        assertThat(findings).isEmpty();
    }

    @Test
    @DisplayName("a dateTime after now is flagged")
    void futureDateTimeIsFlagged() {
        Observation observation = new Observation();
        observation.setEffective(new DateTimeType(Date.from(Instant.now().plus(1, ChronoUnit.DAYS))));

        List<RawFinding> findings = check.run(rubricCheck("Observation.effective"), context(observation));

        assertThat(findings).hasSize(1);
        assertThat(findings.get(0).getCode()).isEqualTo("future-date");
    }
}
