package com.lantanagroup.link.measureeval.utils;

import java.util.Map;
import java.util.function.Function;
import java.util.stream.Collectors;

public class StreamUtils {
    public static <K1, K2, V> Map<K2, V> mapKeys(Map<K1, V> map, Function<K1, K2> mapper) {
        return map.entrySet().stream().collect(Collectors.toMap(
                entry -> mapper.apply(entry.getKey()),
                Map.Entry::getValue));
    }

    public static <T> T toOnlyElement(T element1, T element2) {
        throw new IllegalStateException("Expected at most one element but found multiple");
    }
}
