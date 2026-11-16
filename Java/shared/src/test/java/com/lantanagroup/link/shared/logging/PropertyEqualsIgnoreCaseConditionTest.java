package com.lantanagroup.link.shared.logging;

import ch.qos.logback.classic.Logger;
import ch.qos.logback.classic.LoggerContext;
import ch.qos.logback.classic.joran.JoranConfigurator;
import ch.qos.logback.core.joran.spi.JoranException;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.params.ParameterizedTest;
import org.junit.jupiter.params.provider.ValueSource;

import java.io.ByteArrayInputStream;
import java.nio.charset.StandardCharsets;

import static org.junit.jupiter.api.Assertions.assertFalse;
import static org.junit.jupiter.api.Assertions.assertNotNull;
import static org.junit.jupiter.api.Assertions.assertNull;

/**
 * Runs the condition through logback's own configurator, the way both services' logback-spring.xml use it
 * to decide whether the Loki appender is added.
 */
class PropertyEqualsIgnoreCaseConditionTest {

    private static final String CONDITION_AND_BRANCHES =
            "  <condition class=\"" + PropertyEqualsIgnoreCaseCondition.class.getName() + "\">\n" +
            "    <key>loki.enabled</key>\n" +
            "    <value>true</value>\n" +
            "  </condition>\n" +
            "  <if>\n" +
            "    <then>\n" +
            "      <appender name=\"ENABLED\" class=\"ch.qos.logback.core.read.ListAppender\"/>\n" +
            "      <root level=\"INFO\"><appender-ref ref=\"ENABLED\"/></root>\n" +
            "    </then>\n" +
            "    <else>\n" +
            "      <appender name=\"DISABLED\" class=\"ch.qos.logback.core.read.ListAppender\"/>\n" +
            "      <root level=\"INFO\"><appender-ref ref=\"DISABLED\"/></root>\n" +
            "    </else>\n" +
            "  </if>\n";

    @ParameterizedTest
    @ValueSource(strings = {"true", "TRUE", "True"})
    void evaluate_TrueInAnyCase_TakesThenBranch(String enabled) throws JoranException {
        var root = configure("  <property name=\"loki.enabled\" value=\"" + enabled + "\"/>\n");

        assertNotNull(root.getAppender("ENABLED"));
        assertNull(root.getAppender("DISABLED"));
    }

    @ParameterizedTest
    @ValueSource(strings = {"false", "yes", "1"})
    void evaluate_OtherValue_TakesElseBranch(String enabled) throws JoranException {
        var root = configure("  <property name=\"loki.enabled\" value=\"" + enabled + "\"/>\n");

        assertNull(root.getAppender("ENABLED"));
        assertNotNull(root.getAppender("DISABLED"));
    }

    @Test
    void evaluate_PropertyNotSet_TakesElseBranch() throws JoranException {
        var root = configure("");

        assertNull(root.getAppender("ENABLED"));
        assertNotNull(root.getAppender("DISABLED"));
    }

    @Test
    void evaluate_NoValueConfigured_IsFalse() {
        var condition = new PropertyEqualsIgnoreCaseCondition();
        condition.setKey("loki.enabled");

        assertFalse(condition.evaluate());
    }

    private static Logger configure(String propertyLine) throws JoranException {
        var xml = "<configuration>\n" + propertyLine + CONDITION_AND_BRANCHES + "</configuration>\n";
        var context = new LoggerContext();
        var configurator = new JoranConfigurator();
        configurator.setContext(context);
        configurator.doConfigure(new ByteArrayInputStream(xml.getBytes(StandardCharsets.UTF_8)));
        return context.getLogger(Logger.ROOT_LOGGER_NAME);
    }
}
