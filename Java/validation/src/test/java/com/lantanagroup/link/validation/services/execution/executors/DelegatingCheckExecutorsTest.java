package com.lantanagroup.link.validation.services.execution.executors;

import com.lantanagroup.link.validation.entities.RubricCheck;
import com.lantanagroup.link.validation.enums.CheckType;
import com.lantanagroup.link.validation.enums.PiqiDimension;
import com.lantanagroup.link.validation.models.ExecutionContext;
import com.lantanagroup.link.validation.models.RawFinding;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;

import java.util.List;

import static org.assertj.core.api.Assertions.assertThat;
import static org.mockito.Mockito.mock;
import static org.mockito.Mockito.when;

/**
 * Completeness / Currency / Plausibility executors are thin dimension-labelled delegators to
 * {@link FhirPathCheckExecutor}. They must report their own CheckType but delegate execution verbatim.
 */
class DelegatingCheckExecutorsTest {

    private final FhirPathCheckExecutor delegate = mock(FhirPathCheckExecutor.class);
    private final RubricCheck check = RubricCheck.builder()
            .checkLocalId("d-1").dimension(PiqiDimension.COMPLETENESS).build();
    private final ExecutionContext context = new ExecutionContext();
    private final List<RawFinding> delegateResult = List.of(RawFinding.builder().code("delegated").build());

    @Test
    @DisplayName("CompletenessCheckExecutor reports COMPLETENESS and delegates to FhirPathCheckExecutor")
    void completenessDelegates() {
        when(delegate.execute(check, context)).thenReturn(delegateResult);
        CompletenessCheckExecutor executor = new CompletenessCheckExecutor(delegate);

        assertThat(executor.supports()).isEqualTo(CheckType.COMPLETENESS);
        assertThat(executor.execute(check, context)).isSameAs(delegateResult);
    }

    @Test
    @DisplayName("CurrencyCheckExecutor reports CURRENCY and delegates to FhirPathCheckExecutor")
    void currencyDelegates() {
        when(delegate.execute(check, context)).thenReturn(delegateResult);
        CurrencyCheckExecutor executor = new CurrencyCheckExecutor(delegate);

        assertThat(executor.supports()).isEqualTo(CheckType.CURRENCY);
        assertThat(executor.execute(check, context)).isSameAs(delegateResult);
    }

    @Test
    @DisplayName("PlausibilityCheckExecutor reports PLAUSIBILITY and delegates to FhirPathCheckExecutor")
    void plausibilityDelegates() {
        when(delegate.execute(check, context)).thenReturn(delegateResult);
        PlausibilityCheckExecutor executor = new PlausibilityCheckExecutor(delegate);

        assertThat(executor.supports()).isEqualTo(CheckType.PLAUSIBILITY);
        assertThat(executor.execute(check, context)).isSameAs(delegateResult);
    }
}
