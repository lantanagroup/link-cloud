package com.lantanagroup.link.shared.kafka;

import org.apache.kafka.common.header.Header;

import java.nio.charset.Charset;
import java.nio.charset.StandardCharsets;

public class Headers {
    private static final Charset CHARSET = StandardCharsets.UTF_8;

    public static final String CORRELATION_ID = "X-Correlation-Id";
    public static final String EXCEPTION_FACILITY_ID = "X-Exception-Facility-Id";
    public static final String EXCEPTION_MESSAGE = "X-Exception-Message";
    public static final String EXCEPTION_SERVICE = "X-Exception-Service";
    public static final String RETRY_COUNT = "X-Retry-Count";

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
}
