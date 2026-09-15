package com.lantanagroup.link.validation.demo;

import ca.uhn.fhir.context.FhirContext;
import ca.uhn.fhir.context.support.DefaultProfileValidationSupport;
import ca.uhn.fhir.parser.IParser;
import ca.uhn.fhir.parser.LenientErrorHandler;
import ca.uhn.fhir.validation.FhirValidator;
import ca.uhn.fhir.validation.ResultSeverityEnum;
import ca.uhn.fhir.validation.SingleValidationMessage;
import ca.uhn.fhir.validation.ValidationResult;
import ch.qos.logback.classic.Level;
import ch.qos.logback.classic.Logger;
import org.hl7.fhir.common.hapi.validation.support.CachingValidationSupport;
import org.hl7.fhir.common.hapi.validation.support.CommonCodeSystemsTerminologyService;
import org.hl7.fhir.common.hapi.validation.support.InMemoryTerminologyServerValidationSupport;
import org.hl7.fhir.common.hapi.validation.support.SnapshotGeneratingValidationSupport;
import org.hl7.fhir.common.hapi.validation.support.ValidationSupportChain;
import org.hl7.fhir.common.hapi.validation.validator.FhirInstanceValidator;
import org.hl7.fhir.r4.model.Bundle;
import org.hl7.fhir.r4.model.DiagnosticReport;
import org.hl7.fhir.r4.model.Encounter;
import org.hl7.fhir.r4.model.Location;
import org.hl7.fhir.r4.model.MedicationRequest;
import org.hl7.fhir.r4.model.Observation;
import org.hl7.fhir.r4.model.Resource;
import org.hl7.fhir.r5.utils.validation.IValidationPolicyAdvisor;

import java.io.InputStream;
import java.nio.file.Files;
import java.nio.file.Paths;
import java.util.ArrayList;
import java.util.HashMap;
import java.util.List;
import java.util.Map;

/**
 * Standalone runnable that demonstrates what wiring HAPI's {@code policyForElement} gives us
 * that {@code policyForCodedContent} alone does not. Validates the same resources twice —
 * baseline (no advisor) and with a {@link DemoPolicyAdvisor} carrying three rules that skip
 * cardinality checks on named elements — and prints a side-by-side comparison of message
 * counts and wall time.
 *
 * <p>By default the runner uses a synthetic in-memory fixture whose resources deliberately
 * omit required base-FHIR fields (Encounter.status, MedicationRequest.intent, etc.) so the
 * baseline pass fires a predictable set of "minimum required = 1, but only found 0"
 * messages. Pass {@code --bundle <path>} to run against a real FHIR bundle instead.
 *
 * <p>Companion doc: {@code scratch/policy-for-element-opportunity.md}. Full README:
 * {@code Java/validation/POLICY-FOR-ELEMENT-DEMO.md}.
 *
 * <h4>Running it</h4>
 * <pre>
 *   mvn -pl validation exec:java \
 *     -Dexec.mainClass=com.lantanagroup.link.validation.demo.PolicyForElementDemo
 *   # optional flags:
 *   #   --bundle &lt;path.json&gt;    validate this bundle instead of the synthetic fixture
 *   #   --iterations N            timing iterations (default 3; first is warmup)
 *   #   --verbose                 keep HAPI's INFO chatter (default: silenced)
 * </pre>
 */
public class PolicyForElementDemo {

    public static void main(String[] args) throws Exception {
        Args a = Args.parse(args);
        if (!a.verbose) silenceHapiLogs();
        new PolicyForElementDemo().run(a);
    }

    private void run(Args a) throws Exception {
        FhirContext ctx = FhirContext.forR4();

        List<Resource> subjects = loadSubjects(ctx, a);
        System.out.printf("Subjects: %d resource(s)%s%n", subjects.size(),
                a.bundlePath != null ? " (from " + a.bundlePath + ")" : " (synthetic fixture)");

        // ---- Build two validators sharing the same support chain, so the *only* difference
        //      is whether the DemoPolicyAdvisor is attached.
        ValidationSupportChain chain = new ValidationSupportChain(
                new DefaultProfileValidationSupport(ctx),
                new CommonCodeSystemsTerminologyService(ctx),
                new InMemoryTerminologyServerValidationSupport(ctx),
                new SnapshotGeneratingValidationSupport(ctx));
        CachingValidationSupport caching = new CachingValidationSupport(chain);

        FhirValidator baseline = buildValidator(ctx, caching, null);

        List<DemoPolicyAdvisor.Rule> rules = buildDemoRules();
        DemoPolicyAdvisor advisor = new DemoPolicyAdvisor(rules);
        FhirValidator treated = buildValidator(ctx, caching, advisor);

        // Warmup + timed iterations. First is discarded so both validators pay the same
        // one-time class-loading / snapshot-generation cost before we start measuring.
        System.out.printf("Running %d iteration(s), first is warmup%n%n", a.iterations);
        Run baselineRun = timedRun("Baseline (no advisor)", baseline, subjects, a.iterations);
        Run treatedRun = timedRun("With policyForElement", treated, subjects, a.iterations);

        printComparison(baselineRun, treatedRun, advisor);
    }

    // ---------- validator wiring ----------

    private FhirValidator buildValidator(FhirContext ctx, CachingValidationSupport caching,
                                         IValidationPolicyAdvisor advisor) {
        FhirInstanceValidator module = new FhirInstanceValidator(caching);
        if (advisor != null) {
            module.setValidatorPolicyAdvisor(advisor);
        }
        FhirValidator validator = new FhirValidator(ctx);
        validator.registerValidatorModule(module);
        return validator;
    }

    /**
     * The demo rules. Each names an element path and the {@link IValidationPolicyAdvisor.ElementValidationAction}
     * to exclude — HAPI won't run that action on that element. Cardinality is the most legible
     * demonstration because the "minimum required = 1, but only found 0" message is instantly
     * recognisable in the baseline output. All three paths are base-FHIR R4 required fields
     * (min=1), so each rule silences one message per instance that omits the field.
     */
    private List<DemoPolicyAdvisor.Rule> buildDemoRules() {
        return List.of(
                DemoPolicyAdvisor.Rule.of("demo_encounter_status_cardinality",
                        "Encounter\\.status",
                        IValidationPolicyAdvisor.ElementValidationAction.Cardinality),
                DemoPolicyAdvisor.Rule.of("demo_medicationrequest_intent_cardinality",
                        "MedicationRequest\\.intent",
                        IValidationPolicyAdvisor.ElementValidationAction.Cardinality),
                DemoPolicyAdvisor.Rule.of("demo_observation_code_cardinality",
                        "Observation\\.code",
                        IValidationPolicyAdvisor.ElementValidationAction.Cardinality));
    }

    // ---------- subject loading ----------

    private List<Resource> loadSubjects(FhirContext ctx, Args a) throws Exception {
        if (a.bundlePath != null) {
            IParser parser = ctx.newJsonParser().setParserErrorHandler(new LenientErrorHandler(false));
            try (InputStream in = Files.newInputStream(Paths.get(a.bundlePath))) {
                Bundle bundle = (Bundle) parser.parseResource(in);
                List<Resource> out = new ArrayList<>();
                for (Bundle.BundleEntryComponent entry : bundle.getEntry()) {
                    if (entry.getResource() != null) out.add(entry.getResource());
                }
                return out;
            }
        }
        return syntheticFixture();
    }

    /**
     * Builds a small collection of resources that deliberately omit required base-FHIR fields
     * so the baseline validation pass fires a predictable, repeatable set of cardinality
     * violations. Each demo rule targets a field one of these resources omits, so every rule
     * has at least one hit; other omitted-required fields are left uncovered so the treated
     * pass still emits messages (proof the rules are targeted, not a blanket suppressor).
     */
    private List<Resource> syntheticFixture() {
        List<Resource> subjects = new ArrayList<>();

        // Encounter: base FHIR requires .status. Rule silences it → baseline emits the
        // "Encounter.status: minimum required = 1, but only found 0" message, treated does not.
        Encounter e = new Encounter();
        e.setId("enc-missing-status");
        subjects.add(e);

        // MedicationRequest: base FHIR requires .status, .intent, .medication, .subject.
        // Rule silences .intent only. Baseline emits 4 cardinality errors; treated emits 3
        // (the other three fields are not named by any rule).
        MedicationRequest mr = new MedicationRequest();
        mr.setId("mr-missing-intent");
        subjects.add(mr);

        // Observation: base FHIR requires .status and .code. Rule silences .code. Baseline
        // emits 2 cardinality errors; treated emits 1 (.status still fires).
        Observation obs = new Observation();
        obs.setId("obs-missing-code-and-status");
        subjects.add(obs);

        // DiagnosticReport: base FHIR requires .status and .code. Not covered by any demo
        // rule — its cardinality errors appear identically in both passes, providing a
        // control group to prove the advisor doesn't affect unrelated resources.
        DiagnosticReport dr = new DiagnosticReport();
        dr.setId("dr-missing-code-and-status");
        subjects.add(dr);

        // Location: base FHIR requires no fields. Included as a well-formed control — should
        // add zero messages in either pass, regardless of the advisor.
        Location loc = new Location();
        loc.setId("loc-well-formed");
        loc.setName("General Ward");
        subjects.add(loc);

        return subjects;
    }

    // ---------- timing / measurement ----------

    private record Run(String label, long elapsedMs, int totalMessages,
                       Map<String, Integer> messagesBySeverity, List<String> sampleMessages) {
    }

    private Run timedRun(String label, FhirValidator validator, List<Resource> subjects, int iterations) {
        long bestElapsed = Long.MAX_VALUE;
        int totalMessages = 0;
        Map<String, Integer> bySeverity = new HashMap<>();
        List<String> sampleMessages = new ArrayList<>();

        for (int i = 0; i < iterations; i++) {
            long start = System.nanoTime();
            int msgCount = 0;
            for (Resource r : subjects) {
                ValidationResult result = validator.validateWithResult(r);
                msgCount += result.getMessages().size();
                // Only capture the details on the final iteration to avoid mid-warmup noise.
                if (i == iterations - 1) {
                    for (SingleValidationMessage m : result.getMessages()) {
                        String sev = severityLabel(m.getSeverity());
                        bySeverity.merge(sev, 1, Integer::sum);
                        if (sampleMessages.size() < 5) {
                            sampleMessages.add(String.format("%s  %s: %s",
                                    sev, m.getLocationString(), truncate(m.getMessage(), 90)));
                        }
                    }
                }
            }
            long elapsed = (System.nanoTime() - start) / 1_000_000;
            // Skip iteration 0 as warmup — HAPI does one-time snapshot generation on first
            // touch of each StructureDefinition and it'd swamp real per-call cost.
            if (i > 0) {
                bestElapsed = Math.min(bestElapsed, elapsed);
                if (i == iterations - 1) totalMessages = msgCount;
            }
        }
        return new Run(label, bestElapsed, totalMessages, bySeverity, sampleMessages);
    }

    // ---------- output ----------

    private void printComparison(Run baseline, Run treated, DemoPolicyAdvisor advisor) {
        System.out.println("┌────────────────────────────────┬──────────────┬──────────────┐");
        System.out.printf("│ %-30s │ %12s │ %12s │%n", "", "Baseline", "Treated");
        System.out.println("├────────────────────────────────┼──────────────┼──────────────┤");
        System.out.printf("│ %-30s │ %12d │ %12d │%n", "Total messages", baseline.totalMessages(), treated.totalMessages());
        System.out.printf("│ %-30s │ %12d │ %12d │%n", "Best iteration (ms)", baseline.elapsedMs(), treated.elapsedMs());
        System.out.println("└────────────────────────────────┴──────────────┴──────────────┘");

        int msgDelta = baseline.totalMessages() - treated.totalMessages();
        long msDelta = baseline.elapsedMs() - treated.elapsedMs();
        double msgPct = baseline.totalMessages() == 0 ? 0 : 100.0 * msgDelta / baseline.totalMessages();
        double msPct = baseline.elapsedMs() == 0 ? 0 : 100.0 * msDelta / baseline.elapsedMs();
        System.out.printf("%nDelta: −%d messages (%.1f%%), −%d ms (%.1f%%)%n%n",
                msgDelta, msgPct, msDelta, msPct);

        System.out.println("Rule hits (advisor invocations that fired):");
        advisor.getRuleHitCounts().forEach((id, hits) -> {
            String label = hits == 0 ? "(no matches in these subjects)" : hits + " element visit(s) affected";
            System.out.printf("  %-45s %s%n", id, label);
        });

        System.out.println("\nSample of treated-pass messages (first 5):");
        if (treated.sampleMessages().isEmpty()) {
            System.out.println("  (none — every validation error was silenced)");
        } else {
            for (String m : treated.sampleMessages()) System.out.printf("  %s%n", m);
        }

        System.out.println("\nWhat to look at:");
        System.out.println("  * Messages that disappeared: Encounter.status, MedicationRequest.intent,");
        System.out.println("    Observation.code — HAPI never ran the cardinality check on those paths in");
        System.out.println("    the treated pass because the demo rules named them.");
        System.out.println("  * Messages that stayed: MedicationRequest.status/.medication/.subject,");
        System.out.println("    Observation.status, DiagnosticReport.status/.code — those elements are NOT");
        System.out.println("    named by any demo rule. The advisor was invoked on them but returned HAPI's");
        System.out.println("    default action set, so cardinality still ran.");
        System.out.println("  * DiagnosticReport gets zero suppressions: no demo rule mentions any of its");
        System.out.println("    paths. Proof the advisor is targeted, not a blanket suppressor.");
        System.out.println("  * Location adds zero messages either way: no required-field violations, no");
        System.out.println("    advisor firings that would silence anything.");
    }

    // ---------- helpers ----------

    private static String severityLabel(ResultSeverityEnum s) {
        return s == null ? "UNKNOWN" : s.name();
    }

    private static String truncate(String s, int n) {
        if (s == null) return "";
        return s.length() <= n ? s : s.substring(0, n - 3) + "...";
    }

    /**
     * HAPI's INFO chatter ("Fetching CodeSystem ...", "Loading structure definitions ...") drowns
     * the demo output. Silence it unless {@code --verbose} was passed.
     */
    private static void silenceHapiLogs() {
        for (String name : List.of("ca.uhn.fhir", "org.hl7.fhir")) {
            Logger l = (Logger) org.slf4j.LoggerFactory.getLogger(name);
            l.setLevel(Level.WARN);
        }
    }

    // ---------- CLI args ----------

    private static class Args {
        String bundlePath;
        int iterations = 3;
        boolean verbose = false;

        static Args parse(String[] argv) {
            Args a = new Args();
            for (int i = 0; i < argv.length; i++) {
                String flag = argv[i];
                switch (flag) {
                    case "--bundle" -> a.bundlePath = argv[++i];
                    case "--iterations" -> a.iterations = Integer.parseInt(argv[++i]);
                    case "--verbose", "-v" -> a.verbose = true;
                    default -> throw new IllegalArgumentException("Unknown flag: " + flag);
                }
            }
            if (a.iterations < 2) {
                throw new IllegalArgumentException("--iterations must be >= 2 (first is warmup)");
            }
            return a;
        }
    }
}
