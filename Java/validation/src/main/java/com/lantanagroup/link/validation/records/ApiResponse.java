package com.lantanagroup.link.validation.records;

import com.fasterxml.jackson.annotation.JsonInclude;
import org.springframework.http.HttpStatus;

import java.time.Instant;
import java.time.temporal.ChronoUnit;
import java.util.List;

// uniform envelope for the v2 API;
@JsonInclude(JsonInclude.Include.NON_NULL)
public record ApiResponse<T>(int status, String message, Instant timestamp, T data, List<String> errors) {

    public static <T> ApiResponse<T> ok(String message, T data) {
        return new ApiResponse<>(HttpStatus.OK.value(), message, nowMillis(), data, null);
    }

    public static <T> ApiResponse<T> created(String message, T data) {
        return new ApiResponse<>(HttpStatus.CREATED.value(), message, nowMillis(), data, null);
    }

    public static <T> ApiResponse<T> error(HttpStatus status, String message, List<String> errors) {
        return new ApiResponse<>(status.value(), message, nowMillis(), null, errors);
    }

    /**
     * Response "produced-at" instant, truncated to millisecond precision so every envelope
     * serializes as ISO-8601 UTC with a fixed 3-digit fraction (e.g. {@code 2026-07-22T10:15:30.184Z}),
     * matching the documented format. Shared by the success factories and the error handler.
     */
    public static Instant nowMillis() {
        return Instant.now().truncatedTo(ChronoUnit.MILLIS);
    }
}
