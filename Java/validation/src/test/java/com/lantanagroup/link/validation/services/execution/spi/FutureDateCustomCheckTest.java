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
