package com.lantanagroup.link.shared.kafka;

import java.nio.charset.Charset;
import java.nio.charset.StandardCharsets;

public class Headers {
    private static final Charset CHARSET = StandardCharsets.UTF_8;

    public static final String CORRELATION_ID = "X-Correlation-Id";
    public static final String EXCEPTION_FACILITY_ID = "X-Exception-Facility-Id";
    public static final String EXCEPTION_MESSAGE = "X-Exception-Message";
    public static final String EXCEPTION_SERVICE = "X-Exception-Service";
    public static final String RETRY_COUNT = "X-Retry-Count";
    public static final String REPORT_TRACKING_ID = "X-Report-Tracking-Id";

    public static String getString(byte[] bytes) {
        return new String(bytes, CHARSET);
    }

    public static byte[] getBytes(String string) {
        return string.getBytes(CHARSET);
    }
}
