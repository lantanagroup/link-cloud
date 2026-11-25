package com.lantanagroup.link.shared.audit;

import io.opentelemetry.api.OpenTelemetry;
import io.opentelemetry.api.common.AttributeKey;
import io.opentelemetry.api.common.Attributes;
import io.opentelemetry.api.metrics.LongCounter;
import io.opentelemetry.api.metrics.LongCounterBuilder;
import io.opentelemetry.api.metrics.Meter;
import org.mockito.ArgumentCaptor;

import java.util.List;

import static org.mockito.ArgumentMatchers.anyString;
import static org.mockito.Mockito.atLeast;
import static org.mockito.Mockito.mock;
import static org.mockito.Mockito.verify;
import static org.mockito.Mockito.when;

/**
 * A stand-in for OpenTelemetry that remembers what the one counter built from it was asked to add, so a test
 * can assert on the {@code audit_publish_failed} counter without a metrics SDK.
 */
final class RecordedCounter {
    private static final AttributeKey<String> RESOURCE = AttributeKey.stringKey("resource");

    private final OpenTelemetry openTelemetry = mock(OpenTelemetry.class);
    private final Meter meter = mock(Meter.class);
    private final LongCounterBuilder builder = mock(LongCounterBuilder.class);
    private final LongCounter counter = mock(LongCounter.class);

    RecordedCounter() {
        when(openTelemetry.getMeter(anyString())).thenReturn(meter);
        when(meter.counterBuilder(anyString())).thenReturn(builder);
        when(builder.build()).thenReturn(counter);
    }

    /** What the code under test is given in place of the service's OpenTelemetry. */
    OpenTelemetry openTelemetry() {
        return openTelemetry;
    }

    /** The name of the meter the counter was built from; fails unless exactly one was asked for. */
    String meterName() {
        ArgumentCaptor<String> name = ArgumentCaptor.forClass(String.class);
        verify(openTelemetry).getMeter(name.capture());
        return name.getValue();
    }

    /** The counter's name; fails unless exactly one counter was built. */
    String counterName() {
        ArgumentCaptor<String> name = ArgumentCaptor.forClass(String.class);
        verify(meter).counterBuilder(name.capture());
        return name.getValue();
    }

    /** Everything added to the counter, whatever the resource. */
    long total() {
        ArgumentCaptor<Long> values = ArgumentCaptor.forClass(Long.class);
        verify(counter, atLeast(0)).add(values.capture(), ArgumentCaptor.forClass(Attributes.class).capture());
        return values.getAllValues().stream().mapToLong(Long::longValue).sum();
    }

    /** Everything added to the counter for one resource. */
    long total(String resource) {
        ArgumentCaptor<Long> values = ArgumentCaptor.forClass(Long.class);
        ArgumentCaptor<Attributes> attributes = ArgumentCaptor.forClass(Attributes.class);
        verify(counter, atLeast(0)).add(values.capture(), attributes.capture());
        long total = 0;
        for (int i = 0; i < values.getAllValues().size(); i++) {
            if (resource.equals(attributes.getAllValues().get(i).get(RESOURCE))) {
                total += values.getAllValues().get(i);
            }
        }
        return total;
    }

    /** The resource attribute of each addition, in the order they were made. */
    List<String> resources() {
        ArgumentCaptor<Attributes> attributes = ArgumentCaptor.forClass(Attributes.class);
        verify(counter, atLeast(0)).add(ArgumentCaptor.forClass(Long.class).capture(), attributes.capture());
        return attributes.getAllValues().stream().map(a -> a.get(RESOURCE)).toList();
    }
}
