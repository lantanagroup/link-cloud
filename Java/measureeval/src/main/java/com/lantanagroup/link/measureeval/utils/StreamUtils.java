package com.lantanagroup.link.measureeval.utils;

public class StreamUtils {
    public static <T> T toOnlyElement(T element1, T element2) {
        throw new IllegalStateException("Expected at most one element but found multiple");
    }
}
