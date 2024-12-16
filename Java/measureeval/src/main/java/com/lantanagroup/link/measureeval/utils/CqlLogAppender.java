package com.lantanagroup.link.measureeval.utils;

import ch.qos.logback.classic.spi.ILoggingEvent;
import ch.qos.logback.core.AppenderBase;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;

import java.util.regex.Matcher;
import java.util.regex.Pattern;

public class CqlLogAppender extends AppenderBase<ILoggingEvent> {
    private static final Logger logger = LoggerFactory.getLogger(CqlLogAppender.class);
    private static final Pattern LOG_PATTERN = Pattern.compile("([\\w.]+)\\.(\\d+:\\d+-\\d+:\\d+)\\(\\d+\\):\\s*(\\{\\}|[^\\s]+)");

    @Override
    protected void append(ILoggingEvent event) {
        String message = event.getFormattedMessage();
        Matcher matcher = LOG_PATTERN.matcher(message);

        if (matcher.find()) {
            String libraryId = matcher.group(1);
            String range = matcher.group(2);
            String output = matcher.group(3)
                    .replaceAll("org.hl7.fhir.r4.model.", "")
                    .replaceAll("@[0-9A-z]{8}", "");

            // Custom processing with libraryId and range
            processLogEntry(libraryId, range, output);
        }
    }

    private void processLogEntry(String libraryId, String range, String value) {
        // Implement your custom processing logic here
        logger.info("CQL DEBUG: libraryId={}, range={}, output={}", libraryId, range, value);
    }
}