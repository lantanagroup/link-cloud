package com.lantanagroup.link.measureeval.services;

import ca.uhn.fhir.context.FhirContext;
import ca.uhn.fhir.context.support.DefaultProfileValidationSupport;
import ca.uhn.fhir.context.support.IValidationSupport;
import com.lantanagroup.link.measureeval.models.FhirPathValidationResponse;
import org.hl7.fhir.common.hapi.validation.support.ValidationSupportChain;
import org.hl7.fhir.exceptions.FHIRException;
import org.hl7.fhir.r4.context.IWorkerContext;
import org.hl7.fhir.r4.fhirpath.ExpressionNode;
import org.hl7.fhir.r4.fhirpath.FHIRPathEngine;
import org.hl7.fhir.r4.fhirpath.TypeDetails;
import org.hl7.fhir.r4.hapi.ctx.HapiWorkerContext;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.stereotype.Service;

/**
 * Statically validates FHIRPath expressions using HAPI's {@link FHIRPathEngine#check}.
 * Unlike a plain parse, {@code check} verifies that every navigation segment resolves to a
 * real element on the resource type and that operators/operands are type-compatible, and it
 * infers the expression's return type. This is the semantic validation the .NET/Firely
 * compiler cannot do (Firely validates syntax only and is lenient about unknown elements).
 */
@Service
public class FhirPathValidationService {

    private static final Logger logger = LoggerFactory.getLogger(FhirPathValidationService.class);

    // Built once. Loading the R4 StructureDefinitions (DefaultProfileValidationSupport) is the
    // expensive part; the worker context is immutable and safe to share across requests.
    private final IWorkerContext workerContext;

    public FhirPathValidationService(FhirContext fhirContext) {
        // ValidationSupportChain replaces the deprecated CachingValidationSupport; caching is now
        // built into the chain via CacheConfiguration.
        IValidationSupport validationSupport = new ValidationSupportChain(
                ValidationSupportChain.CacheConfiguration.defaultValues(),
                new DefaultProfileValidationSupport(fhirContext));
        // Warm the StructureDefinition cache so the first request doesn't pay the unzip/index cost.
        validationSupport.fetchAllStructureDefinitions();
        this.workerContext = new HapiWorkerContext(fhirContext, validationSupport);
    }

    public FhirPathValidationResponse validate(String resourceType, String expression) {
        FhirPathValidationResponse response = new FhirPathValidationResponse();

        if (resourceType == null || resourceType.isBlank()) {
            response.getErrors().add("resourceType is required.");
        }
        if (expression == null || expression.isBlank()) {
            response.getErrors().add("fhirPath is required.");
        }
        if (!response.getErrors().isEmpty()) {
            response.setValid(false);
            return response;
        }

        // A fresh engine per call keeps this thread-safe under concurrent requests; the costly
        // worker context it wraps is shared.
        FHIRPathEngine engine = new FHIRPathEngine(workerContext);

        try {
            ExpressionNode parsed = engine.parse(expression);                                       // syntax
            TypeDetails returnType = engine.check(null, resourceType, resourceType, resourceType, parsed); // semantics

            if (returnType != null) {
                String described = returnType.describe();
                response.setReturnType(described);

                // Location conditions are expected to evaluate to a boolean; warn (not fail) otherwise.
                if (described == null || !described.toLowerCase().contains("boolean")) {
                    response.getWarnings().add(
                            "Expression return type is '" + described
                                    + "'; a condition is expected to evaluate to a boolean.");
                }
            }
        } catch (FHIRException e) {
            // Covers lexer (syntax), path-engine and definition errors: unknown element,
            // type mismatch, etc.
            response.getErrors().add(e.getMessage());
        } catch (Exception e) {
            logger.warn("Unexpected error validating FHIRPath expression against {}", resourceType, e);
            response.getErrors().add("FHIRPath validation failed: " + e.getMessage());
        }

        response.setValid(response.getErrors().isEmpty());
        return response;
    }
}
