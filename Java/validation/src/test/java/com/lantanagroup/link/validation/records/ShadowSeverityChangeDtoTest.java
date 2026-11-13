package com.lantanagroup.link.validation.records;

import com.lantanagroup.link.validation.entities.Result;
import com.lantanagroup.link.validation.services.shadow.ResultDiff;
import org.hl7.fhir.r4.model.OperationOutcome;
import org.junit.jupiter.api.Test;

import static org.assertj.core.api.Assertions.assertThat;

class ShadowSeverityChangeDtoTest {

    private static Result result(OperationOutcome.IssueSeverity severity, String message) {
        Result result = new Result();
        result.setSeverity(severity);
        result.setMessage(message);
        result.setExpression("Patient.name[0]");
        return result;
    }

    @Test
    void fromMapsBothSidesOfTheSeverityChangeThroughShadowFindingDto() {
        Result legacy = result(OperationOutcome.IssueSeverity.ERROR, "legacy message");
        Result modern = result(OperationOutcome.IssueSeverity.WARNING, "modern message");

        ShadowSeverityChangeDto dto = ShadowSeverityChangeDto.from(new ResultDiff.SeverityChange(legacy, modern));

        assertThat(dto.getLegacy().getSeverity()).isEqualTo(OperationOutcome.IssueSeverity.ERROR);
        assertThat(dto.getLegacy().getMessage()).isEqualTo("legacy message");
        assertThat(dto.getModern().getSeverity()).isEqualTo(OperationOutcome.IssueSeverity.WARNING);
        assertThat(dto.getModern().getMessage()).isEqualTo("modern message");
    }
}
