package com.lantanagroup.link.shared.kafka;

import org.apache.kafka.common.header.Header;

import java.nio.charset.Charset;
import java.nio.charset.StandardCharsets;

public class Headers {
    private static final Charset CHARSET = StandardCharsets.UTF_8;

    public static final String CORRELATION_ID = "X-Correlation-Id";
    public static final String METRICS_MODE = "X-Metrics-Mode";
    public static final String EXCEPTION_FACILITY_ID = "X-Exception-Facility-Id";
    public static final String EXCEPTION_MESSAGE = "X-Exception-Message";
    public static final String EXCEPTION_SERVICE = "X-Exception-Service";
    public static final String RETRY_COUNT = "X-Retry-Count";
    public static final String QUERY_TYPE = "X-Query-Type";

    public static String getString(byte[] bytes) {
        return new String(bytes, CHARSET);
    }

    public static byte[] getBytes(String string) {
        return string.getBytes(CHARSET);
    }

    public static String getCorrelationId(org.apache.kafka.common.header.Headers headers) {
        Header header = headers.lastHeader(CORRELATION_ID);
        return header == null ? null : getString(header.value());
    }

    public static String getQueryType(org.apache.kafka.common.header.Headers headers) {
        Header header = headers.lastHeader(QUERY_TYPE);
        return header == null ? null : getString(header.value());
    }

    public static String getMetricsMode(org.apache.kafka.common.header.Headers headers) {
        if (headers == null) {
            return null;
        }
        Header header = headers.lastHeader(METRICS_MODE);
        return header == null ? null : getString(header.value());
    }

    public static void copyMetricsMode(org.apache.kafka.common.header.Headers source,
                                       org.apache.kafka.common.header.Headers destination) {
        String mode = getMetricsMode(source);
        if (mode != null && !mode.isBlank() && destination != null) {
            destination.remove(METRICS_MODE);
            destination.add(METRICS_MODE, getBytes(mode));
        }
    }
}
