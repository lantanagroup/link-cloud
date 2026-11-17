package com.lantanagroup.link.validation.records;

import org.springframework.data.domain.Page;

import java.util.List;
import java.util.function.Function;

public record PagedData<T>(List<T> content, int page, int size, long totalElements, int totalPages) {

    public static <E, T> PagedData<T> from(Page<E> page, Function<E, T> mapper) {
        return new PagedData<>(
                page.getContent().stream().map(mapper).toList(),
                page.getNumber(),
                page.getSize(),
                page.getTotalElements(),
                page.getTotalPages());
    }
}
