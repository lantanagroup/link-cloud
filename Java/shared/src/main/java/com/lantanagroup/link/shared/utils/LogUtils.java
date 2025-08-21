package com.lantanagroup.link.shared.utils;

public class LogUtils {
    public static String sanitize(String value) {
        return value == null ? null : value.replaceAll("[^a-zA-Z0-9\\-_ ]", "");
    }
}
