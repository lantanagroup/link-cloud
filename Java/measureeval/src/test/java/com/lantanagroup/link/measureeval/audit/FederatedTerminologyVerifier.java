package com.lantanagroup.link.measureeval.audit;

import ca.uhn.fhir.context.FhirContext;
import ca.uhn.fhir.parser.IParser;
import ca.uhn.fhir.parser.LenientErrorHandler;
import ca.uhn.fhir.rest.client.api.IGenericClient;
import com.github.tomakehurst.wiremock.WireMockServer;
import com.lantanagroup.link.measureeval.models.DebugSections;
import com.lantanagroup.link.measureeval.models.MeasureEvaluationResult;
import com.lantanagroup.link.measureeval.services.MeasureEvaluator;
import org.hl7.fhir.instance.model.api.IBaseResource;
import org.hl7.fhir.r4.model.Bundle;
import org.hl7.fhir.r4.model.DateTimeType;
import org.hl7.fhir.r4.model.MeasureReport;
import org.hl7.fhir.r4.model.Parameters;
import org.hl7.fhir.r4.model.Patient;
import org.hl7.fhir.r4.model.StringType;
import org.hl7.fhir.r4.model.ValueSet;

import java.io.InputStream;
import java.nio.file.Files;
import java.nio.file.Path;
import java.nio.file.Paths;
import java.util.ArrayList;
import java.util.EnumSet;
import java.util.List;
import java.util.stream.Stream;

import static com.github.tomakehurst.wiremock.client.WireMock.aResponse;
import static com.github.tomakehurst.wiremock.client.WireMock.equalTo;
import static com.github.tomakehurst.wiremock.client.WireMock.get;
import static com.github.tomakehurst.wiremock.client.WireMock.urlPathEqualTo;
import static com.github.tomakehurst.wiremock.core.WireMockConfiguration.wireMockConfig;

/**
 * Manual verifier for the federated-terminology wire-up in {@link MeasureEvaluator}. Point it at
 * a real IG measure bundle (Measure + Library + ValueSets) and a directory of subject bundles;
 * it runs three scenarios per subject and reports whether the results agree and whether the mock
 * terminology server was consulted the way we expect.
 *
 * <p>Scenarios (per subject bundle):
 * <ol>
 *   <li><b>No federation</b> — evaluate against the full measure bundle, no remote client.
 *       Captures the baseline initial-population count.</li>
 *   <li><b>Federation on, full bundle</b> — evaluate again with a remote client pointed at
 *       WireMock. Bundle carries all the ValueSets it needs, so WireMock should record zero
 *       requests. Results must match scenario 1.</li>
 *   <li><b>Federation on, stripped bundle</b> — remove every ValueSet from the bundle before
 *       evaluating; WireMock is pre-stubbed to serve those same ValueSets. Results must match
 *       scenario 1; WireMock records requests for the stripped ValueSets.</li>
 * </ol>
 *
 * <p>Not committed as a JUnit test because it depends on large IG bundles that don't belong in
 * the repo. Run manually against local NHSN measure bundles when verifying the wire-up under
 * production-shaped conditions. See {@code Java/measureeval/FEDERATION-VERIFICATION.md} for
 * the invocation and interpretation procedure.
 *
 * <p>Run via:
 * <pre>
 *   mvn -pl measureeval exec:java -Dexec.classpathScope=test \
 *     -Dexec.mainClass=com.lantanagroup.link.measureeval.audit.FederatedTerminologyVerifier \
 *     -Dexec.args="--measure-bundle /path/to/measure-bundle.json \
 *                  --subjects-dir /path/to/subject/bundles \
 *                  --period-start 2024-01-01 --period-end 2024-12-31"
 * </pre>
 */
public class FederatedTerminologyVerifier {

    public static void main(String[] args) throws Exception {
        Args a = Args.parse(args);
        new FederatedTerminologyVerifier().run(a);
    }

    private void run(Args a) throws Exception {
        FhirContext ctx = FhirContext.forR4();
        IParser parser = ctx.newJsonParser().setParserErrorHandler(new LenientErrorHandler(false));

        System.out.printf("Measure bundle: %s%n", a.measureBundle);
        System.out.printf("Subjects dir:   %s%n", a.subjectsDir);
        System.out.printf("Mock TS port:   %d%n", a.tsPort);
        System.out.printf("Period:         %s .. %s%n%n", a.periodStart, a.periodEnd);

        Bundle measureBundle = loadBundle(a.measureBundle, parser);
        List<ValueSet> valueSets = extractValueSets(measureBundle);
        Bundle strippedBundle = stripValueSets(measureBundle);
        System.out.printf("Measure bundle carries %d ValueSet(s); stripped variant contains %d entries%n",
                valueSets.size(), strippedBundle.getEntry().size());

        List<Path> subjectPaths = listSubjectBundles(a.subjectsDir);
        System.out.printf("Found %d subject bundle(s):%n", subjectPaths.size());
        subjectPaths.forEach(p -> System.out.printf("  - %s%n", p.getFileName()));
        System.out.println();

        WireMockServer wm = new WireMockServer(wireMockConfig().port(a.tsPort));
        wm.start();
        int passed = 0;
        int total = 0;
        try {
            stubValueSets(wm, valueSets, ctx);
            IGenericClient client = ctx.newRestfulGenericClient("http://localhost:" + a.tsPort);

            printHeader();
            for (Path subjectPath : subjectPaths) {
                Bundle subjectBundle = loadBundle(subjectPath, parser);
                String subjectRef = extractPatientReference(subjectBundle);
                boolean pass = verifySubject(ctx, measureBundle, strippedBundle, subjectBundle,
                        subjectRef, subjectPath.getFileName().toString(), client, wm, a);
                if (pass) passed++;
                total++;
            }
            printFooter(passed, total);
        } finally {
            wm.stop();
        }
    }

    // ---------- per-subject verification ----------

    private boolean verifySubject(
            FhirContext ctx,
            Bundle fullBundle,
            Bundle strippedBundle,
            Bundle subjectBundle,
            String subjectRef,
            String subjectFileName,
            IGenericClient client,
            WireMockServer wm,
            Args a) {
        // Scenario A: no federation, full bundle
        wm.resetRequests();
        int countA = runAndCount(ctx, fullBundle, subjectRef, subjectBundle, null, a);
        int callsA = wm.getAllServeEvents().size();  // sanity: should be 0

        // Scenario B: federation on, full bundle — remote should NOT be consulted
        wm.resetRequests();
        int countB = runAndCount(ctx, fullBundle, subjectRef, subjectBundle, client, a);
        int callsB = wm.getAllServeEvents().size();

        // Scenario C: federation on, stripped bundle — remote MUST be consulted
        wm.resetRequests();
        int countC = runAndCount(ctx, strippedBundle, subjectRef, subjectBundle, client, a);
        int callsC = wm.getAllServeEvents().size();

        boolean resultsMatch = countA == countB && countB == countC;
        boolean bWasSilent = callsB == 0;
        boolean cUsedRemote = callsC > 0;
        boolean pass = resultsMatch && bWasSilent && cUsedRemote;

        String verdict = pass ? "✓ PASS"
                : (!resultsMatch ? "✗ FAIL results diverge"
                : !bWasSilent ? "✗ FAIL scenario B hit remote"
                : "✗ FAIL scenario C did not use remote");

        System.out.printf("  %-45s | %-4d | %-4d | %-4d | %-4d | %-4d | %s%n",
                truncate(subjectFileName, 45), countA, countB, countC, callsB, callsC, verdict);
        return pass;
    }

    private int runAndCount(
            FhirContext ctx,
            Bundle measureBundle,
            String subjectRef,
            Bundle subjectBundle,
            IGenericClient client,
            Args a) {
        Parameters params = new Parameters();
        params.addParameter().setName("periodStart").setValue(new DateTimeType(a.periodStart));
        params.addParameter().setName("periodEnd").setValue(new DateTimeType(a.periodEnd));
        params.addParameter().setName("subject").setValue(new StringType(subjectRef));
        params.addParameter().setName("additionalData").setResource(subjectBundle);

        try {
            MeasureEvaluationResult result = MeasureEvaluator.compileAndEvaluate(
                    ctx, measureBundle, params, EnumSet.noneOf(DebugSections.class), client);
            MeasureReport report = result.getMeasureReport();
            if (report.getGroup().isEmpty()) return -1;
            var pop = report.getGroupFirstRep().getPopulationFirstRep();
            return pop != null ? pop.getCount() : -1;
        } catch (Exception ex) {
            System.err.printf("    ! evaluation error: %s%n", ex.getMessage());
            return -1;
        }
    }

    // ---------- fixture helpers ----------

    private Bundle loadBundle(Path path, IParser parser) throws Exception {
        try (InputStream in = Files.newInputStream(path)) {
            return (Bundle) parser.parseResource(in);
        }
    }

    private List<ValueSet> extractValueSets(Bundle bundle) {
        return bundle.getEntry().stream()
                .map(Bundle.BundleEntryComponent::getResource)
                .filter(ValueSet.class::isInstance)
                .map(ValueSet.class::cast)
                .toList();
    }

    private Bundle stripValueSets(Bundle bundle) {
        Bundle stripped = new Bundle();
        stripped.setType(bundle.getType());
        for (Bundle.BundleEntryComponent entry : bundle.getEntry()) {
            if (!(entry.getResource() instanceof ValueSet)) {
                stripped.addEntry(entry.copy());
            }
        }
        return stripped;
    }

    private void stubValueSets(WireMockServer wm, List<ValueSet> valueSets, FhirContext ctx) {
        IParser parser = ctx.newJsonParser();
        for (ValueSet vs : valueSets) {
            if (vs.getUrl() == null) continue;
            Bundle searchset = new Bundle();
            searchset.setType(Bundle.BundleType.SEARCHSET);
            searchset.addEntry().setResource(vs);
            wm.stubFor(get(urlPathEqualTo("/ValueSet"))
                    .withQueryParam("url", equalTo(vs.getUrl()))
                    .willReturn(aResponse()
                            .withStatus(200)
                            .withHeader("Content-Type", "application/fhir+json")
                            .withBody(parser.encodeResourceToString(searchset))));
        }
    }

    private String extractPatientReference(Bundle bundle) {
        return bundle.getEntry().stream()
                .map(Bundle.BundleEntryComponent::getResource)
                .filter(Patient.class::isInstance)
                .map(IBaseResource::getIdElement)
                .findFirst()
                .map(id -> "Patient/" + id.getIdPart())
                .orElse("Patient/unknown");
    }

    private List<Path> listSubjectBundles(Path subjectsDir) throws Exception {
        try (Stream<Path> stream = Files.list(subjectsDir)) {
            return stream
                    .filter(Files::isRegularFile)
                    .filter(p -> p.getFileName().toString().endsWith(".json"))
                    .filter(p -> p.getFileName().toString().contains("subject"))
                    .sorted()
                    .toList();
        }
    }

    // ---------- output ----------

    private void printHeader() {
        System.out.println("Subject                                       | A    | B    | C    | Bcalls | Ccalls | Verdict");
        System.out.println("----------------------------------------------|------|------|------|--------|--------|--------");
    }

    private void printFooter(int passed, int total) {
        System.out.println();
        System.out.printf("Overall: %d/%d PASS%n%n", passed, total);
        System.out.println("Legend:");
        System.out.println("  A     = initial-population, no federation, full bundle");
        System.out.println("  B     = initial-population, federation on, full bundle (must equal A)");
        System.out.println("  C     = initial-population, federation on, stripped bundle (must equal A)");
        System.out.println("  Bcalls= mock TS requests during scenario B (must be 0)");
        System.out.println("  Ccalls= mock TS requests during scenario C (must be > 0)");
    }

    private String truncate(String s, int n) {
        return s.length() <= n ? s : s.substring(0, n - 3) + "...";
    }

    // ---------- CLI args ----------

    private static class Args {
        Path measureBundle;
        Path subjectsDir;
        int tsPort = 8089;
        String periodStart = "2024-01-01";
        String periodEnd = "2024-12-31";

        static Args parse(String[] argv) {
            Args a = new Args();
            for (int i = 0; i < argv.length; i++) {
                String flag = argv[i];
                String value = (i + 1 < argv.length) ? argv[++i] : null;
                switch (flag) {
                    case "--measure-bundle" -> a.measureBundle = Paths.get(value);
                    case "--subjects-dir" -> a.subjectsDir = Paths.get(value);
                    case "--ts-port" -> a.tsPort = Integer.parseInt(value);
                    case "--period-start" -> a.periodStart = value;
                    case "--period-end" -> a.periodEnd = value;
                    default -> throw new IllegalArgumentException("Unknown flag: " + flag);
                }
            }
            if (a.measureBundle == null || a.subjectsDir == null) {
                throw new IllegalArgumentException("--measure-bundle and --subjects-dir are required");
            }
            return a;
        }
    }
}
