package com.lantanagroup.link.measureeval;

import ca.uhn.fhir.context.FhirContext;
import com.lantanagroup.link.measureeval.services.MeasureEvaluator;
import org.apache.commons.io.FileUtils;
import org.hl7.fhir.r4.model.*;

import java.io.File;
import java.io.IOException;
import java.util.ArrayList;
import java.util.List;

/**
 * This class is used to invoke measure evaluation using arbitrary artifacts on the file system for ease of testing and debugging new measures.
 * Format: `java -jar measureeval.jar -cp com.lantanagroup.link.measureeval.FileSystemInvocation "<measure-bundle-path>" "<patient-bundle-path>" "<start>" "<end>"`
 * Example: `java -jar measureeval.jar -cp com.lantanagroup.link.measureeval.FileSystemInvocation "C:/path/to/measure-bundle.json" "C:/path/to/patient-bundle.json" "2021-01-01" "2021-12-31"`
 * Careful when specifying file paths to use forward-slash instead of windows' backslash, as that will cause java to interpret the backslash as an escape character and not as a path separator, leading to potentially incorrect parameter interpretation.
 * The `start` and `end` parameters must be in a valid DateTime format from the FHIR specification.
 * The `measure-bundle-path` can be a path to a single measure bundle file or a directory containing each of the resources needed for the measure.
 * The `patient-bundle-path` must be a path to a single (JSON or XML) Bundle file or a directory of files containing the patient data to be used in the evaluation.
 * The response from the operation is the MeasureReport resource being printed to the console in JSON format.
 */
public class FileSystemInvocation {
    private static final FhirContext fhirContext = FhirContext.forR4Cached();

    private static Bundle getBundle(String measureBundlePath) throws IOException {
        System.out.println("Loading measure bundle from: " + measureBundlePath);

        File measureBundleFile = new File(measureBundlePath);

        if (!measureBundleFile.exists()) {
            throw new IllegalArgumentException("Measure bundle file does not exist: " + measureBundlePath);
        }

        if (measureBundleFile.isFile()) {
            String measureBundleContent = FileUtils.readFileToString(measureBundleFile);
            if (measureBundlePath.toLowerCase().endsWith(".json")) {
                return (Bundle) fhirContext.newJsonParser().parseResource(measureBundleContent);
            } else if (measureBundlePath.toLowerCase().endsWith(".xml")) {
                return (Bundle) fhirContext.newXmlParser().parseResource(measureBundleContent);
            } else {
                throw new IllegalArgumentException("Unsupported measure bundle file format: " + measureBundlePath);
            }
        } else {
            // Parse and load each file in the directory
            Bundle bundle = new Bundle();
            bundle.setType(Bundle.BundleType.COLLECTION);

            File[] files = measureBundleFile.listFiles();

            if (files != null) {
                List<String> loaded = new ArrayList<>();

                for (File file : files) {
                    String filePath = file.getAbsolutePath();
                    if (file.isFile() && (filePath.endsWith(".json") || filePath.endsWith(".xml"))) {
                        String fileName = file.getName().substring(0, file.getName().lastIndexOf('.'));

                        if (loaded.contains(fileName)) {
                            System.out.println("Skipping duplicate file: " + filePath);
                        }

                        Resource resource;

                        if (filePath.endsWith(".json")) {
                            resource = (Resource) fhirContext.newJsonParser().parseResource(FileUtils.readFileToString(file));
                        } else {
                            resource = (Resource) fhirContext.newXmlParser().parseResource(FileUtils.readFileToString(file));
                        }

                        bundle.addEntry(new Bundle.BundleEntryComponent().setResource(resource));
                        loaded.add(fileName);
                    } else {
                        System.out.println("Skipping file: " + filePath);
                    }
                }
            }

            System.out.println("Loaded " + bundle.getEntry().size() + " resources from directory: " + measureBundlePath);

            return bundle;
        }
    }

    public static void main(String[] args) {
        String measureBundlePath = args[0];
        String patientBundlePath = args[1];
        String start = args[2];
        String end = args[3];

        try {
            Bundle measureBundle = getBundle(measureBundlePath);
            Bundle patientBundle = getBundle(patientBundlePath);
            MeasureEvaluator evaluator = MeasureEvaluator.compile(fhirContext, measureBundle);
            Patient patient = patientBundle.getEntry().stream()
                    .filter(e -> e.getResource() instanceof Patient)
                    .map(e -> (Patient) e.getResource())
                    .findFirst()
                    .orElseThrow(() -> new IllegalArgumentException("Patient resource not found in bundle"));
            var report = evaluator.evaluate(new DateTimeType(start), new DateTimeType(end),
                    new StringType("Patient/" + patient.getIdElement().getIdPart()), patientBundle);
            String json = fhirContext.newJsonParser().encodeResourceToString(report);
            System.out.println(json);
        } catch (Exception e) {
            System.err.println("Error occurred while evaluating measure: " + e.getMessage());
            e.printStackTrace();
        }
    }
}
