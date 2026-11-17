package com.lantanagroup.link.validation.services.execution.executors;

import com.lantanagroup.link.validation.entities.RubricCheck;
import com.lantanagroup.link.validation.enums.CheckType;
import com.lantanagroup.link.validation.models.ExecutionContext;
import com.lantanagroup.link.validation.models.RawFinding;
import com.lantanagroup.link.validation.services.execution.CheckExecutor;
import lombok.RequiredArgsConstructor;
import org.springframework.stereotype.Component;

import java.util.List;

@Component
@RequiredArgsConstructor
public class CurrencyCheckExecutor implements CheckExecutor {

    private final FhirPathCheckExecutor delegate;

    @Override
    public CheckType supports() {
        return CheckType.CURRENCY;
    }

    @Override
    public List<RawFinding> execute(RubricCheck check, ExecutionContext context) {
        return delegate.execute(check, context);
    }
}
