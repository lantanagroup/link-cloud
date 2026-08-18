package com.lantanagroup.link.shared.utils;

public class DateUtils {
    public static String safeDate(Object date) {
        return (date == null) ? "" : date.toString();
    }
}
