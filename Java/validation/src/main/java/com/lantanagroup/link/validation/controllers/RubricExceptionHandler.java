package com.lantanagroup.link.validation.controllers;

import com.lantanagroup.link.validation.exceptions.FacilityOverrideNotFoundException;
import com.lantanagroup.link.validation.exceptions.InvalidRubricDefinitionException;
import com.lantanagroup.link.validation.exceptions.RubricDryRunRequiredException;
import com.lantanagroup.link.validation.exceptions.PayloadParseException;
import com.lantanagroup.link.validation.exceptions.RubricLifecycleException;
import com.lantanagroup.link.validation.exceptions.RubricNotFoundException;
import com.lantanagroup.link.validation.exceptions.RubricVersionConflictException;
import com.lantanagroup.link.validation.exceptions.RubricVersionNotFoundException;
import com.lantanagroup.link.shared.utils.LogUtils;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.http.HttpStatus;
import org.springframework.http.ProblemDetail;
import org.springframework.web.bind.annotation.ExceptionHandler;
import org.springframework.web.bind.annotation.RestControllerAdvice;

/**
 * Maps the rubric-registry domain exceptions to the HTTP status codes documented on
 * {@link RubricController} (and the facility-override surface). Without this advice these plain
 * {@code RuntimeException}s fall through to the default error path and surface as HTTP 500,
 * contradicting the endpoints' {@code @ApiResponses} contract.
 *
 * <p>Responses use {@link ProblemDetail} (RFC 7807), consistent with {@code spring.mvc.problemdetails}
 * being enabled and with the {@code ResponseStatusException}-based errors from the other controllers.
 */
@RestControllerAdvice
public class RubricExceptionHandler {

    private static final Logger logger = LoggerFactory.getLogger(RubricExceptionHandler.class);

    @ExceptionHandler({
            RubricNotFoundException.class,
            RubricVersionNotFoundException.class,
            FacilityOverrideNotFoundException.class
    })
    public ProblemDetail handleNotFound(RuntimeException ex) {
        logger.warn("Rubric registry lookup failed: {}", LogUtils.sanitize(ex.getMessage()));
        return ProblemDetail.forStatusAndDetail(HttpStatus.NOT_FOUND, ex.getMessage());
    }

    @ExceptionHandler({
            RubricVersionConflictException.class,
            RubricLifecycleException.class,
            RubricDryRunRequiredException.class
    })
    public ProblemDetail handleConflict(RuntimeException ex) {
        logger.warn("Rubric lifecycle conflict: {}", LogUtils.sanitize(ex.getMessage()));
        return ProblemDetail.forStatusAndDetail(HttpStatus.CONFLICT, ex.getMessage());
    }

    @ExceptionHandler(InvalidRubricDefinitionException.class)
    public ProblemDetail handleInvalidDefinition(InvalidRubricDefinitionException ex) {
        logger.warn("Rejected rubric definition: {} {}",
                LogUtils.sanitize(ex.getMessage()), LogUtils.sanitize(ex.getErrors()));
        ProblemDetail problem = ProblemDetail.forStatusAndDetail(HttpStatus.BAD_REQUEST, ex.getMessage());
        problem.setProperty("errors", ex.getErrors());
        return problem;
    }

    @ExceptionHandler(PayloadParseException.class)
    public ProblemDetail handlePayloadParse(PayloadParseException ex) {
        logger.warn("Rejected unparseable rubric payload: {}", LogUtils.sanitize(ex.getMessage()));
        return ProblemDetail.forStatusAndDetail(HttpStatus.BAD_REQUEST, ex.getMessage());
    }
}
