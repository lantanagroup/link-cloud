package com.lantanagroup.link.shared.utils;

import java.util.regex.Pattern;

public class LogUtils {
    private static final Pattern UNSAFE_CHARACTER = Pattern.compile("[^a-zA-Z0-9._\\- ]");

    public static String sanitize(String value) {
        return value == null ? null : UNSAFE_CHARACTER.matcher(value).replaceAll("");
    }
}
