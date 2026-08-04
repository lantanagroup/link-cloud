package com.lantanagroup.link.validation.records;

import com.fasterxml.jackson.annotation.JsonInclude;
import org.springframework.http.HttpStatus;

import java.time.Instant;
import java.util.List;

// uniform envelope for the v2 API;
@JsonInclude(JsonInclude.Include.NON_NULL)
public record ApiResponse<T>(int status, String message, Instant timestamp, T data, List<String> errors) {

    public static <T> ApiResponse<T> ok(String message, T data) {
        return new ApiResponse<>(HttpStatus.OK.value(), message, Instant.now(), data, null);
    }

    public static <T> ApiResponse<T> created(String message, T data) {
        return new ApiResponse<>(HttpStatus.CREATED.value(), message, Instant.now(), data, null);
    }

    public static <T> ApiResponse<T> error(HttpStatus status, String message, List<String> errors) {
        return new ApiResponse<>(status.value(), message, Instant.now(), null, errors);
    }
}
