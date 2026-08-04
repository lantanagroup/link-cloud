package com.lantanagroup.link.validation.services.execution.spi;

import com.lantanagroup.link.validation.entities.RubricCheck;
import com.lantanagroup.link.validation.models.ExecutionContext;
import com.lantanagroup.link.validation.models.RawFinding;

import java.util.List;

public interface CustomCheck {

    String id();

    List<RawFinding> run(RubricCheck check, ExecutionContext context);
}
