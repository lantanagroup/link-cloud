package com.lantanagroup.link.validation.audit;

import ca.uhn.fhir.context.FhirContext;
import ca.uhn.fhir.context.support.DefaultProfileValidationSupport;
import ca.uhn.fhir.parser.IParser;
import ca.uhn.fhir.parser.LenientErrorHandler;
import ca.uhn.fhir.validation.FhirValidator;
import ca.uhn.fhir.validation.ResultSeverityEnum;
import ca.uhn.fhir.validation.SingleValidationMessage;
import ca.uhn.fhir.validation.ValidationOptions;
import ca.uhn.fhir.validation.ValidationResult;
import ch.qos.logback.classic.Level;
import ch.qos.logback.classic.Logger;
import com.fasterxml.jackson.annotation.JsonInclude;
import com.fasterxml.jackson.databind.ObjectMapper;
import com.fasterxml.jackson.databind.SerializationFeature;
import org.hl7.fhir.common.hapi.validation.support.CachingValidationSupport;
import org.hl7.fhir.common.hapi.validation.support.CommonCodeSystemsTerminologyService;
import org.hl7.fhir.common.hapi.validation.support.InMemoryTerminologyServerValidationSupport;
import org.hl7.fhir.common.hapi.validation.support.PrePopulatedValidationSupport;
import org.hl7.fhir.common.hapi.validation.support.SnapshotGeneratingValidationSupport;
import org.hl7.fhir.common.hapi.validation.support.ValidationSupportChain;
import org.hl7.fhir.common.hapi.validation.validator.FhirInstanceValidator;
import org.hl7.fhir.instance.model.api.IBaseResource;
import org.hl7.fhir.r4.model.Bundle;
import org.hl7.fhir.r4.model.CanonicalType;
import org.hl7.fhir.r4.model.Resource;
import org.hl7.fhir.r4.model.ResourceType;
import org.hl7.fhir.utilities.npm.NpmPackage;

import java.io.IOException;
import java.io.InputStream;
import java.nio.file.Files;
import java.nio.file.Path;
import java.nio.file.Paths;
import java.util.ArrayList;
import java.util.Arrays;
import java.util.Collection;
import java.util.Comparator;
import java.util.HashMap;
import java.util.List;
import java.util.Map;
import java.util.regex.Pattern;
import java.util.stream.Collectors;
import java.util.stream.Stream;
import org.slf4j.LoggerFactory;

/**
 * Point-and-shoot validation cost audit.
 *
 * <p>Given an IG package (.tgz), a directory of dependency IG packages, and a directory of
 * sample bundles, this tool drives validation of every resource in every bundle against every
 * declared profile in isolation, timing each call, and emits a JSON report ranked by total
 * validation cost.
 *
 * <p>Run via Maven:
 * <pre>
 *   mvn -pl validation exec:java \
 *     -Dexec.mainClass=com.lantanagroup.link.validation.audit.ValidationCostAudit \
 *     -Dexec.args="--ig /path/to/package.tgz \
 *                  --deps /path/to/deps-dir \
 *                  --bundles /path/to/bundles-dir \
 *                  --iterations 3 \
 *                  --report cost-report.json"
 * </pre>
 *
 * <p>Not measured: per-invariant / per-constraint timing (HAPI doesn't natively expose it).
 * If a profile shows high cost, the follow-up conversation is instrumenting the FHIRPath
 * engine to attribute time to specific invariants — deliberately out of scope here.
 */
public class ValidationCostAudit {

    public static void main(String[] args) throws Exception {
        Args parsed = Args.parse(args);
        if (!parsed.verbose) {
            quietHapiLogs();
        }
        new ValidationCostAudit().run(parsed);
    }

    /**
     * Silence HAPI's chatty INFO-level loggers so audit output isn't drowned by
     * "Fetching CodeSystem for..." and "Loading structure definitions from..." noise.
     * WARN and ERROR still get through so real validator problems remain visible.
     */
    private static void quietHapiLogs() {
        for (String name : List.of(
                "ca.uhn.fhir",
                "org.hl7.fhir",
                "ca.uhn.fhir.log.terminology_troubleshooting",
                "ca.uhn.fhir.parser.LenientErrorHandler")) {
            ((Logger) LoggerFactory.getLogger(name)).setLevel(Level.WARN);
        }
    }

    private void run(Args a) throws Exception {
        System.out.printf("IG:            %s%n", a.ig);
        System.out.printf("Deps dir:      %s%n", a.deps);
        System.out.printf("Bundles dir:   %s%n", a.bundles);
        System.out.printf("Iterations:    %d (1 warmup + %d recorded)%n", a.iterations, a.iterations - 1);
        System.out.println();

        FhirContext ctx = FhirContext.forR4();
        System.out.println("Building validator chain...");
        long tBuildStart = System.nanoTime();
        FhirValidator validator = buildValidator(ctx, a.ig, a.deps);
        double buildMs = (System.nanoTime() - tBuildStart) / 1_000_000.0;
        System.out.printf("  ...validator ready in %.1f ms%n%n", buildMs);

        List<Path> bundleFiles = listBundleFiles(a.bundles);
        System.out.printf("Discovered %d bundle file(s)%n", bundleFiles.size());
        for (Path p : bundleFiles) {
            System.out.printf("  - %s%n", p);
        }
        System.out.println();

        IParser parser = ctx.newJsonParser().setParserErrorHandler(new LenientErrorHandler(false));

        AuditReport report = new AuditReport();
        report.ig = a.ig.toString();
        report.iterations = a.iterations;
        report.warmupDropped = 1;

        for (Path bundlePath : bundleFiles) {
            System.out.printf(">> Auditing %s%n", bundlePath.getFileName());
            Bundle bundle;
            try (InputStream s = Files.newInputStream(bundlePath)) {
                bundle = (Bundle) parser.parseResource(s);
            }
            BundleReport br = auditBundle(validator, bundle, bundlePath.toString(), a.iterations);
            report.bundles.add(br);
            System.out.printf("   entries=%d, samples=%d, total=%.1f ms%n%n",
                    br.entryCount, br.sampleCount, br.totalMs);
        }

        aggregate(report, a.topMessages);

        ObjectMapper mapper = new ObjectMapper().enable(SerializationFeature.INDENT_OUTPUT);
        mapper.setSerializationInclusion(JsonInclude.Include.NON_NULL);
        Files.writeString(a.report, mapper.writeValueAsString(report));
        System.out.printf("Report written: %s%n", a.report);
        printExecutiveSummary(report);
    }

    // ---------- Validator setup ----------

    private FhirValidator buildValidator(FhirContext ctx, Path primaryIg, Path depsDir) throws IOException {
        PrePopulatedValidationSupport packageSupport = new PrePopulatedValidationSupport(ctx);
        loadPackage(ctx, packageSupport, primaryIg);
        if (depsDir != null && Files.isDirectory(depsDir)) {
            try (Stream<Path> stream = Files.list(depsDir)) {
                List<Path> deps = stream
                        .filter(p -> p.getFileName().toString().endsWith(".tgz"))
                        .filter(p -> !p.toAbsolutePath().equals(primaryIg.toAbsolutePath()))
                        .sorted()
                        .toList();
                for (Path dep : deps) {
                    loadPackage(ctx, packageSupport, dep);
                }
            }
        }

        ValidationSupportChain chain = new ValidationSupportChain(
                new DefaultProfileValidationSupport(ctx),
                packageSupport,
                new CommonCodeSystemsTerminologyService(ctx),
                new InMemoryTerminologyServerValidationSupport(ctx),
                new SnapshotGeneratingValidationSupport(ctx));

        CachingValidationSupport caching = new CachingValidationSupport(chain);
        FhirInstanceValidator module = new FhirInstanceValidator(caching);
        FhirValidator validator = new FhirValidator(ctx);
        validator.registerValidatorModule(module);
        return validator;
    }

    private static final Collection<String> LOADABLE_TYPES = List.of(
            ResourceType.CodeSystem.name(),
            ResourceType.ValueSet.name(),
            ResourceType.StructureDefinition.name());

    private void loadPackage(FhirContext ctx, PrePopulatedValidationSupport support, Path tgz) throws IOException {
        NpmPackage pkg;
        try (InputStream stream = Files.newInputStream(tgz)) {
            pkg = NpmPackage.fromPackage(stream);
        }
        IParser parser = ctx.newJsonParser().setParserErrorHandler(new LenientErrorHandler(false));
        int loaded = 0;
        for (String type : LOADABLE_TYPES) {
            for (String file : pkg.listResources(type)) {
                try (InputStream stream = pkg.loadResource(file)) {
                    IBaseResource resource = parser.parseResource(stream);
                    support.addResource(resource);
                    loaded++;
                } catch (Exception ex) {
                    System.err.printf("  ! failed to load %s/%s: %s%n", tgz.getFileName(), file, ex.getMessage());
                }
            }
        }
        System.out.printf("  loaded %s (%s@%s) — %d resources%n",
                tgz.getFileName(), pkg.name(), pkg.version(), loaded);
    }

    // ---------- Audit loop ----------

    private BundleReport auditBundle(FhirValidator validator, Bundle bundle, String path, int iterations) {
        BundleReport br = new BundleReport();
        br.file = path;
        br.entryCount = bundle.getEntry().size();

        for (Bundle.BundleEntryComponent entry : bundle.getEntry()) {
            Resource res = entry.getResource();
            if (res == null) continue;

            List<String> declaredProfiles = res.getMeta().getProfile().stream()
                    .map(CanonicalType::getValue)
                    .filter(v -> v != null && !v.isBlank())
                    .toList();

            List<String> profilesToRun = declaredProfiles.isEmpty()
                    ? List.of("(base)")
                    : declaredProfiles;

            for (String profile : profilesToRun) {
                Resource forValidation = cloneWithSingleProfile(res, profile);
                ValidationOptions options = new ValidationOptions();
                if (!"(base)".equals(profile)) {
                    options.addProfile(profile);
                }

                int lastMessageCount = -1;
                List<MessageRecord> capturedMessages = null;
                for (int i = 0; i < iterations; i++) {
                    long start = System.nanoTime();
                    ValidationResult result = validator.validateWithResult(forValidation, options);
                    long elapsed = System.nanoTime() - start;

                    if (i == 0) {
                        // Warmup iteration — discarded.
                        lastMessageCount = messageCount(result);
                        continue;
                    }

                    Sample s = new Sample();
                    s.resourceType = res.fhirType();
                    s.resourceId = res.getIdPart();
                    s.profile = profile;
                    s.elapsedMs = elapsed / 1_000_000.0;
                    s.messageCount = lastMessageCount;
                    // Capture messages once, on the first recorded iteration only, to avoid
                    // multiplying the counts by the number of recorded iterations.
                    if (capturedMessages == null) {
                        capturedMessages = extractMessages(result);
                        s.messages = capturedMessages;
                    }
                    br.samples.add(s);
                    br.totalMs += s.elapsedMs;
                    br.sampleCount++;
                }
            }
        }
        return br;
    }

    private List<MessageRecord> extractMessages(ValidationResult result) {
        if (result.getMessages() == null || result.getMessages().isEmpty()) {
            return List.of();
        }
        List<MessageRecord> out = new ArrayList<>();
        for (SingleValidationMessage m : result.getMessages()) {
            MessageRecord r = new MessageRecord();
            ResultSeverityEnum sev = m.getSeverity();
            r.severity = sev == null ? null : sev.name();
            r.location = m.getLocationString();
            r.message = m.getMessage();
            out.add(r);
        }
        return out;
    }

    /**
     * Copy the resource and reset {@code meta.profile} to just the one under test so
     * declared profiles on the resource don't get validated alongside the target.
     * Uses HAPI's built-in copy (deep, cheap relative to a JSON round-trip).
     */
    private Resource cloneWithSingleProfile(Resource original, String profile) {
        Resource copy = original.copy();
        copy.getMeta().setProfile(new ArrayList<>());
        if (!"(base)".equals(profile)) {
            copy.getMeta().addProfile(profile);
        }
        return copy;
    }

    private int messageCount(ValidationResult result) {
        return result.getMessages() == null ? 0 : result.getMessages().size();
    }

    // ---------- Aggregation ----------

    private void aggregate(AuditReport report, int topMessages) {
        Map<String, List<Sample>> byProfile = new HashMap<>();
        Map<String, List<Sample>> byResourceType = new HashMap<>();
        for (BundleReport br : report.bundles) {
            for (Sample s : br.samples) {
                byProfile.computeIfAbsent(s.profile, k -> new ArrayList<>()).add(s);
                byResourceType.computeIfAbsent(s.resourceType, k -> new ArrayList<>()).add(s);
            }
        }

        report.byProfile = byProfile.entrySet().stream()
                .map(e -> {
                    ProfileStats ps = new ProfileStats();
                    ps.profile = e.getKey();
                    populate(ps, e.getValue());
                    ps.resourceTypes = e.getValue().stream()
                            .map(s -> s.resourceType)
                            .collect(Collectors.toCollection(java.util.LinkedHashSet::new))
                            .stream().sorted().toList();
                    return ps;
                })
                .sorted(Comparator.comparingDouble((ProfileStats p) -> p.totalMs).reversed())
                .toList();

        report.byResourceType = byResourceType.entrySet().stream()
                .map(e -> {
                    ResourceTypeStats rs = new ResourceTypeStats();
                    rs.resourceType = e.getKey();
                    populate(rs, e.getValue());
                    return rs;
                })
                .sorted(Comparator.comparingDouble((ResourceTypeStats r) -> r.totalMs).reversed())
                .toList();

        report.topMessages = rollupMessages(report, topMessages);
    }

    /**
     * Regexes used to strip volatile substrings from message text so that
     * "Reference 'Patient/123' not found" and "Reference 'Patient/456' not found"
     * collapse into the same bucket. Order matters — more specific patterns first.
     */
    private static final List<Pattern> NORMALIZE_PATTERNS = List.of(
            Pattern.compile("'[^']*'"),           // any single-quoted string
            Pattern.compile("\\b[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}\\b"), // uuid
            Pattern.compile("\\[\\d+\\]"),        // array indexes
            Pattern.compile("\\b\\d+\\b"));       // bare numbers

    private String normalizeMessage(String raw) {
        String out = raw;
        for (Pattern p : NORMALIZE_PATTERNS) {
            out = p.matcher(out).replaceAll("*");
        }
        return out;
    }

    private List<MessageRollup> rollupMessages(AuditReport report, int topN) {
        Map<String, MessageRollup> byKey = new HashMap<>();
        for (BundleReport br : report.bundles) {
            for (Sample s : br.samples) {
                if (s.messages == null) continue;
                for (MessageRecord m : s.messages) {
                    if (m.message == null) continue;
                    String normalized = normalizeMessage(m.message);
                    String key = m.severity + "|" + normalized;
                    MessageRollup rollup = byKey.computeIfAbsent(key, k -> {
                        MessageRollup r = new MessageRollup();
                        r.severity = m.severity;
                        r.pattern = normalized;
                        r.exampleMessage = m.message;
                        r.exampleLocations = new ArrayList<>();
                        r.exampleResources = new ArrayList<>();
                        return r;
                    });
                    rollup.count++;
                    if (rollup.exampleLocations.size() < 3 && m.location != null
                            && !rollup.exampleLocations.contains(m.location)) {
                        rollup.exampleLocations.add(m.location);
                    }
                    String resourceRef = s.resourceType + "/" + s.resourceId;
                    if (rollup.exampleResources.size() < 3
                            && !rollup.exampleResources.contains(resourceRef)) {
                        rollup.exampleResources.add(resourceRef);
                    }
                }
            }
        }
        return byKey.values().stream()
                .sorted(Comparator.comparingInt((MessageRollup r) -> r.count).reversed())
                .limit(topN)
                .toList();
    }

    private void populate(BaseStats stats, List<Sample> samples) {
        double[] ms = samples.stream().mapToDouble(s -> s.elapsedMs).sorted().toArray();
        stats.sampleCount = ms.length;
        stats.totalMs = Arrays.stream(ms).sum();
        stats.meanMs = stats.totalMs / ms.length;
        stats.medianMs = percentile(ms, 50);
        stats.p95Ms = percentile(ms, 95);
        stats.maxMs = ms[ms.length - 1];
        stats.totalMessages = samples.stream().mapToInt(s -> s.messageCount).sum();
    }

    private double percentile(double[] sorted, int pct) {
        if (sorted.length == 0) return 0.0;
        int rank = (int) Math.ceil(pct / 100.0 * sorted.length) - 1;
        return sorted[Math.max(0, Math.min(rank, sorted.length - 1))];
    }

    // ---------- Output ----------

    private void printExecutiveSummary(AuditReport report) {
        System.out.println();
        System.out.println("=== Executive summary ===");
        System.out.printf("Total samples: %d across %d bundle(s)%n",
                report.byProfile.stream().mapToInt(p -> p.sampleCount).sum(),
                report.bundles.size());
        double grandTotal = report.byProfile.stream().mapToDouble(p -> p.totalMs).sum();
        System.out.printf("Grand total time: %.1f ms%n%n", grandTotal);

        System.out.println("Top profiles by total time:");
        report.byProfile.stream().limit(10).forEach(p ->
                System.out.printf("  %8.1f ms  (n=%d, mean=%.1f, p95=%.1f)  %s%n",
                        p.totalMs, p.sampleCount, p.meanMs, p.p95Ms, p.profile));

        System.out.println();
        System.out.println("Top resource types by total time:");
        report.byResourceType.stream().limit(10).forEach(r ->
                System.out.printf("  %8.1f ms  (n=%d, mean=%.1f, p95=%.1f)  %s%n",
                        r.totalMs, r.sampleCount, r.meanMs, r.p95Ms, r.resourceType));

        if (report.topMessages != null && !report.topMessages.isEmpty()) {
            System.out.println();
            System.out.println("Top validation messages (normalized, deduped):");
            report.topMessages.stream().limit(10).forEach(m -> {
                String snippet = m.exampleMessage.length() > 140
                        ? m.exampleMessage.substring(0, 137) + "..."
                        : m.exampleMessage;
                System.out.printf("  x%-5d %-8s  %s%n", m.count, m.severity, snippet);
            });
        }
    }

    // ---------- Bundle discovery ----------

    private List<Path> listBundleFiles(Path bundlesDir) throws IOException {
        try (Stream<Path> walked = Files.walk(bundlesDir)) {
            return walked
                    .filter(Files::isRegularFile)
                    .filter(p -> p.getFileName().toString().endsWith("-bundle.json"))
                    .filter(p -> !p.toString().contains("-files/"))
                    .sorted()
                    .toList();
        }
    }

    // ---------- Report DTOs ----------

    @JsonInclude(JsonInclude.Include.NON_NULL)
    public static class AuditReport {
        public String ig;
        public int iterations;
        public int warmupDropped;
        public List<BundleReport> bundles = new ArrayList<>();
        public List<ProfileStats> byProfile;
        public List<ResourceTypeStats> byResourceType;
        public List<MessageRollup> topMessages;
    }

    public static class BundleReport {
        public String file;
        public int entryCount;
        public int sampleCount;
        public double totalMs;
        public List<Sample> samples = new ArrayList<>();
    }

    @JsonInclude(JsonInclude.Include.NON_NULL)
    public static class Sample {
        public String resourceType;
        public String resourceId;
        public String profile;
        public double elapsedMs;
        public int messageCount;
        public List<MessageRecord> messages;
    }

    @JsonInclude(JsonInclude.Include.NON_NULL)
    public static class MessageRecord {
        public String severity;
        public String location;
        public String message;
    }

    @JsonInclude(JsonInclude.Include.NON_NULL)
    public static class MessageRollup {
        public int count;
        public String severity;
        public String pattern;
        public String exampleMessage;
        public List<String> exampleLocations;
        public List<String> exampleResources;
    }

    public static class BaseStats {
        public int sampleCount;
        public double totalMs;
        public double meanMs;
        public double medianMs;
        public double p95Ms;
        public double maxMs;
        public int totalMessages;
    }

    public static class ProfileStats extends BaseStats {
        public String profile;
        public List<String> resourceTypes;
    }

    public static class ResourceTypeStats extends BaseStats {
        public String resourceType;
    }

    // ---------- CLI args ----------

    private static class Args {
        Path ig;
        Path deps;
        Path bundles;
        Path report = Paths.get("validation-cost-report.json");
        int iterations = 3;
        boolean verbose = false;
        int topMessages = 20;

        static Args parse(String[] argv) {
            Args a = new Args();
            for (int i = 0; i < argv.length; i++) {
                String flag = argv[i];
                switch (flag) {
                    case "--verbose", "-v" -> a.verbose = true;
                    default -> {
                        String value = (i + 1 < argv.length) ? argv[++i] : null;
                        switch (flag) {
                            case "--ig" -> a.ig = Paths.get(value);
                            case "--deps" -> a.deps = Paths.get(value);
                            case "--bundles" -> a.bundles = Paths.get(value);
                            case "--iterations" -> a.iterations = Integer.parseInt(value);
                            case "--report" -> a.report = Paths.get(value);
                            case "--top-messages" -> a.topMessages = Integer.parseInt(value);
                            default -> throw new IllegalArgumentException("Unknown flag: " + flag);
                        }
                    }
                }
            }
            if (a.ig == null || a.bundles == null) {
                throw new IllegalArgumentException("--ig and --bundles are required");
            }
            if (a.iterations < 2) {
                throw new IllegalArgumentException("--iterations must be >= 2 (1 warmup + >=1 recorded)");
            }
            return a;
        }
    }
}
