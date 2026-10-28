package com.lantanagroup.link.measureeval.utils;

import org.hl7.fhir.r4.model.Bundle;
import org.hl7.fhir.r4.model.Library;
import org.springframework.http.HttpStatus;
import org.springframework.web.server.ResponseStatusException;

public class CqlUtils {

    /**
     * Thrown when a requested library or CQL fragment cannot be located in a measure
     * definition bundle. Replaces the previous reliance on {@code javassist.NotFoundException},
     * which pulled javassist in solely as an exception type.
     */
    public static class ResourceNotFoundException extends Exception {
        public ResourceNotFoundException(String message) {
            super(message);
        }
    }

    public static Library getLibrary(Bundle bundle, String libraryId) {
        return bundle.getEntry().stream()
                .map(Bundle.BundleEntryComponent::getResource)
                .filter(Library.class::isInstance)
                .map(Library.class::cast)
                .filter(l -> {
                    if (l.getUrl() == null) {
                        return false;
                    }

                    return l.getUrl().endsWith("/" + libraryId);
                })
                .findFirst()
                .orElse(null);
    }

    public static String getCql(Bundle bundle, String libraryId, String range) throws ResourceNotFoundException {
        Library library = getLibrary(bundle, libraryId);
        if (library == null) {
            throw new ResourceNotFoundException("Library not found in measure definition bundle");
        }
        return getCql(library, range);
    }

    public static String getCql(Library library, String range) {
        // Get CQL from library's "content" and base64 decode it
        String cql = library.getContent().stream()
                .filter(content -> content.hasContentType() && content.getContentType().equals("text/cql"))
                .findFirst()
                .map(content -> new String(content.getData()))
                .orElseThrow(() -> new ResponseStatusException(HttpStatus.NOT_FOUND, "CQL content not found in library"));

        // Find range in CQL
        if (range != null) {
            // Split range into start and end line/column
            return CqlUtils.getCqlRange(range, cql);
        }

        return cql;
    }

    private static String getCqlRange(String range, String cql) {
        String[] rangeParts = range.split(":|-");

        if (rangeParts.length != 4) {
            return cql;
        }

        int startLine = Integer.parseInt(rangeParts[0]);
        int startColumn = Integer.parseInt(rangeParts[1]);
        int endLine = Integer.parseInt(rangeParts[2]);
        int endColumn = Integer.parseInt(rangeParts[3]);

        // Get the lines from the CQL
        String[] lines = cql.split("\n");

        // Get the lines in the range
        StringBuilder rangeCql = new StringBuilder();
        for (int i = startLine - 1; i < endLine; i++) {

            if (i == startLine - 1) {
                rangeCql.append(lines[i].substring(startColumn - 1));
            } else if (i == endLine - 1) {
                rangeCql.append(lines[i].substring(0, endColumn));
            } else {
                rangeCql.append(lines[i]);
            }
            if (i != endLine - 1) {
                rangeCql.append("\n");
            }
        }

        return rangeCql.toString();
    }
}
