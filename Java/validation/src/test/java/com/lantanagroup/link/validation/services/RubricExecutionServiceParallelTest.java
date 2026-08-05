package com.lantanagroup.link.validation.services;

import ca.uhn.fhir.context.FhirContext;
import com.fasterxml.jackson.databind.JsonNode;
import com.fasterxml.jackson.databind.ObjectMapper;
import com.lantanagroup.link.validation.configs.CheckExecutionConfig;
import com.lantanagroup.link.validation.enums.CheckType;
import com.lantanagroup.link.validation.enums.PiqiDimension;
import com.lantanagroup.link.validation.enums.Severity;
import com.lantanagroup.link.validation.entities.RubricCheck;
import com.lantanagroup.link.validation.entities.RubricVersion;
import com.lantanagroup.link.validation.models.EvaluateRequestDto;
import com.lantanagroup.link.validation.models.ExecutionContext;
import com.lantanagroup.link.validation.models.FindingDto;
import com.lantanagroup.link.validation.models.RawFinding;
import com.lantanagroup.link.validation.models.ValidationResultEnvelope;
import com.lantanagroup.link.validation.repositories.RubricCheckRepository;
import com.lantanagroup.link.validation.services.execution.CheckExecutor;
import com.lantanagroup.link.validation.services.execution.CheckExecutorRegistry;
import org.junit.jupiter.api.AfterEach;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.Test;
import org.springframework.scheduling.concurrent.ThreadPoolTaskExecutor;

import java.util.ArrayList;
import java.util.Collections;
import java.util.List;
import java.util.Map;
import java.util.Set;
import java.util.UUID;
import java.util.concurrent.ConcurrentHashMap;
import java.util.concurrent.CountDownLatch;
import java.util.concurrent.ExecutorService;
import java.util.concurrent.Executors;
import java.util.concurrent.Future;
import java.util.concurrent.TimeUnit;
import java.util.function.Function;
import java.util.stream.Collectors;
import java.util.stream.IntStream;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertFalse;
import static org.junit.jupiter.api.Assertions.assertNotNull;
import static org.junit.jupiter.api.Assertions.assertTrue;
import static org.mockito.ArgumentMatchers.any;
import static org.mockito.Mockito.mock;
import static org.mockito.Mockito.when;

/**
 * Covers the parallel check fan-out in {@link RubricExecutionService}.
 *
 * <p>The behaviour worth pinning down is that going parallel changed nothing observable: findings and
 * per-check durations still come back in rubric order, a check that throws is still isolated to its own
 * finding, and no check is ever dropped — including when the pool's queue overflows and the request
 * thread has to run checks itself.
 */
class RubricExecutionServiceParallelTest {

    private static final String RUBRIC_ID = "test-rubric";
    private static final FhirContext FHIR_CONTEXT = FhirContext.forR4();

    private final ObjectMapper objectMapper = new ObjectMapper();

    private RubricVersionResolver versionResolver;
    private RubricCheckRepository rubricCheckRepository;
    private CheckExecutorRegistry executorRegistry;
    private ThreadPoolTaskExecutor pool;

    private RubricVersion version;

    @BeforeEach
    void setUp() {
        version = RubricVersion.builder()
                .rubricVersionId(UUID.randomUUID())
                .rubricId(RUBRIC_ID)
                .semver("1.0.0")
                .checksum("checksum")
                .build();

        versionResolver = mock(RubricVersionResolver.class);
        rubricCheckRepository = mock(RubricCheckRepository.class);
        executorRegistry = mock(CheckExecutorRegistry.class);

        // Every evaluation in this suite is a dry run (persist=false).
        when(versionResolver.resolve(RUBRIC_ID, null, false)).thenReturn(version);
    }

    @AfterEach
    void tearDown() {
        if (pool != null) {
            pool.shutdown();
        }
    }

    @Test
    void findingsAndDurationsFollowRubricOrderNotCompletionOrder() {
        List<RubricCheck> checks = checks(12);
        // Earlier checks are slowest, so completion order is the reverse of submission order.
        RecordingExecutor executor = new RecordingExecutor(check -> sleepMillis(60 - check.getOrdinal() * 5));
        RubricExecutionService service = service(checks, executor, poolWith(6, 12, 0));

        ValidationResultEnvelope envelope = service.evaluate(RUBRIC_ID, null, request(), false);

        assertEquals(expectedLocalIds(12), checkIdsOf(envelope));
        assertEquals(expectedLocalIds(12), new ArrayList<>(envelope.getTrace().getCheckDurationsMs().keySet()));
        assertTrue(executor.threadNames().size() > 1, "expected the checks to run on more than one thread");
    }

    @Test
    void throwingCheckIsIsolatedToItsOwnFinding() {
        List<RubricCheck> checks = checks(3);
        RecordingExecutor executor = new RecordingExecutor(check -> {
            if ("check-01".equals(check.getCheckLocalId())) {
                throw new IllegalStateException("boom");
            }
        });
        RubricExecutionService service = service(checks, executor, poolWith(3, 6, 0));

        ValidationResultEnvelope envelope = service.evaluate(RUBRIC_ID, null, request(), false);

        assertEquals(List.of("check-00", "check-01", "check-02"), checkIdsOf(envelope));
        Map<String, FindingDto> byCheck = envelope.getFindings().stream()
                .collect(Collectors.toMap(FindingDto::getCheckId, Function.identity()));

        FindingDto failed = byCheck.get("check-01");
        assertEquals("check-execution-error", failed.getCode());
        assertEquals(Severity.ERROR, failed.getSeverity());
        assertTrue(failed.getMessage().contains("boom"), "message should carry the cause: " + failed.getMessage());
        assertEquals("Patient", failed.getLocation());

        // The other two checks are untouched, and the failure is still timed.
        assertEquals("test-finding", byCheck.get("check-00").getCode());
        assertEquals("test-finding", byCheck.get("check-02").getCode());
        assertEquals(3, envelope.getTrace().getCheckDurationsMs().size());
    }

    @Test
    void disabledChecksAreNeitherRunNorTimed() {
        List<RubricCheck> checks = new ArrayList<>(checks(4));
        checks.get(2).setEnabled(false);
        RecordingExecutor executor = new RecordingExecutor(check -> { });
        RubricExecutionService service = service(checks, executor, poolWith(4, 8, 0));

        ValidationResultEnvelope envelope = service.evaluate(RUBRIC_ID, null, request(), false);

        assertEquals(List.of("check-00", "check-01", "check-03"), checkIdsOf(envelope));
        Map<String, Long> durations = envelope.getTrace().getCheckDurationsMs();
        assertEquals(Set.of("check-00", "check-01", "check-03"), durations.keySet());
        assertFalse(executor.executed().contains("check-02"), "disabled check must not be executed");

        long summed = durations.values().stream().mapToLong(Long::longValue).sum();
        assertEquals(summed, envelope.getTrace().getCheckWorkMs().longValue());
    }

    @Test
    void everyCheckRunsWhenTheQueueOverflowsOntoTheRequestThread() {
        int checkCount = 24;
        List<RubricCheck> checks = checks(checkCount);
        // One worker, a queue that holds almost nothing, and checks slow enough that the worker cannot
        // drain it — so CallerRunsPolicy has to push work back onto the submitting thread.
        RecordingExecutor executor = new RecordingExecutor(check -> sleepMillis(60));
        RubricExecutionService service = service(checks, executor, poolWith(1, 1, 1));

        ValidationResultEnvelope envelope = service.evaluate(RUBRIC_ID, null, request(), false);

        assertEquals(expectedLocalIds(checkCount), checkIdsOf(envelope));
        assertEquals(checkCount, executor.executed().size());
        assertTrue(executor.threadNames().contains(Thread.currentThread().getName()),
                "expected the request thread to have run some checks, saw: " + executor.threadNames());
    }

    @Test
    void concurrentEvaluationsDoNotInterfere() throws Exception {
        int concurrency = 8;
        List<RubricCheck> checks = checks(10);
        RecordingExecutor executor = new RecordingExecutor(check -> sleepMillis(5));
        RubricExecutionService service = service(checks, executor, poolWith(4, 8, 0));

        ExecutorService callers = Executors.newFixedThreadPool(concurrency);
        try {
            CountDownLatch start = new CountDownLatch(1);
            List<Future<ValidationResultEnvelope>> futures = new ArrayList<>();
            for (int i = 0; i < concurrency; i++) {
                futures.add(callers.submit(() -> {
                    start.await();
                    return service.evaluate(RUBRIC_ID, null, request(), false);
                }));
            }
            start.countDown();

            for (Future<ValidationResultEnvelope> future : futures) {
                ValidationResultEnvelope envelope = future.get(60, TimeUnit.SECONDS);
                assertNotNull(envelope.getTrace());
                assertEquals(expectedLocalIds(10), checkIdsOf(envelope));
                assertEquals(10, envelope.getTrace().getCheckDurationsMs().size());
            }
            assertEquals(concurrency * 10, executor.executed().size());
        } finally {
            callers.shutdownNow();
        }
    }

    @Test
    void sequentialModeRunsEveryCheckOnTheRequestThread() {
        List<RubricCheck> checks = checks(8);
        RecordingExecutor executor = new RecordingExecutor(check -> { });
        RubricExecutionService service = service(checks, executor, poolWith(4, 8, 0), false);

        ValidationResultEnvelope envelope = service.evaluate(RUBRIC_ID, null, request(), false);

        assertEquals(expectedLocalIds(8), checkIdsOf(envelope));
        assertEquals(Set.of(Thread.currentThread().getName()), executor.threadNames(),
                "sequential mode must not touch the pool");
    }

    /**
     * The contract behind {@code vaas.checks.parallel}: flipping it changes wall-clock time and nothing
     * else. Both modes run the same checks against the same payload and must agree on every finding, in
     * order, and on which checks were timed.
     *
     * <p>This is the strongest guard in the suite. It is what lets the flag be flipped per environment
     * without re-validating behaviour, and it would fail if either path ever merged out of order or
     * dropped a check.
     */
    @Test
    void bothModesProduceIdenticalResults() {
        // Deliberately slowest-first, so parallel completion order is the reverse of rubric order and a
        // merge that followed completion would diverge from the sequential run.
        RecordingExecutor.SideEffect timing = check -> sleepMillis(40 - check.getOrdinal() * 4);

        ValidationResultEnvelope sequential = service(
                checks(10), new RecordingExecutor(timing), poolWith(4, 8, 0), false)
                .evaluate(RUBRIC_ID, null, request(), false);

        ValidationResultEnvelope parallel = service(
                checks(10), new RecordingExecutor(timing), poolWith(4, 8, 0), true)
                .evaluate(RUBRIC_ID, null, request(), false);

        assertEquals(fingerprint(sequential), fingerprint(parallel),
                "sequential and parallel must produce the same findings in the same order");
        assertEquals(
                new ArrayList<>(sequential.getTrace().getCheckDurationsMs().keySet()),
                new ArrayList<>(parallel.getTrace().getCheckDurationsMs().keySet()),
                "both modes must time the same checks, in rubric order");
        assertEquals(sequential.getStatus(), parallel.getStatus());
        assertEquals(sequential.getSummary().getErrorCount(), parallel.getSummary().getErrorCount());
        assertEquals(sequential.getSummary().getWarningCount(), parallel.getSummary().getWarningCount());
    }

    // ---------------------------------------------------------------- helpers

    /**
     * Everything about a response that must not depend on execution mode. Excludes the random request
     * and finding ids, and the per-check durations, which legitimately differ run to run.
     */
    private static List<String> fingerprint(ValidationResultEnvelope envelope) {
        return envelope.getFindings().stream()
                .map(f -> String.join("|", f.getCheckId(), String.valueOf(f.getDimension()),
                        String.valueOf(f.getSeverity()), f.getCode(), f.getMessage(),
                        String.valueOf(f.getLocation()), String.valueOf(f.getExpression())))
                .toList();
    }

    /** Parallel mode, which is what most of these tests are about. */
    private RubricExecutionService service(List<RubricCheck> checks, CheckExecutor executor, ThreadPoolTaskExecutor checkPool) {
        return service(checks, executor, checkPool, true);
    }

    private RubricExecutionService service(List<RubricCheck> checks, CheckExecutor executor,
                                           ThreadPoolTaskExecutor checkPool, boolean parallel) {
        when(rubricCheckRepository.findByRubricVersionIdOrderByOrdinalAsc(version.getRubricVersionId()))
                .thenReturn(checks);
        when(executorRegistry.get(any())).thenReturn(executor);
        return new RubricExecutionService(
                versionResolver,
                rubricCheckRepository,
                executorRegistry,
                new ResultEnvelopeAssembler(objectMapper, new ScoreAggregator()),
                mock(RubricResultPersister.class),
                FHIR_CONTEXT,
                objectMapper,
                checkPool,
                parallel);
    }

    private ThreadPoolTaskExecutor poolWith(int core, int max, int queueCapacity) {
        CheckExecutionConfig config = new CheckExecutionConfig();
        config.setCorePoolSize(core);
        config.setMaxPoolSize(max);
        config.setQueueCapacity(queueCapacity);
        pool = config.checkExecutorPool();
        return pool;
    }

    private EvaluateRequestDto request() {
        return EvaluateRequestDto.builder().payload(patientPayload()).build();
    }

    private JsonNode patientPayload() {
        try {
            return objectMapper.readTree("{\"resourceType\":\"Patient\",\"id\":\"p1\",\"active\":true}");
        } catch (Exception e) {
            throw new IllegalStateException(e);
        }
    }

    private static List<RubricCheck> checks(int count) {
        return IntStream.range(0, count)
                .mapToObj(i -> RubricCheck.builder()
                        .checkId(UUID.randomUUID())
                        .checkLocalId(String.format("check-%02d", i))
                        .type(CheckType.FHIRPATH)
                        .dimension(PiqiDimension.CONFORMANCE)
                        .ordinal(i)
                        .enabled(true)
                        .build())
                .collect(Collectors.toCollection(ArrayList::new));
    }

    private static List<String> expectedLocalIds(int count) {
        return IntStream.range(0, count).mapToObj(i -> String.format("check-%02d", i)).toList();
    }

    private static List<String> checkIdsOf(ValidationResultEnvelope envelope) {
        return envelope.getFindings().stream().map(FindingDto::getCheckId).toList();
    }

    private static void sleepMillis(long millis) {
        try {
            Thread.sleep(Math.max(0, millis));
        } catch (InterruptedException e) {
            Thread.currentThread().interrupt();
            throw new IllegalStateException(e);
        }
    }

    /**
     * A {@link CheckExecutor} that emits exactly one finding per check and records what ran where, so
     * tests can assert on ordering, coverage and the threads involved.
     */
    private static final class RecordingExecutor implements CheckExecutor {

        private final SideEffect sideEffect;
        private final List<String> executed = Collections.synchronizedList(new ArrayList<>());
        private final Set<String> threadNames = ConcurrentHashMap.newKeySet();

        private RecordingExecutor(SideEffect sideEffect) {
            this.sideEffect = sideEffect;
        }

        @Override
        public CheckType supports() {
            return CheckType.FHIRPATH;
        }

        @Override
        public List<RawFinding> execute(RubricCheck check, ExecutionContext context) {
            threadNames.add(Thread.currentThread().getName());
            executed.add(check.getCheckLocalId());
            sideEffect.apply(check);
            return List.of(RawFinding.builder()
                    .checkLocalId(check.getCheckLocalId())
                    .dimension(check.getDimension())
                    .severity(Severity.INFORMATION)
                    .code("test-finding")
                    .message("finding from " + check.getCheckLocalId())
                    .build());
        }

        List<String> executed() {
            synchronized (executed) {
                return new ArrayList<>(executed);
            }
        }

        Set<String> threadNames() {
            return Set.copyOf(threadNames);
        }

        @FunctionalInterface
        private interface SideEffect {
            void apply(RubricCheck check);
        }
    }
}
