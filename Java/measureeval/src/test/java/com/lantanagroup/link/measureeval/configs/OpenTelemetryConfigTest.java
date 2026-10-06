package com.lantanagroup.link.measureeval.configs;

import com.lantanagroup.link.shared.utils.DiagnosticNames;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.params.ParameterizedTest;
import org.junit.jupiter.params.provider.NullAndEmptySource;
import org.junit.jupiter.params.provider.ValueSource;

import static io.opentelemetry.api.common.AttributeKey.stringKey;
import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertFalse;
import static org.junit.jupiter.api.Assertions.assertNotEquals;
import static org.junit.jupiter.api.Assertions.assertNull;

/**
 * The test environments share one Prometheus. The environment attribute is what tells their series
 * apart, and the instance id is what keeps two processes from writing to the same series.
 */
class OpenTelemetryConfigTest {

    @Test
    void buildResource_environmentSet_addsDeploymentEnvironmentName() {
        var resource = OpenTelemetryConfig.buildResource("measureeval", "qa");

        assertEquals("qa", resource.getAttribute(stringKey(DiagnosticNames.DEPLOYMENT_ENVIRONMENT_NAME)));
    }

    @ParameterizedTest
    @NullAndEmptySource
    @ValueSource(strings = {"  "})
    void buildResource_environmentBlank_omitsTheAttribute(String deploymentEnvironment) {
        var resource = OpenTelemetryConfig.buildResource("measureeval", deploymentEnvironment);

        assertNull(resource.getAttribute(stringKey(DiagnosticNames.DEPLOYMENT_ENVIRONMENT_NAME)));
    }

    @Test
    void buildResource_eachProcess_getsItsOwnInstanceId() {
        var first = OpenTelemetryConfig.buildResource("measureeval", "qa")
                .getAttribute(stringKey(OpenTelemetryConfig.SERVICE_INSTANCE_ID));
        var second = OpenTelemetryConfig.buildResource("measureeval", "qa")
                .getAttribute(stringKey(OpenTelemetryConfig.SERVICE_INSTANCE_ID));

        assertFalse(first == null || first.isBlank(), "every process carries an instance id");
        assertNotEquals(first, second);
    }

    @Test
    void buildResource_anyEnvironment_namesTheService() {
        var resource = OpenTelemetryConfig.buildResource("measureeval", "qa");

        assertEquals("measureeval", resource.getAttribute(stringKey("service.name")));
    }
}
