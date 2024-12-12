package com.lantanagroup.link.measureeval;

import ca.uhn.fhir.context.FhirContext;
import com.lantanagroup.link.measureeval.services.MeasureEvaluator;
import com.lantanagroup.link.measureeval.utils.StreamUtils;
import org.apache.commons.io.FileUtils;
import org.apache.commons.io.FilenameUtils;
import org.hl7.fhir.r4.model.*;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;

import java.io.File;
import java.io.IOException;
import java.util.ArrayList;
import java.util.HashSet;
import java.util.List;

/**
 * This class is used to invoke measure evaluation using arbitrary artifacts on the file system for ease of testing and debugging new measures.
 * See the main measureeval project's README for more details about how to execute the JAR from the command line.
 * Careful when specifying file paths to use forward-slash instead of windows' backslash, as that will cause java to interpret the backslash as an escape character and not as a path separator, leading to potentially incorrect parameter interpretation.
 * The `start` and `end` parameters must be in a valid DateTime format from the FHIR specification.
 * The `measure-bundle-path` can be a path to a single measure bundle file or a directory containing each of the resources needed for the measure.
 * The `patient-bundle-path` must be a path to a single (JSON or XML) Bundle file or a directory of files containing the patient data to be used in the evaluation.
 * The response from the operation is the MeasureReport resource being printed to the console in JSON format.
 */
public class FileSystemInvocation {
    private static final FhirContext fhirContext = FhirContext.forR4Cached();
    private static final Logger logger = LoggerFactory.getLogger(FileSystemInvocation.class);

    private static Bundle getBundle(String measureBundlePath) throws IOException {
        logger.info("Loading measure bundle from: {}", measureBundlePath);

        try {
            File measureBundleFile = new File(measureBundlePath);

            if (!measureBundleFile.exists()) {
                throw new IllegalArgumentException("Measure bundle file does not exist: " + measureBundlePath);
            }

            if (measureBundleFile.isFile()) {
                String measureBundleContent = FileUtils.readFileToString(measureBundleFile, "UTF-8");
                if (measureBundlePath.toLowerCase().endsWith(".json")) {
                    return fhirContext.newJsonParser().parseResource(Bundle.class, measureBundleContent);
                } else if (measureBundlePath.toLowerCase().endsWith(".xml")) {
                    return fhirContext.newXmlParser().parseResource(Bundle.class, measureBundleContent);
                } else {
                    throw new IllegalArgumentException("Unsupported measure bundle file format: " + measureBundlePath);
                }
            } else {
                // Parse and load each file in the directory
                Bundle bundle = new Bundle();
                bundle.setType(Bundle.BundleType.COLLECTION);

                File[] files = measureBundleFile.listFiles();

                if (files != null) {
                    HashSet<String> loaded = new HashSet<>();

                    for (File file : files) {
                        String filePath = file.getAbsolutePath();
                        String fileExtension = file.isFile() ? FilenameUtils.getExtension(filePath) : null;

                        if (file.isFile() && (fileExtension.equalsIgnoreCase("json") || fileExtension.equalsIgnoreCase("xml"))) {
                            String fileName = FilenameUtils.getBaseName(filePath);

                            if (loaded.contains(fileName)) {
                                logger.warn("Skipping duplicate file: {}", filePath);
                            }

                            Resource resource;

                            if (filePath.endsWith(".json")) {
                                resource = (Resource) fhirContext.newJsonParser().parseResource(FileUtils.readFileToString(file, "UTF-8"));
                            } else {
                                resource = (Resource) fhirContext.newXmlParser().parseResource(FileUtils.readFileToString(file, "UTF-8"));
                            }

                            bundle.addEntry(new Bundle.BundleEntryComponent().setResource(resource));
                            loaded.add(fileName);
                        } else {
                            logger.warn("Skipping file: {}", filePath);
                        }
                    }
                }

                logger.info("Loaded " + bundle.getEntry().size() + " resources from directory: " + measureBundlePath);

                return bundle;
            }
        } catch (IOException ex) {
            logger.error("Error occurred while loading measure bundle: {}", ex.getMessage());
            throw ex;
        }
    }


    public static List<Bundle> getBundlesFromDirectoryAndSubDirectories(String directory) {
        List<Bundle> bundles = new ArrayList<>();
        File[] files = new File(directory).listFiles();
        if (files != null) {
            for (File file : files) {
                if (file.isDirectory()) {
                    bundles.addAll(getBundlesFromDirectoryAndSubDirectories(file.getAbsolutePath()));
                } else {
                    try {
                        if (!file.getAbsolutePath().toLowerCase().endsWith(".json") && !file.getAbsolutePath().toLowerCase().endsWith(".xml")) {
                            continue;
                        }
                        Bundle bundle = getBundle(file.getAbsolutePath());
                        bundles.add(bundle);
                    } catch (IOException e) {
                        System.err.println("Error occurred while loading bundle: " + e.getMessage());
                        continue;
                    }
                }
            }
        }
        return bundles;
    }

    public static String getGroupPopulations(MeasureReport measureReport) {
        StringBuilder populations = new StringBuilder();
        for (MeasureReport.MeasureReportGroupComponent group : measureReport.getGroup()) {
            populations.append("Group: ").append(group.getId()).append("\n");
            for (MeasureReport.MeasureReportGroupPopulationComponent population : group.getPopulation()) {
                populations.append("Population: ").append(population.getCode().getCodingFirstRep().getDisplay()).append(" - ").append(population.getCount()).append("\n");
            }
        }
        return populations.toString();
    }

    private static Patient findPatient(Bundle bundle) {
        return bundle.getEntry().stream()
                .filter(e -> e.getResource() instanceof Patient)
                .map(e -> (Patient) e.getResource())
                .reduce(StreamUtils::toOnlyElement)
                .orElseThrow(() -> new IllegalArgumentException("Patient resource not found in bundle"));
    }

    private static void evaluatePatientBundle(Bundle patientBundle, String start, String end, MeasureEvaluator evaluator, boolean isDebug) {
        Patient patient = findPatient(patientBundle);
        var report = evaluator.evaluate(
                new DateTimeType(start),
                new DateTimeType(end),
                new StringType("Patient/" + patient.getIdElement().getIdPart()),
                patientBundle);
        String json = fhirContext.newJsonParser().encodeResourceToString(report);
        logger.info("Summary of evaluate for patient/groups/populations:\nPatient: {}\n{}\nJSON: {}", patient.getIdElement().getIdPart(), getGroupPopulations(report), json);
    }

    public static void main(String[] args) {
        if (args.length != 4) {
            System.err.println("Invalid number of arguments. Expected 4 arguments: <measure-bundle-path> <patient-bundle-path> <start> <end>");
            System.exit(1);
        }

        String measureBundlePath = args[0];
        String patientBundlePath = args[1];
        String start = args[2];
        String end = args[3];

        try {
            Bundle measureBundle = getBundle(measureBundlePath);
            MeasureEvaluator evaluator = MeasureEvaluator.compile(fhirContext, measureBundle, true);

            File patientBundleFile = new File(patientBundlePath);

            if (patientBundleFile.isDirectory()) {
                List<Bundle> patientBundles = getBundlesFromDirectoryAndSubDirectories(patientBundlePath);

                for (Bundle patientBundle : patientBundles) {
                    logger.info("\n===================================================");
                    evaluatePatientBundle(patientBundle, start, end, evaluator, true);
                }
            } else {
                Bundle patientBundle = getBundle(patientBundlePath);
                evaluatePatientBundle(patientBundle, start, end, evaluator, true);
            }
        } catch (Exception e) {
            System.err.println("Error occurred while evaluating measure: " + e.getMessage());
            e.printStackTrace();
        }
    }
}
