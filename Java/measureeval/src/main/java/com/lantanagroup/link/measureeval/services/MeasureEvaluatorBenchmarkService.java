package com.lantanagroup.link.measureeval.services;

import ca.uhn.fhir.context.FhirContext;
import ca.uhn.fhir.util.BundleUtil;
import com.lantanagroup.link.measureeval.utils.PerformanceSummary;
import com.lantanagroup.link.measureeval.utils.UrnUuidRewriter;
import com.sun.management.OperatingSystemMXBean;
import oshi.SystemInfo;
import oshi.software.os.OSProcess;
import oshi.software.os.OperatingSystem;
import org.hl7.fhir.r4.model.*;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;

import java.io.File;
import java.io.IOException;
import java.lang.management.ManagementFactory;
import java.lang.management.MemoryMXBean;
import java.nio.file.Files;
import java.util.*;

import static java.lang.String.format;

/**
 * A service used to:
 *  1) Load and validate a measure package
 *  2) Evaluate it against Synthea (or other) patient bundles
 *  3) Collect performance metrics (CPU, memory, latency)
 *  4) Identify which patients are in the "initial population"
 * Usage Example:
 *   MeasureEvaluatorBenchmarkService benchmark = new MeasureEvaluatorBenchmarkService(
 *       "/path/to/measure-bundle.json",
 *       "/path/to/synthea/output/fhir",
 *       "2022-01-01",
 *       "2022-12-31",
 *       FhirContext.forR4Cached(),
 *       1000
 *   );
 *   benchmark.initialize();
 *   benchmark.evaluateAllPatients();
 *   benchmark.generatePerformanceReport();
 */
public class MeasureEvaluatorBenchmarkService {

    private static final Logger logger = LoggerFactory.getLogger(MeasureEvaluatorBenchmarkService.class);

    private final String measurePackagePath;
    private final String patientDataDirectory;
    private final DateTimeType periodStart;
    private final DateTimeType periodEnd;
    private final FhirContext fhirContext;
    private final long latencyBenchmarkMs;


    // Performance metrics accumulators
    private final List<Long> responseTimes = new ArrayList<>();
    private final List<Double> cpuUsages = new ArrayList<>();
    private final List<Long> memoryUsages = new ArrayList<>();
    private int totalRequests = 0;
    private int failedRequests = 0;

    // Measure evaluator instance (initialized later)
    private MeasureEvaluator measureEvaluator;

    // OSHI fields for OS-level metrics
    private SystemInfo systemInfo;
    private OperatingSystem operatingSystem;
    private OSProcess preTestProcessSnapshot;
    private int currentJavaPid;

    // Capture patients in groups
    private final Map<String, List<String>> groupPopulations = new HashMap<>();

    // NEW: Store evaluation results for summary min/max tracking.
    private final List<EvaluationResult> evaluationResults = new ArrayList<>();

    public MeasureEvaluatorBenchmarkService(String measurePackagePath,
                                            String patientDataDirectory,
                                            String periodStart,
                                            String periodEnd,
                                            FhirContext fhirContext,
                                            long latencyBenchmarkMs) {
        this.measurePackagePath = measurePackagePath;
        this.patientDataDirectory = patientDataDirectory;
        this.periodStart = new DateTimeType(periodStart);
        this.periodEnd = new DateTimeType(periodEnd);
        this.fhirContext = fhirContext;
        this.latencyBenchmarkMs = latencyBenchmarkMs;
    }

    // Inner class to hold per-evaluation metrics.
    private static class EvaluationMetrics {
        final long durationWallNs;
        final double iterationCpuUsage;
        final long usedMemoryBytes;

        EvaluationMetrics(long durationWallNs, double iterationCpuUsage, long usedMemoryBytes) {
            this.durationWallNs = durationWallNs;
            this.iterationCpuUsage = iterationCpuUsage;
            this.usedMemoryBytes = usedMemoryBytes;
        }
    }

    // NEW: Inner class to store a complete evaluation result (with patient identifier)
    private static class EvaluationResult {
        final String identifier;
        final long latencyNs;
        final double cpuUsage;
        final long memoryUsedBytes;

        EvaluationResult(String identifier, long latencyNs, double cpuUsage, long memoryUsedBytes) {
            this.identifier = identifier;
            this.latencyNs = latencyNs;
            this.cpuUsage = cpuUsage;
            this.memoryUsedBytes = memoryUsedBytes;
        }
    }

    /**
     * 1) Validates the measure package JSON file.
     * 2) Initializes the measure evaluator.
     * 3) Captures a pre-test OSHI snapshot for CPU/memory metrics.
     */
    public void initialize() throws IOException {
        logger.info("Initializing environment...");

        var measurePackageFile = new File(this.measurePackagePath);
        if (!measurePackageFile.exists() || measurePackageFile.length() == 0) {
            throw new IllegalStateException("Measure package file is missing or empty: " + measurePackageFile);
        }

        var measurePackage = parseMeasurePackage(measurePackageFile);
        validateMeasurePackage(measurePackage);
        this.measureEvaluator = MeasureEvaluator.compile(this.fhirContext, measurePackage, true);
        logger.info("Measure package validated successfully.");

        systemInfo = new SystemInfo();
        operatingSystem = systemInfo.getOperatingSystem();
        currentJavaPid = (int) ProcessHandle.current().pid();
        preTestProcessSnapshot = operatingSystem.getProcess(currentJavaPid);
        if (preTestProcessSnapshot == null) {
            logger.warn("Unable to capture OS process info for PID={}", currentJavaPid);
        } else {
            logger.info("Captured pre-test snapshot for PID={}", currentJavaPid);
        }
    }

    private Bundle parseMeasurePackage(File measurePackageFile) throws IOException {
        var content = Files.readString(measurePackageFile.toPath());
        return fhirContext.newJsonParser().parseResource(Bundle.class, content);
    }

    private void validateMeasurePackage(Bundle measurePackage) {
        var errors = new ArrayList<String>();
        new MeasureDefinitionBundleValidator().doValidate(measurePackage, errors);
        if (!errors.isEmpty()) {
            throw new IllegalStateException("Measure package validation errors: " + errors);
        }
    }

    /**
     * Loops over each patient data bundle and gathers performance metrics.
     */
    public void evaluateAllPatients(List<Bundle> bundles) {
        logger.info("Evaluating measure against patient data...");

        var osBean = ManagementFactory.getPlatformMXBean(OperatingSystemMXBean.class);
        var memoryBean = ManagementFactory.getMemoryMXBean();

        for (var bundle : bundles) {
            var patients = BundleUtil.toListOfResourcesOfType(fhirContext, bundle, Patient.class);
            if (patients.isEmpty()) {
                logger.warn("No Patient resource found in bundle {}", bundle.getId());
                continue;
            }
            var patient = patients.get(0);
            enforcePatientIdFormat(patient);

            var startWallTime = System.nanoTime();
            var startCpuTime  = osBean.getProcessCpuTime();
            totalRequests++;

            try {
                var subject = new StringType(patient.getId());
                var report = measureEvaluator.evaluate(periodStart, periodEnd, subject, bundle);
                if (report == null) {
                    throw new IllegalStateException("MeasureReport is null for patient: " + patient.getId());
                }
                captureGroupPopulations(report);
            } catch (Exception e) {
                failedRequests++;
                logger.error("Measure evaluation failed for bundle {}: {}", bundle.getId(), e.getMessage(), e);
            } finally {
                var metrics = updateMetrics(osBean, memoryBean, startWallTime, startCpuTime);
                logger.info("Measure evaluation completed for patient: {}", patient.getId());
                logIndividualReport(patient.getId(), metrics);
                // NEW: Record the evaluation result for summary min/max reporting.
                evaluationResults.add(new EvaluationResult(patient.getId(), metrics.durationWallNs, metrics.iterationCpuUsage, metrics.usedMemoryBytes));
            }
        }
    }

    /**
     * Loops over each JSON file in the patient data directory, rewrites URN UUID references,
     * evaluates the measure for each patient, and gathers performance metrics.
     */
    public void evaluateAllPatients() throws IOException {
        logger.info("Evaluating measure against patient data...");

        var patientDirectory = new File(this.patientDataDirectory);
        if (!patientDirectory.exists() || !patientDirectory.isDirectory()) {
            throw new IllegalStateException("Invalid patient data directory: " + this.patientDataDirectory);
        }

        var jsonFiles = patientDirectory.listFiles((dir, name) -> name.endsWith(".json"));
        if (jsonFiles == null || jsonFiles.length == 0) {
            throw new IllegalStateException("No JSON files found in patient data directory: " + this.patientDataDirectory);
        }

        var osBean = ManagementFactory.getPlatformMXBean(OperatingSystemMXBean.class);
        var memoryBean = ManagementFactory.getMemoryMXBean();

        for (var file : jsonFiles) {
            var fileName = file.getName();
            if (shouldSkipFile(fileName)) {
                logger.debug("Skipping non-patient resource file: {}", fileName);
                continue;
            }

            var content = Files.readString(file.toPath());
            var bundle = UrnUuidRewriter.rewriteUrnUuids(
                    fhirContext.newJsonParser().parseResource(Bundle.class, content), fhirContext);
            bundle.setId(fileName);

            var patients = BundleUtil.toListOfResourcesOfType(fhirContext, bundle, Patient.class);
            if (patients.isEmpty()) {
                logger.warn("No Patient resource found in bundle {}", fileName);
                continue;
            }
            var patient = patients.get(0);
            enforcePatientIdFormat(patient);

            long startWallTime = System.nanoTime();
            long startCpuTime  = osBean.getProcessCpuTime();
            totalRequests++;

            try {
                var subject = new StringType(patient.getId());
                var report = measureEvaluator.evaluate(periodStart, periodEnd, subject, bundle);
                if (report == null) {
                    throw new IllegalStateException("MeasureReport is null for bundle " + fileName);
                }
                captureGroupPopulations(report);
            } catch (Exception e) {
                failedRequests++;
                logger.error("Measure evaluation failed for bundle {}: {}", fileName, e.getMessage(), e);
            } finally {
                EvaluationMetrics metrics = updateMetrics(osBean, memoryBean, startWallTime, startCpuTime);
                logger.info("Measure evaluation completed for file: {}", fileName);
                logIndividualReport(patient.getId(), metrics);
                evaluationResults.add(new EvaluationResult(fileName, metrics.durationWallNs, metrics.iterationCpuUsage, metrics.usedMemoryBytes));
            }
        }
    }

    /**
     * Updates the metrics for a single evaluation and returns the computed values.
     */
    private EvaluationMetrics updateMetrics(OperatingSystemMXBean osBean, MemoryMXBean memoryBean, long startWallTime, long startCpuTime) {
        long endWallTime = System.nanoTime();
        long endCpuTime  = osBean.getProcessCpuTime();

        long durationWallNs = endWallTime - startWallTime;
        responseTimes.add(durationWallNs);

        long durationCpuNs = endCpuTime - startCpuTime;
        double iterationCpuUsage = (durationWallNs > 0) ? ((double) durationCpuNs / durationWallNs) * 100.0 : 0.0;
        cpuUsages.add(iterationCpuUsage);

        long usedMemoryBytes = memoryBean.getHeapMemoryUsage().getUsed();
        memoryUsages.add(usedMemoryBytes);

        return new EvaluationMetrics(durationWallNs, iterationCpuUsage, usedMemoryBytes);
    }

    /**
     * Logs a detailed report for an individual evaluation.
     */
    private void logIndividualReport(String identifier, EvaluationMetrics metrics) {
        var latencyMs = metrics.durationWallNs / 1_000_000;
        var memoryUsedMB = metrics.usedMemoryBytes / (1024 * 1024);
        logger.info("Individual Evaluation Report for {}:", identifier);
        logger.info(" - Latency: {} ms", latencyMs);
        logger.info(String.format(" - CPU Usage during evaluation: %.2f%%", metrics.iterationCpuUsage));
        logger.info(" - Memory used (heap): {} MB", memoryUsedMB);
        // Determine group populations for this patient.
        List<String> groups = new ArrayList<>();
        for (Map.Entry<String, List<String>> entry : groupPopulations.entrySet()) {
            if (entry.getValue().contains(identifier)) {
                groups.add(entry.getKey());
            }
        }
        if (!groups.isEmpty()) {
            logger.info(" - Group Populations: {}", groups);
        } else {
            logger.info(" - Group Populations: None");
        }
    }

    /**
     * Generates a summary report that aggregates metrics across all evaluations.
     * Also identifies patients whose latency exceeds the benchmark.
     */
    public void generatePerformanceReport() {
        var sb = new StringBuilder();
        sb.append(String.format("%n----- Performance Report -----%n"))
                .append("Total Measure Evaluation Requests: ").append(totalRequests).append(String.format("%n"))
                .append("Failed Requests: ").append(failedRequests).append("\n");

        var errorRate = (totalRequests == 0) ? 0.0 : ((double) failedRequests / totalRequests) * 100.0;
        sb.append("Error Rate: ").append(String.format("%.2f", errorRate)).append("%").append(String.format("%n%n"));

        appendLatencyMetrics(sb);
        appendCpuUsageMetrics(sb);
        appendMemoryUsageMetrics(sb);
        appendOshiMetrics(sb);
        appendGroupPopulationMetrics(sb);
        appendLatencyBenchmarkResults(sb);
        enforcePerformanceThresholds();

        sb.append(String.format("----- End of Performance Report -----%n"));
        logger.info(sb.toString());
    }


    private void appendLatencyMetrics(StringBuilder sb) {
        if (evaluationResults.isEmpty()) {
            sb.append(String.format("No response times recorded.%n"));
        } else {
            long totalLatencyNs = evaluationResults.stream().mapToLong(er -> er.latencyNs).sum();
            int count = evaluationResults.size();
            long avgLatencyMs = (totalLatencyNs / count) / 1_000_000;

            // Find min and max latency evaluations.
            EvaluationResult minLatency = evaluationResults.stream().min((er1, er2) -> Long.compare(er1.latencyNs, er2.latencyNs)).orElse(null);
            EvaluationResult maxLatency = evaluationResults.stream().max((er1, er2) -> Long.compare(er1.latencyNs, er2.latencyNs)).orElse(null);

            sb.append(String.format("Latency (ms):%n"))
                    .append(" - Average: ").append(avgLatencyMs).append(String.format("%n"));
            if (minLatency != null) {
                sb.append(" - Min    : ").append(minLatency.latencyNs / 1_000_000)
                        .append(" ms (Patient: ").append(minLatency.identifier).append(")").append(String.format("%n"));
            }
            if (maxLatency != null) {
                sb.append(" - Max    : ").append(maxLatency.latencyNs / 1_000_000)
                        .append(" ms (Patient: ").append(maxLatency.identifier).append(")").append(String.format("%n"));
            }
        }
        sb.append(String.format("%n"));
    }

    private void appendCpuUsageMetrics(StringBuilder sb) {
        if (!evaluationResults.isEmpty()) {
            double totalCpu = evaluationResults.stream().mapToDouble(er -> er.cpuUsage).sum();
            int count = evaluationResults.size();
            double avgCpu = totalCpu / count;
            EvaluationResult minCpu = evaluationResults.stream().min(Comparator.comparingDouble(er -> er.cpuUsage)).orElse(null);
            EvaluationResult maxCpu = evaluationResults.stream().max(Comparator.comparingDouble(er -> er.cpuUsage)).orElse(null);

            sb.append(String.format("CPU Usage (%%) per Evaluation:%n"))
                    .append(" - Average: ").append(String.format("%.2f%%", avgCpu)).append(String.format("%n"));
            if (minCpu != null) {
                sb.append(" - Min    : ").append(String.format("%.2f%%", minCpu.cpuUsage))
                        .append(" (Patient: ").append(minCpu.identifier).append(")").append(String.format("%n"));
            }
            if (maxCpu != null) {
                sb.append(" - Max    : ").append(String.format("%.2f%%", maxCpu.cpuUsage))
                        .append(" (Patient: ").append(maxCpu.identifier).append(")").append(String.format("%n"));
            }
            sb.append(String.format("%n"));
        }
    }

    private void appendMemoryUsageMetrics(StringBuilder sb) {
        if (!evaluationResults.isEmpty()) {
            int count = evaluationResults.size();
            long totalMemBytes = evaluationResults.stream().mapToLong(er -> er.memoryUsedBytes).sum();
            double avgMemMB = (totalMemBytes / (double) count) / (1024.0 * 1024.0);
            EvaluationResult minMem = evaluationResults.stream().min((er1, er2) -> Long.compare(er1.memoryUsedBytes, er2.memoryUsedBytes)).orElse(null);
            EvaluationResult maxMem = evaluationResults.stream().max((er1, er2) -> Long.compare(er1.memoryUsedBytes, er2.memoryUsedBytes)).orElse(null);

            sb.append(String.format("Memory Usage (heap) per Evaluation (MB):%n"))
                    .append(" - Average: ").append(String.format("%.2f MB", avgMemMB)).append(String.format("%n"));
            if (minMem != null) {
                sb.append(" - Min    : ").append(String.format("%.2f MB", minMem.memoryUsedBytes / (1024.0 * 1024.0)))
                        .append(" (Patient: ").append(minMem.identifier).append(")").append(String.format("%n"));
            }
            if (maxMem != null) {
                sb.append(" - Max    : ").append(String.format("%.2f MB", maxMem.memoryUsedBytes / (1024.0 * 1024.0)))
                        .append(" (Patient: ").append(maxMem.identifier).append(")").append(String.format("%n"));
            }
            sb.append(String.format("%n"));
        }
    }

    private void appendOshiMetrics(StringBuilder sb) {
        if (preTestProcessSnapshot != null && operatingSystem != null) {
            var postTestSnapshot = operatingSystem.getProcess(currentJavaPid);
            if (postTestSnapshot != null) {
                var totalCpuPct = postTestSnapshot.getProcessCpuLoadBetweenTicks(preTestProcessSnapshot) * 100.0;
                // insight into the process’s real memory footprint
                var rssBytes = postTestSnapshot.getResidentSetSize();
                var rssMB = rssBytes / (1024.0 * 1024.0);
                sb.append(String.format("=== OSHI - Java Process Metrics ===%n"))
                        .append("Overall CPU usage for PID=").append(currentJavaPid)
                        .append(": ").append(String.format("%.2f%%", totalCpuPct)).append(String.format("%n"))
                        .append("Resident Set Size (RSS): ").append(String.format("%.2f MB", rssMB)).append(String.format("%n%n"));
            } else {
                sb.append("OSHI - Unable to track process metrics for PID=").append(currentJavaPid).append(String.format("%n%n"));
            }
        }
    }

    private void appendGroupPopulationMetrics(StringBuilder sb) {
        sb.append(String.format("=== Group Population Percentage ===%n"));
        for (var entry : groupPopulations.entrySet()) {
            var count = entry.getValue().size();
            if (totalRequests > 0) {
                var percent = ((double) count / totalRequests) * 100.0;
                sb.append(String.format("Patients in %s: %d out of %d (%.2f%%)%n", entry.getKey(), count, totalRequests, percent));
            } else {
                sb.append(String.format("No requests or no patients evaluated for %s.%n", entry.getKey()));
            }
            sb.append(String.format("%n"));
        }
    }

    private void appendLatencyBenchmarkResults(StringBuilder sb) {
        sb.append("Evaluations exceeding latency benchmark of ")
                .append(latencyBenchmarkMs)
                .append(" ms:")
                .append(System.lineSeparator());

        // Filter the evaluation results for those that exceed the benchmark.
        List<EvaluationResult> aboveBenchmark = evaluationResults.stream()
                .filter(er -> (er.latencyNs / 1_000_000) > latencyBenchmarkMs)
                .toList();

        if (aboveBenchmark.isEmpty()) {
            sb.append(" - None").append(System.lineSeparator());
        } else {
            for (EvaluationResult er : aboveBenchmark) {
                long latencyMs = er.latencyNs / 1_000_000;
                sb.append(" - Patient: ").append(er.identifier)
                        .append(" with latency: ").append(latencyMs).append(" ms")
                        .append(System.lineSeparator());
            }
        }
        sb.append(System.lineSeparator());
    }

    private void enforcePerformanceThresholds() {
        long avgWallMs = 0L;
        if (!responseTimes.isEmpty()) {
            long totalTimeNano = responseTimes.stream().mapToLong(Long::longValue).sum();
            avgWallMs = (totalTimeNano / responseTimes.size()) / 1_000_000;
        }
        double finalErrorRate = (totalRequests == 0) ? 0.0 : ((double) failedRequests / totalRequests) * 100.0;
        if (avgWallMs > 2000) {
            throw new IllegalStateException(format("Average response time exceeded threshold! [avg=%d ms]", avgWallMs));
        }
        if (finalErrorRate > 5.0) {
            throw new IllegalStateException(format("Error rate is too high! [%.2f%%]", finalErrorRate));
        }
    }

    public List<Bundle> getSyntheaBundles() throws IOException {
        var patientDirectory = new File(this.patientDataDirectory);
        if (!patientDirectory.exists() || !patientDirectory.isDirectory()) {
            throw new IllegalStateException("Invalid patient data directory: " + this.patientDataDirectory);
        }

        var jsonFiles = patientDirectory.listFiles((dir, name) -> name.endsWith(".json"));
        if (jsonFiles == null || jsonFiles.length == 0) {
            throw new IllegalStateException("No JSON files found in patient data directory: " + this.patientDataDirectory);
        }

        var bundles = new ArrayList<Bundle>();
        for (var file : jsonFiles) {
            var fileName = file.getName();
            if (shouldSkipFile(fileName)) {
                logger.debug("Skipping non-patient resource file: {}", fileName);
                continue;
            }

            var content = Files.readString(file.toPath());
            var bundle = UrnUuidRewriter.rewriteUrnUuids(
                    fhirContext.newJsonParser().parseResource(Bundle.class, content), fhirContext);
            bundle.setId(fileName);
            bundles.add(bundle);
        }
        return bundles;
    }

    public void captureGroupPopulations(MeasureReport measureReport) {
        var subjectRef = measureReport.hasSubject() ? measureReport.getSubject().getReference() : null;
        for (MeasureReport.MeasureReportGroupComponent group : measureReport.getGroup()) {
            for (MeasureReport.MeasureReportGroupPopulationComponent population : group.getPopulation()) {
                var display = population.getCode().getCodingFirstRep().getDisplay();
                var key = display != null ? display : population.getCode().getCodingFirstRep().getCode();
                groupPopulations.computeIfAbsent(key, p -> new ArrayList<>());
                if (population.hasCount() && population.getCount() > 0 && subjectRef != null) {
                    groupPopulations.get(key).add(subjectRef);
                }
            }
        }
    }

    private boolean shouldSkipFile(String fileName) {
        return fileName.startsWith("hospitalInformation") || fileName.startsWith("practitionerInformation");
    }

    private void enforcePatientIdFormat(Patient patient) {
        var patientId = patient.getIdElement().getIdPart();
        if (!patient.getIdElement().getValue().startsWith("Patient/")) {
            patient.setId("Patient/" + patientId);
        }
    }

    // Make the summary report available as a POJO
    public PerformanceSummary getPerformanceSummary() {
        // Compute basic request metrics
        int total = totalRequests;
        int failed = failedRequests;
        double errorRate = (total == 0) ? 0.0 : ((double) failed / total) * 100.0;

        // Compute latency statistics from evaluationResults
        long totalLatencyNs = evaluationResults.stream().mapToLong(er -> er.latencyNs).sum();
        int count = evaluationResults.size();
        long avgLatencyMs = (count > 0) ? (totalLatencyNs / count) / 1_000_000 : 0;

        EvaluationResult minLatencyResult = evaluationResults.stream()
                .min(Comparator.comparingLong(er -> er.latencyNs)).orElse(null);
        EvaluationResult maxLatencyResult = evaluationResults.stream()
                .max(Comparator.comparingLong(er -> er.latencyNs)).orElse(null);
        long minLatencyMs = (minLatencyResult != null) ? minLatencyResult.latencyNs / 1_000_000 : 0;
        String minLatencyPatient = (minLatencyResult != null) ? minLatencyResult.identifier : "";
        long maxLatencyMs = (maxLatencyResult != null) ? maxLatencyResult.latencyNs / 1_000_000 : 0;
        String maxLatencyPatient = (maxLatencyResult != null) ? maxLatencyResult.identifier : "";

        // Compute CPU usage statistics
        double totalCpu = evaluationResults.stream().mapToDouble(er -> er.cpuUsage).sum();
        double avgCpu = (count > 0) ? totalCpu / count : 0.0;
        EvaluationResult minCpuResult = evaluationResults.stream()
                .min(Comparator.comparingDouble(er -> er.cpuUsage)).orElse(null);
        EvaluationResult maxCpuResult = evaluationResults.stream()
                .max(Comparator.comparingDouble(er -> er.cpuUsage)).orElse(null);
        double minCpu = (minCpuResult != null) ? minCpuResult.cpuUsage : 0.0;
        String minCpuPatient = (minCpuResult != null) ? minCpuResult.identifier : "";
        double maxCpu = (maxCpuResult != null) ? maxCpuResult.cpuUsage : 0.0;
        String maxCpuPatient = (maxCpuResult != null) ? maxCpuResult.identifier : "";

        // Compute memory usage statistics (converted to MB)
        long totalMemBytes = evaluationResults.stream().mapToLong(er -> er.memoryUsedBytes).sum();
        double avgMemMB = (count > 0) ? (totalMemBytes / (double) count) / (1024.0 * 1024.0) : 0.0;
        EvaluationResult minMemResult = evaluationResults.stream()
                .min(Comparator.comparingLong(er -> er.memoryUsedBytes)).orElse(null);
        EvaluationResult maxMemResult = evaluationResults.stream()
                .max(Comparator.comparingLong(er -> er.memoryUsedBytes)).orElse(null);
        double minMemMB = (minMemResult != null) ? minMemResult.memoryUsedBytes / (1024.0 * 1024.0) : 0.0;
        String minMemPatient = (minMemResult != null) ? minMemResult.identifier : "";
        double maxMemMB = (maxMemResult != null) ? maxMemResult.memoryUsedBytes / (1024.0 * 1024.0) : 0.0;
        String maxMemPatient = (maxMemResult != null) ? maxMemResult.identifier : "";

        // Build a group population percentages map (based on your existing logic)
        Map<String, Double> groupPercentages = new HashMap<>();
        for (Map.Entry<String, List<String>> entry : groupPopulations.entrySet()) {
            int countInGroup = entry.getValue().size();
            double percent = (total > 0) ? ((double) countInGroup / total) * 100.0 : 0.0;
            groupPercentages.put(entry.getKey(), percent);
        }

        // Determine evaluations that exceeded the latency benchmark
        List<String> evalsAboveBenchmark = evaluationResults.stream()
                .filter(er -> (er.latencyNs / 1_000_000) > latencyBenchmarkMs)
                .map(er -> er.identifier + " (" + (er.latencyNs / 1_000_000) + " ms)")
                .toList();

        // Build and return the summary object
        return new PerformanceSummary(
                total,
                failed,
                errorRate,
                avgLatencyMs,
                minLatencyMs,
                minLatencyPatient,
                maxLatencyMs,
                maxLatencyPatient,
                avgCpu,
                minCpu,
                minCpuPatient,
                maxCpu,
                maxCpuPatient,
                avgMemMB,
                minMemMB,
                minMemPatient,
                maxMemMB,
                maxMemPatient,
                groupPercentages,
                evalsAboveBenchmark
        );
    }
}
