package com.lantanagroup.link.validation.services.execution;

import com.lantanagroup.link.validation.entities.RubricCheck;
import com.lantanagroup.link.validation.enums.CheckType;
import com.lantanagroup.link.validation.models.ExecutionContext;
import com.lantanagroup.link.validation.models.RawFinding;

import java.util.List;

public interface CheckExecutor {

    CheckType supports();

    List<RawFinding> execute(RubricCheck check, ExecutionContext context);
}
