package com.lantanagroup.link.measureeval.utils;

import ch.qos.logback.classic.spi.ILoggingEvent;
import ch.qos.logback.core.AppenderBase;
import com.lantanagroup.link.measureeval.services.MeasureEvaluator;
import com.lantanagroup.link.measureeval.services.MeasureEvaluatorCache;
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
                    .replaceAll("@[0-9A-Fa-f]{8}", "");
            String cql = null;

            MeasureEvaluatorCache measureEvalCache = ApplicationContextProvider.getApplicationContext().getBean(MeasureEvaluatorCache.class);
            MeasureEvaluator evaluator = measureEvalCache.get(libraryId);       // Assume the measure id is the same as the library id since the log entry doesn't output the measure url/id

            if (evaluator != null) {
                cql = CqlUtils.getCql(evaluator.getBundle(), libraryId, range);
            }

            // Custom processing with libraryId and range
            processLogEntry(libraryId, range, output, cql);
        }
    }

    private void processLogEntry(String libraryId, String range, String output, String cql) {
        if (cql != null) {
            Pattern definePattern = Pattern.compile("^define \"([^\"]+)\"");
            Matcher matcher = cql != null ? definePattern.matcher(cql) : null;
            if (matcher != null && matcher.find()) {
                String definition = matcher.group(1);
                logger.info("CQL DEBUG: libraryId={}, range={}, output={}, cql-definition={}", libraryId, range, output, definition);
            } else {
                logger.info("CQL DEBUG: libraryId={}, range={}, output={}, cql=\n{}", libraryId, range, output, cql);
            }
        } else {
            logger.info("CQL DEBUG: libraryId={}, range={}, output={}", libraryId, range, output);
        }
    }
}