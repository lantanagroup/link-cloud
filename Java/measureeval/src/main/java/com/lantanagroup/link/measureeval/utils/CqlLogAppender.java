package com.lantanagroup.link.measureeval.utils;

import ch.qos.logback.classic.Level;
import ch.qos.logback.classic.LoggerContext;
import ch.qos.logback.classic.spi.ILoggingEvent;
import ch.qos.logback.core.AppenderBase;
import com.lantanagroup.link.measureeval.services.LibraryResolver;
import javassist.NotFoundException;
import org.hl7.fhir.r4.model.Library;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;

import java.util.HashMap;
import java.util.Map;
import java.util.regex.Matcher;
import java.util.regex.Pattern;

public class CqlLogAppender extends AppenderBase<ILoggingEvent> {
    private static final Logger logger = LoggerFactory.getLogger(CqlLogAppender.class);
    private static final Pattern LOG_PATTERN = Pattern.compile("([\\w.]+)\\.(\\d+:\\d+-\\d+:\\d+)\\(\\d+\\):\\s*(\\{\\}|[^\\s]+)");

    private final LibraryResolver libraryResolver;

    public CqlLogAppender(LibraryResolver libraryResolver) {
        this.libraryResolver = libraryResolver;
    }

    public static CqlLogAppender start(LoggerContext context, LibraryResolver libraryResolver) {
        CqlLogAppender appender = new CqlLogAppender(libraryResolver);
        appender.setContext(context);
        appender.start();
        ch.qos.logback.classic.Logger logger = (ch.qos.logback.classic.Logger) LoggerFactory.getLogger("org.opencds.cqf.cql.engine.debug.DebugUtilities");
        logger.setLevel(Level.DEBUG);
        logger.setAdditive(false);
        logger.addAppender(appender);
        return appender;
    }

    @Override
    protected void append(ILoggingEvent event) {
        String message = event.getFormattedMessage();
        Matcher matcher = LOG_PATTERN.matcher(message);

        if (matcher.find()) {
            String libraryId = matcher.group(1);
            String range = matcher.group(2);
            String output = matcher.group(3)
                    .replaceAll("org.hl7.fhir.r4.model.", "")
                    .replaceAll("@[0-9A-Fa-f]{6,8}", "");
            String cql = null;

            // Group the resources in the output
            output = groupResources(output);

            Library library = libraryResolver.resolve(libraryId);
            if (library == null) {
                logger.warn("Failed to resolve library: {}", libraryId);
            } else {
                try {
                    cql = CqlUtils.getCql(library, range);
                } catch (NotFoundException e) {
                    logger.warn("Failed to get CQL for libraryId={}, range={}, output={}", libraryId, range, output);
                }
            }

            // Custom processing with libraryId and range
            processLogEntry(libraryId, range, output, cql);
        }
    }

    private String groupResources(String output) {
        if (output == null || output.isEmpty() || output.equals("{}") || !output.startsWith("{")) {
            return output;
        }

        output = output.substring(1, output.length() - 1);

        if (output.endsWith(",")) {
            output = output.substring(0, output.length() - 1);
        }

        String[] resources = output.split(",");
        Map<String, Integer> resourceCount = new HashMap<>();

        for (String resource : resources) {
            resourceCount.put(resource, resourceCount.getOrDefault(resource, 0) + 1);
        }

        StringBuilder groupedOutput = new StringBuilder();
        for (Map.Entry<String, Integer> entry : resourceCount.entrySet()) {
            if (groupedOutput.length() > 0) {
                groupedOutput.append(",");
            }
            groupedOutput.append(entry.getKey());
            if (entry.getValue() > 1) {
                groupedOutput.append("(").append(entry.getValue()).append(")");
            }
        }

        return groupedOutput.toString();
    }

    private void processLogEntry(String libraryId, String range, String output, String cql) {
        if (cql != null) {
            Pattern definePattern = Pattern.compile("^define \"([^\"]+)\"");
            Matcher matcher = definePattern.matcher(cql);
            if (matcher.find()) {
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