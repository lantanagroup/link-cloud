package com.lantanagroup.link.validation.controllers;

import com.lantanagroup.link.shared.utils.LogUtils;
import com.lantanagroup.link.validation.exceptions.InvalidRubricDefinitionException;
import com.lantanagroup.link.validation.exceptions.PayloadParseException;
import com.lantanagroup.link.validation.exceptions.RubricDryRunRequiredException;
import com.lantanagroup.link.validation.exceptions.RubricLifecycleException;
import com.lantanagroup.link.validation.exceptions.RubricNotFoundException;
import com.lantanagroup.link.validation.exceptions.RubricVersionConflictException;
import com.lantanagroup.link.validation.exceptions.RubricVersionNotFoundException;
import com.lantanagroup.link.validation.records.ApiResponse;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.beans.TypeMismatchException;
import org.springframework.core.Ordered;
import org.springframework.core.annotation.Order;
import org.springframework.http.HttpHeaders;
import org.springframework.http.HttpStatus;
import org.springframework.http.HttpStatusCode;
import org.springframework.http.ProblemDetail;
import org.springframework.http.ResponseEntity;
import org.springframework.http.converter.HttpMessageNotReadableException;
import org.springframework.web.bind.MethodArgumentNotValidException;
import org.springframework.web.bind.annotation.ExceptionHandler;
import org.springframework.web.bind.annotation.RestControllerAdvice;
import org.springframework.web.context.request.ServletWebRequest;
import org.springframework.web.context.request.WebRequest;
import org.springframework.web.method.annotation.MethodArgumentTypeMismatchException;
import org.springframework.web.servlet.mvc.method.annotation.ResponseEntityExceptionHandler;

import java.util.List;

/**
 * Normalizes exceptions from the <b>rubric-governance APIs</b> into the uniform {@link ApiResponse}
 * error envelope ({@code status}, {@code message}, {@code timestamp}, optional {@code errors};
 * {@code data} omitted). Governance = the Rubric Registry under {@code /api/validation/rubrics/**}
 * and the v2 evaluation endpoints under {@code /api/validation/v2/rubrics/**}.
 *
 * <p>The <b>legacy</b> validation endpoints ({@code /$validate}, {@code /$categorize}, pre-qual) are
 * deliberately left on Spring's default problem-detail handling: for the standard MVC exceptions this
 * advice delegates non-governance requests to {@link ResponseEntityExceptionHandler super}, so their
 * responses are unchanged. Governance vs legacy is decided by the request path.
 *
 * <p>Registered at {@link Ordered#HIGHEST_PRECEDENCE}. Envelope {@code timestamp} comes from
 * {@link ApiResponse#nowMillis()} (UTC, millisecond precision).
 */
@RestControllerAdvice
@Order(Ordered.HIGHEST_PRECEDENCE)
public class GlobalExceptionHandler extends ResponseEntityExceptionHandler {

    private static final Logger logger = LoggerFactory.getLogger(GlobalExceptionHandler.class);

    // --- Rubric-governance domain exceptions (these types are only ever thrown by governance paths) ---

    @ExceptionHandler({
            RubricNotFoundException.class,
            RubricVersionNotFoundException.class
    })
    public ResponseEntity<Object> handleNotFound(RuntimeException ex) {
        logger.warn("Rubric registry lookup failed: {}", LogUtils.sanitize(ex.getMessage()));
        return envelope(HttpStatus.NOT_FOUND, ex.getMessage(), null);
    }

    @ExceptionHandler({
            RubricVersionConflictException.class,
            RubricLifecycleException.class,
            RubricDryRunRequiredException.class
    })
    public ResponseEntity<Object> handleConflict(RuntimeException ex) {
        logger.warn("Rubric lifecycle conflict: {}", LogUtils.sanitize(ex.getMessage()));
        return envelope(HttpStatus.CONFLICT, ex.getMessage(), null);
    }

    @ExceptionHandler(InvalidRubricDefinitionException.class)
    public ResponseEntity<Object> handleInvalidDefinition(InvalidRubricDefinitionException ex) {
        logger.warn("Rejected rubric definition: {} {}",
                LogUtils.sanitize(ex.getMessage()), LogUtils.sanitize(ex.getErrors()));
        return envelope(HttpStatus.BAD_REQUEST, ex.getMessage(), ex.getErrors());
    }

    @ExceptionHandler(PayloadParseException.class)
    public ResponseEntity<Object> handlePayloadParse(PayloadParseException ex) {
        logger.warn("Rejected unparseable payload: {}", LogUtils.sanitize(ex.getMessage()));
        return envelope(HttpStatus.BAD_REQUEST, ex.getMessage(), null);
    }

    // --- Standard MVC exceptions: envelope for governance paths, framework default for the rest ---

    @Override
    protected ResponseEntity<Object> handleMethodArgumentNotValid(
            MethodArgumentNotValidException ex, HttpHeaders headers, HttpStatusCode status, WebRequest request) {
        if (!isGovernance(request)) {
            return super.handleMethodArgumentNotValid(ex, headers, status, request);
        }
        List<String> errors = ex.getBindingResult().getFieldErrors().stream()
                .map(e -> e.getField() + ": " + e.getDefaultMessage())
                .toList();
        logger.warn("Rejected invalid request payload: {}", LogUtils.sanitize(errors.toString()));
        return envelope(HttpStatus.BAD_REQUEST, "Request validation failed", errors);
    }

    @Override
    protected ResponseEntity<Object> handleHttpMessageNotReadable(
            HttpMessageNotReadableException ex, HttpHeaders headers, HttpStatusCode status, WebRequest request) {
        if (!isGovernance(request)) {
            return super.handleHttpMessageNotReadable(ex, headers, status, request);
        }
        logger.warn("Rejected unreadable request body: {}", LogUtils.sanitize(ex.getMessage()));
        return envelope(HttpStatus.BAD_REQUEST, "Malformed request body", null);
    }

    @Override
    protected ResponseEntity<Object> handleTypeMismatch(
            TypeMismatchException ex, HttpHeaders headers, HttpStatusCode status, WebRequest request) {
        if (!isGovernance(request)) {
            return super.handleTypeMismatch(ex, headers, status, request);
        }
        String name = (ex instanceof MethodArgumentTypeMismatchException m) ? m.getName() : ex.getPropertyName();
        String message = "Invalid value '" + ex.getValue() + "' for parameter '" + name + "'";
        logger.warn("Rejected request parameter: {}", LogUtils.sanitize(message));
        return envelope(HttpStatus.BAD_REQUEST, message, null);
    }

    // --- Catch-all: envelope 500 for governance; framework-style ProblemDetail otherwise. Internal
    //     details are logged but never returned. ---

    @ExceptionHandler(Exception.class)
    public ResponseEntity<Object> handleUnexpected(Exception ex, WebRequest request) {
        logger.error("Unhandled exception", ex);
        if (isGovernance(request)) {
            return envelope(HttpStatus.INTERNAL_SERVER_ERROR, "An unexpected error occurred", null);
        }
        return ResponseEntity.internalServerError()
                .body(ProblemDetail.forStatusAndDetail(HttpStatus.INTERNAL_SERVER_ERROR, "An unexpected error occurred"));
    }

    private static boolean isGovernance(WebRequest request) {
        if (request instanceof ServletWebRequest swr) {
            String path = swr.getRequest().getRequestURI();
            return path != null
                    && (path.startsWith("/api/validation/rubrics") || path.startsWith("/api/validation/v2/rubrics"));
        }
        return false;
    }

    private static ResponseEntity<Object> envelope(HttpStatusCode status, String message, List<String> errors) {
        ApiResponse<Void> body = new ApiResponse<>(status.value(), message, ApiResponse.nowMillis(), null, errors);
        return ResponseEntity.status(status).body(body);
    }
}
