package com.lantanagroup.link.measureeval.utils;

import ca.uhn.fhir.context.FhirContext;
import org.apache.commons.io.FileUtils;
import org.hl7.fhir.common.hapi.validation.support.PrePopulatedValidationSupport;
import org.hl7.fhir.instance.model.api.IBaseResource;
import org.hl7.fhir.r4.model.StructureDefinition;
import org.hl7.fhir.utilities.FileUtilities;
import org.hl7.fhir.utilities.npm.NpmPackage;

import java.io.File;
import java.io.FileNotFoundException;
import java.io.FileReader;
import java.io.IOException;
import java.nio.charset.StandardCharsets;
import java.util.ArrayList;
import java.util.HashMap;
import java.util.HashSet;
import java.util.List;
import java.util.Locale;
import java.util.Map;
import java.util.Set;

public class LinkValidationSupport extends PrePopulatedValidationSupport {

    private final Map<String, List<String>> profileMap = new HashMap<>();
    // this includes MS elements, required elements and "id", "meta"
    private final Map<String, Set<String>> mustSupportElements = new HashMap<>();

    public LinkValidationSupport(FhirContext theContext) {
        super(theContext);
    }

    /**
     * Overloaded from HAPI to load the provided package.
     *
     * @see org.hl7.fhir.common.hapi.validation.support.NpmPackageValidationSupport#loadPackageFromClasspath(String)
     * @param npmPackage the NPM package
     * @throws IOException could not find the package
     */
    public void loadPackage(NpmPackage npmPackage) throws IOException {
        if (npmPackage.getFolders().containsKey("package")) {
            loadResourcesFromPackage(npmPackage);
            loadBinariesFromPackage(npmPackage);
        }
    }

    private void loadResourcesFromPackage(NpmPackage thePackage) {
        NpmPackage.NpmPackageFolder packageFolder = thePackage.getFolders().get("package");

        for (String nextFile : packageFolder.listFiles()) {
            if (nextFile.toLowerCase(Locale.US).endsWith(".json")) {
                String input;
                IBaseResource resource;
                if (packageFolder.getContent().isEmpty()) {
                    try {
                        resource = getFhirContext().newJsonParser().parseResource(
                                new FileReader(FileUtils.getFile(new File(packageFolder.getFolderPath()), nextFile)));
                    } catch (FileNotFoundException e) {
                        throw new RuntimeException(e);
                    }
                } else {
                    input = new String(packageFolder.getContent().get(nextFile), StandardCharsets.UTF_8);
                    resource = getFhirContext().newJsonParser().parseResource(input);
                }
                if (resource instanceof StructureDefinition) {
                    var type = ((StructureDefinition) resource).getTypeName();
                    var url = ((StructureDefinition) resource).getUrl();
                    if (profileMap.containsKey(type)) {
                        profileMap.get(type).add(url);
                    } else {
                        var list = new ArrayList<String>();
                        list.add(url);
                        profileMap.put(type,list);
                    }
                    populateMustSupportElements((StructureDefinition) resource);
                }
                super.addResource(resource);
            }
        }
    }

    private void loadBinariesFromPackage(NpmPackage thePackage) throws IOException {
        List<String> binaries = thePackage.list("other");
        for (String binaryName : binaries) {
            addBinary(FileUtilities.streamToBytes(thePackage.load("other", binaryName)), binaryName);
        }
    }

    // For now just looking at top level elements
    private void populateMustSupportElements(StructureDefinition sd) {
        for (var element : sd.getSnapshot().getElement()) {
            if (element.getMustSupport() || (element.hasMin() && element.getMin() > 0)
                    || element.getName().equals("id") || element.getName().equals("meta")
                    || element.getName().equals("extension")) {
                if (!mustSupportElements.containsKey(sd.getUrl())) {
                    var list = new HashSet<String>();
                    list.add(element.getName());
                    mustSupportElements.put(sd.getUrl(), list);
                } else {
                    mustSupportElements.get(sd.getUrl()).add(element.getName());
                }
            }
        }
    }

    public Map<String, List<String>> getProfileMap() {
        return profileMap;
    }

    public Map<String, Set<String>> getMustSupportElements() {
        return mustSupportElements;
    }
}
