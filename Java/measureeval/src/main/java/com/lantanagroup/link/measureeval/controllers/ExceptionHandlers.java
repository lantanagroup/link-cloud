package com.lantanagroup.link.measureeval.controllers;

import com.lantanagroup.link.shared.exceptions.FhirParseException;
import com.lantanagroup.link.shared.exceptions.ValidationException;
import jakarta.servlet.http.HttpServletRequest;
import org.apache.commons.collections4.CollectionUtils;
import org.springframework.http.HttpStatus;
import org.springframework.http.HttpStatusCode;
import org.springframework.http.ResponseEntity;
import org.springframework.web.bind.annotation.ExceptionHandler;
import org.springframework.web.bind.annotation.RestControllerAdvice;
import org.springframework.web.server.ResponseStatusException;

import java.time.OffsetDateTime;
import java.util.Collection;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;

@RestControllerAdvice
public class ExceptionHandlers {
    private ResponseEntity<Map<String, Object>> handle(
            HttpServletRequest request,
            HttpStatus status,
            Collection<String> messages) {
        if (status == null) {
            status = HttpStatus.INTERNAL_SERVER_ERROR;
        }
        Map<String, Object> body = new LinkedHashMap<>();
        body.put("timestamp", OffsetDateTime.now());
        body.put("status", status.value());
        body.put("reason", status.getReasonPhrase());
        if (CollectionUtils.isNotEmpty(messages)) {
            body.put("messages", messages);
        }
        body.put("path", request.getRequestURI());
        return new ResponseEntity<>(body, status);
    }

    private ResponseEntity<Map<String, Object>> handle(
            HttpServletRequest request,
            HttpStatus status,
            String message) {
        return handle(request, status, message == null ? null : List.of(message));
    }

    @ExceptionHandler
    public ResponseEntity<Map<String, Object>> handle(HttpServletRequest request, FhirParseException exception) {
        return handle(request, HttpStatus.BAD_REQUEST, exception.getMessage());
    }

    @ExceptionHandler
    public ResponseEntity<Map<String, Object>> handle(HttpServletRequest request, ValidationException exception) {
        return handle(request, HttpStatus.BAD_REQUEST, exception.getErrors());
    }

    @ExceptionHandler
    public ResponseEntity<Map<String, Object>> handle(HttpServletRequest request, ResponseStatusException exception) {
        HttpStatusCode statusCode = exception.getStatusCode();
        HttpStatus status = HttpStatus.resolve(statusCode.value());
        String message = statusCode.is4xxClientError() ? exception.getReason() : null;
        return handle(request, status, message);
    }

}
