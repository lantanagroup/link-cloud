package com.lantanagroup.link.shared.utils;

public class StringUtils {
    public static String safe(String v) {
        String s = LogUtils.sanitize(v);
        return (s == null) ? "" : s;
    }
}
