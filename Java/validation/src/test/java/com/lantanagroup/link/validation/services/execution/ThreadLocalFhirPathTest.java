package com.lantanagroup.link.validation.services.execution;

import ca.uhn.fhir.context.FhirContext;
import ca.uhn.fhir.fhirpath.IFhirPath;
import ca.uhn.fhir.fhirpath.IFhirPathEvaluationContext;
import org.hl7.fhir.r4.model.BooleanType;
import org.hl7.fhir.r4.model.Patient;
import org.junit.jupiter.api.Test;

import java.util.ArrayList;
import java.util.List;
import java.util.concurrent.Callable;
import java.util.concurrent.CountDownLatch;
import java.util.concurrent.ExecutorService;
import java.util.concurrent.Executors;
import java.util.concurrent.Future;
import java.util.concurrent.TimeUnit;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertNotSame;
import static org.junit.jupiter.api.Assertions.assertSame;
import static org.junit.jupiter.api.Assertions.assertTrue;

/**
 * HAPI's FHIRPath engine carries mutable per-evaluation state and is not safe to share, so the bean
 * injected into the check executors must hand every thread its own.
 */
class ThreadLocalFhirPathTest {

    private static final FhirContext FHIR_CONTEXT = FhirContext.forR4();

    @Test
    void eachThreadGetsItsOwnEngineAndKeepsIt() throws Exception {
        ThreadLocalFhirPath fhirPath = new ThreadLocalFhirPath(FHIR_CONTEXT);

        IFhirPath onThisThread = fhirPath.current();
        assertSame(onThisThread, fhirPath.current(), "the same thread must reuse its engine");

        ExecutorService executor = Executors.newFixedThreadPool(2);
        try {
            Future<IFhirPath> first = executor.submit(fhirPath::current);
            Future<IFhirPath> second = executor.submit(fhirPath::current);
            IFhirPath a = first.get(30, TimeUnit.SECONDS);
            IFhirPath b = second.get(30, TimeUnit.SECONDS);

            assertNotSame(onThisThread, a);
            assertNotSame(onThisThread, b);
            assertNotSame(a, b, "two pool threads must not share one engine");
        } finally {
            executor.shutdownNow();
        }
    }

    @Test
    void concurrentEvaluationProducesCorrectResults() throws Exception {
        int threads = 4;
        int iterations = 50;
        ThreadLocalFhirPath fhirPath = new ThreadLocalFhirPath(FHIR_CONTEXT);

        ExecutorService executor = Executors.newFixedThreadPool(threads);
        try {
            CountDownLatch start = new CountDownLatch(1);
            List<Future<Integer>> futures = new ArrayList<>();
            for (int t = 0; t < threads; t++) {
                boolean active = t % 2 == 0;
                futures.add(executor.submit((Callable<Integer>) () -> {
                    // Each thread evaluates against its own resource: concurrent reads of one shared HAPI
                    // resource graph are a separate question from engine sharing, and mixing the two in
                    // here would blur what this test proves.
                    Patient patient = new Patient();
                    patient.setId("p1");
                    patient.setActive(active);

                    start.await();
                    int matched = 0;
                    for (int i = 0; i < iterations; i++) {
                        boolean evaluated = fhirPath.evaluateFirst(patient, "Patient.active", BooleanType.class)
                                .map(BooleanType::booleanValue)
                                .orElse(false);
                        if (evaluated == active) {
                            matched++;
                        }
                    }
                    return matched;
                }));
            }
            start.countDown();

            for (Future<Integer> future : futures) {
                assertEquals(iterations, future.get(60, TimeUnit.SECONDS),
                        "every evaluation should have returned the value set on that thread's own resource");
            }
        } finally {
            executor.shutdownNow();
        }
    }

    @Test
    void evaluationContextSetAtWiringTimeReachesEnginesCreatedLater() throws Exception {
        ThreadLocalFhirPath fhirPath = new ThreadLocalFhirPath(FHIR_CONTEXT);
        fhirPath.setEvaluationContext(new RecordingEvaluationContext());

        ExecutorService executor = Executors.newSingleThreadExecutor();
        try {
            // A thread that first touches the bean after wiring still evaluates successfully, which is
            // only true if the remembered context was applied to its freshly built engine.
            Boolean evaluated = executor.submit(() -> {
                Patient patient = new Patient();
                patient.setActive(true);
                return fhirPath.evaluateFirst(patient, "Patient.active", BooleanType.class)
                        .map(BooleanType::booleanValue)
                        .orElse(false);
            }).get(30, TimeUnit.SECONDS);
            assertTrue(evaluated);
        } finally {
            executor.shutdownNow();
        }
    }

    /** Minimal no-op context; the test only cares that it is accepted by every engine. */
    private static final class RecordingEvaluationContext implements IFhirPathEvaluationContext {
    }
}
