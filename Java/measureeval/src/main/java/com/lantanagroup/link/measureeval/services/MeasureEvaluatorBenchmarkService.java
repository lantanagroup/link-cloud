package com.lantanagroup.link.measureeval.services;

import ca.uhn.fhir.context.FhirContext;
import ca.uhn.fhir.util.BundleUtil;
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
import java.util.ArrayList;
import java.util.HashMap;
import java.util.List;
import java.util.Map;

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
 *       FhirContext.forR4Cached()
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

    // Performance metrics accumulators
    private final List<Long> responseTimes = new ArrayList<>();
    private final List<Double> cpuUsages = new ArrayList<>();
    private final List<Long> memoryUsages = new ArrayList<>();
    private int totalRequests = 0;
    private int failedRequests = 0;

    // Capture patients in the "initial population"
    private final List<String> initialPopulationPatientIds = new ArrayList<>();

    // Measure evaluator instance (initialized later)
    private MeasureEvaluator measureEvaluator;

    // OSHI fields for OS-level metrics
    private SystemInfo systemInfo;
    private OperatingSystem operatingSystem;
    private OSProcess preTestProcessSnapshot;
    private int currentJavaPid;

    // Capture patients in groups
    private final Map<String, List<String>> groupPopulations = new HashMap<>();

    public MeasureEvaluatorBenchmarkService(String measurePackagePath, String patientDataDirectory, String periodStart, String periodEnd, FhirContext fhirContext) {
        this.measurePackagePath = measurePackagePath;
        this.patientDataDirectory = patientDataDirectory;
        this.periodStart = new DateTimeType(periodStart);
        this.periodEnd = new DateTimeType(periodEnd);
        this.fhirContext = fhirContext;
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
     * Loops over each JSON file in the patient data directory, rewrites URN UUID references,
     * evaluates the measure for each patient, and gathers performance metrics.
     */
    public void evaluateAllPatients(List<Bundle> bundles) throws IOException {
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
                updateMetrics(osBean, memoryBean, startWallTime, startCpuTime);
                logger.info("Measure evaluation completed for patient: {}", patient.getId());
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

    private void updateMetrics(OperatingSystemMXBean osBean, MemoryMXBean memoryBean, long startWallTime, long startCpuTime) {
        var endWallTime = System.nanoTime();
        var endCpuTime  = osBean.getProcessCpuTime();

        var durationWallNs = endWallTime - startWallTime;
        responseTimes.add(durationWallNs);

        var durationCpuNs = endCpuTime - startCpuTime;
        var iterationCpuUsage = (durationWallNs > 0) ? ((double) durationCpuNs / durationWallNs) * 100.0 : 0.0;
        cpuUsages.add(iterationCpuUsage);

        var usedMemoryBytes = memoryBean.getHeapMemoryUsage().getUsed();
        memoryUsages.add(usedMemoryBytes);
    }

    /**
     * Logs CPU usage, memory usage, latency, throughput, and initial population metrics.
     * Also enforces basic performance thresholds.
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
        enforcePerformanceThresholds();

        sb.append(String.format("----- End of Performance Report -----%n"));
        logger.info(sb.toString());
    }

    private void appendLatencyMetrics(StringBuilder sb) {
        if (responseTimes.isEmpty()) {
            sb.append(String.format("No response times recorded.%n"));
        } else {
            var totalTimeNano = responseTimes.stream().mapToLong(Long::longValue).sum();
            var count = responseTimes.size();
            var avgWallNs = totalTimeNano / count;
            var minWallNs = responseTimes.stream().mapToLong(Long::longValue).min().orElse(0);
            var maxWallNs = responseTimes.stream().mapToLong(Long::longValue).max().orElse(0);

            sb.append(String.format("Latency (ms):%n"))
                    .append(" - Average: ").append(avgWallNs / 1_000_000).append(String.format("%n"))
                    .append(" - Min    : ").append(minWallNs / 1_000_000).append(String.format("%n"))
                    .append(" - Max    : ").append(maxWallNs / 1_000_000).append(String.format("%n"));

            var totalTimeSeconds = totalTimeNano / 1_000_000_000.0;
            var throughput = (totalTimeSeconds > 0) ? (totalRequests / totalTimeSeconds) : 0.0;
            sb.append("Throughput (requests/second): ").append(String.format("%.2f", throughput)).append(String.format("%n"));
        }
        sb.append(String.format("%n"));
    }

    private void appendCpuUsageMetrics(StringBuilder sb) {
        if (!cpuUsages.isEmpty()) {
            var totalCpu = cpuUsages.stream().mapToDouble(Double::doubleValue).sum();
            var avgCpu = totalCpu / cpuUsages.size();
            var minCpu = cpuUsages.stream().mapToDouble(Double::doubleValue).min().orElse(0.0);
            var maxCpu = cpuUsages.stream().mapToDouble(Double::doubleValue).max().orElse(0.0);
            sb.append(String.format("CPU Usage (%%) per Evaluation:%n"))
                    .append(" - Average: ").append(String.format("%.2f%%", avgCpu)).append(String.format("%n"))
                    .append(" - Min    : ").append(String.format("%.2f%%", minCpu)).append(String.format("%n"))
                    .append(" - Max    : ").append(String.format("%.2f%%", maxCpu)).append(String.format("%n%n"));
        }
    }

    private void appendMemoryUsageMetrics(StringBuilder sb) {
        if (!memoryUsages.isEmpty()) {
            var memCount = memoryUsages.size();
            var minMemBytes = memoryUsages.stream().mapToLong(Long::longValue).min().orElse(0);
            var maxMemBytes = memoryUsages.stream().mapToLong(Long::longValue).max().orElse(0);
            var sumMemBytes = memoryUsages.stream().mapToLong(Long::longValue).sum();
            var avgMemBytes = sumMemBytes / memCount;
            var minMemMB = minMemBytes / (1024.0 * 1024.0);
            var maxMemMB = maxMemBytes / (1024.0 * 1024.0);
            var avgMemMB = avgMemBytes / (1024.0 * 1024.0);
            sb.append(String.format("Memory Usage (heap) per Evaluation (MB):%n"))
                    .append(" - Average: ").append(String.format("%.2f MB", avgMemMB)).append(String.format("%n"))
                    .append(" - Min    : ").append(String.format("%.2f MB", minMemMB)).append(String.format("%n"))
                    .append(" - Max    : ").append(String.format("%.2f MB", maxMemMB)).append(String.format("%n%n"));
        }
    }

    private void appendOshiMetrics(StringBuilder sb) {
        if (preTestProcessSnapshot != null && operatingSystem != null) {
            var postTestSnapshot = operatingSystem.getProcess(currentJavaPid);
            if (postTestSnapshot != null) {
                var totalCpuPct = postTestSnapshot.getProcessCpuLoadBetweenTicks(preTestProcessSnapshot) * 100.0;
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

    private void enforcePerformanceThresholds() {
        var avgWallMs = 0L;
        if (!responseTimes.isEmpty()) {
            var totalTimeNano = responseTimes.stream().mapToLong(Long::longValue).sum();
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
}

