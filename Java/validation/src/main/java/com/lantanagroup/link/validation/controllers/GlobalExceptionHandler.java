package com.lantanagroup.link.validation.controllers;

import com.lantanagroup.link.shared.utils.LogUtils;
import com.lantanagroup.link.validation.exceptions.InvalidRubricDefinitionException;
import com.lantanagroup.link.validation.exceptions.RubricDryRunRequiredException;
import com.lantanagroup.link.validation.exceptions.PayloadParseException;
import com.lantanagroup.link.validation.exceptions.RubricLifecycleException;
import com.lantanagroup.link.validation.exceptions.RubricNotFoundException;
import com.lantanagroup.link.validation.exceptions.RubricVersionConflictException;
import com.lantanagroup.link.validation.exceptions.RubricVersionNotFoundException;
import com.lantanagroup.link.validation.records.ApiResponse;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.core.Ordered;
import org.springframework.core.annotation.Order;
import org.springframework.http.HttpStatus;
import org.springframework.http.HttpStatusCode;
import org.springframework.http.ResponseEntity;
import org.springframework.http.converter.HttpMessageNotReadableException;
import org.springframework.web.ErrorResponse;
import org.springframework.web.bind.MethodArgumentNotValidException;
import org.springframework.web.bind.annotation.ExceptionHandler;
import org.springframework.web.bind.annotation.RestControllerAdvice;
import org.springframework.web.method.annotation.MethodArgumentTypeMismatchException;
import org.springframework.web.server.ResponseStatusException;

import java.util.List;

/**
 * Normalizes every handled exception across the Validation API into the uniform {@link ApiResponse}
 * error envelope: the same shape as success responses ({@code status}, {@code message},
 * {@code timestamp}), with {@code data} omitted and an optional {@code errors} array. Applies to the
 * v2 evaluation endpoints, the Rubric Registry, and the legacy endpoints on
 * {@link ValidationController} (whose {@link ResponseStatusException}s are caught and re-shaped,
 * preserving their status and using the reason as {@code message}).
 *
 * <p>Registered at {@link Ordered#HIGHEST_PRECEDENCE} so it wins ahead of Spring's problem-detail
 * advice ({@code spring.mvc.problemdetails}). The {@code timestamp} comes from
 * {@link ApiResponse#nowMillis()} (UTC, millisecond precision) — identical in source and format to
 * the success envelope.
 *
 * <p>Exception: the pre-qual report endpoint catches its own failure and returns a raw
 * {@code ProblemDetail} directly, so that 500 does not pass through this handler.
 */
@RestControllerAdvice
@Order(Ordered.HIGHEST_PRECEDENCE)
public class GlobalExceptionHandler {

    private static final Logger logger = LoggerFactory.getLogger(GlobalExceptionHandler.class);

    // --- Rubric registry domain exceptions (documented statuses, now emitted as ApiResponse) ---

    @ExceptionHandler({
            RubricNotFoundException.class,
            RubricVersionNotFoundException.class
    })
    public ResponseEntity<ApiResponse<Void>> handleNotFound(RuntimeException ex) {
        logger.warn("Rubric registry lookup failed: {}", LogUtils.sanitize(ex.getMessage()));
        return envelope(HttpStatus.NOT_FOUND, ex.getMessage(), null);
    }

    @ExceptionHandler({
            RubricVersionConflictException.class,
            RubricLifecycleException.class,
            RubricDryRunRequiredException.class
    })
    public ResponseEntity<ApiResponse<Void>> handleConflict(RuntimeException ex) {
        logger.warn("Rubric lifecycle conflict: {}", LogUtils.sanitize(ex.getMessage()));
        return envelope(HttpStatus.CONFLICT, ex.getMessage(), null);
    }

    @ExceptionHandler(InvalidRubricDefinitionException.class)
    public ResponseEntity<ApiResponse<Void>> handleInvalidDefinition(InvalidRubricDefinitionException ex) {
        logger.warn("Rejected rubric definition: {} {}",
                LogUtils.sanitize(ex.getMessage()), LogUtils.sanitize(ex.getErrors()));
        return envelope(HttpStatus.BAD_REQUEST, ex.getMessage(), ex.getErrors());
    }

    @ExceptionHandler(PayloadParseException.class)
    public ResponseEntity<ApiResponse<Void>> handlePayloadParse(PayloadParseException ex) {
        logger.warn("Rejected unparseable payload: {}", LogUtils.sanitize(ex.getMessage()));
        return envelope(HttpStatus.BAD_REQUEST, ex.getMessage(), null);
    }

    // --- Request binding / validation failures (framework), normalized to the envelope ---

    @ExceptionHandler(MethodArgumentNotValidException.class)
    public ResponseEntity<ApiResponse<Void>> handleValidation(MethodArgumentNotValidException ex) {
        List<String> errors = ex.getBindingResult().getFieldErrors().stream()
                .map(e -> e.getField() + ": " + e.getDefaultMessage())
                .toList();
        logger.warn("Rejected invalid request payload: {}", LogUtils.sanitize(errors.toString()));
        return envelope(HttpStatus.BAD_REQUEST, "Request validation failed", errors);
    }

    @ExceptionHandler(HttpMessageNotReadableException.class)
    public ResponseEntity<ApiResponse<Void>> handleNotReadable(HttpMessageNotReadableException ex) {
        logger.warn("Rejected unreadable request body: {}", LogUtils.sanitize(ex.getMessage()));
        return envelope(HttpStatus.BAD_REQUEST, "Malformed request body", null);
    }

    @ExceptionHandler(MethodArgumentTypeMismatchException.class)
    public ResponseEntity<ApiResponse<Void>> handleTypeMismatch(MethodArgumentTypeMismatchException ex) {
        String message = "Invalid value '" + ex.getValue() + "' for parameter '" + ex.getName() + "'";
        logger.warn("Rejected request parameter: {}", LogUtils.sanitize(message));
        return envelope(HttpStatus.BAD_REQUEST, message, null);
    }

    // --- Legacy ResponseStatusException, re-shaped preserving status and using the reason as message ---

    @ExceptionHandler(ResponseStatusException.class)
    public ResponseEntity<ApiResponse<Void>> handleResponseStatus(ResponseStatusException ex) {
        HttpStatusCode status = ex.getStatusCode();
        String message = ex.getReason() != null ? ex.getReason() : reasonPhrase(status);
        logger.warn("Request failed ({}): {}", status, LogUtils.sanitize(message));
        return envelope(status, message, null);
    }

    // --- Catch-all: preserve the status of framework ErrorResponses; everything else is a 500 whose
    //     internal details are logged but never returned. ---

    @ExceptionHandler(Exception.class)
    public ResponseEntity<ApiResponse<Void>> handleUnexpected(Exception ex) {
        if (ex instanceof ErrorResponse er) {
            HttpStatusCode status = er.getStatusCode();
            String message = er.getBody() != null && er.getBody().getDetail() != null
                    ? er.getBody().getDetail()
                    : reasonPhrase(status);
            logger.warn("Request failed ({}): {}", status, LogUtils.sanitize(message));
            return envelope(status, message, null);
        }
        logger.error("Unhandled exception", ex);
        return envelope(HttpStatus.INTERNAL_SERVER_ERROR, "An unexpected error occurred", null);
    }

    private static ResponseEntity<ApiResponse<Void>> envelope(HttpStatusCode status, String message, List<String> errors) {
        ApiResponse<Void> body = new ApiResponse<>(status.value(), message, ApiResponse.nowMillis(), null, errors);
        return ResponseEntity.status(status).body(body);
    }

    private static String reasonPhrase(HttpStatusCode status) {
        HttpStatus resolved = HttpStatus.resolve(status.value());
        return resolved != null ? resolved.getReasonPhrase() : "Error";
    }
}
