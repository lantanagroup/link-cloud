package com.lantanagroup.link.measureeval.utils;

import org.hl7.fhir.r4.model.Bundle;
import org.hl7.fhir.r4.model.Library;
import org.hl7.fhir.r4.model.ResourceType;
import org.springframework.http.HttpStatus;
import org.springframework.web.server.ResponseStatusException;

import java.util.Optional;

public class CqlUtils {
    public static String getCql(Bundle bundle, String libraryId, String range) {
        // Get library from the measure definition bundle based on the libraryId
        Optional<Library> library = bundle.getEntry().stream()
                .filter(entry -> {
                    if (!entry.hasResource() || entry.getResource().getResourceType() != ResourceType.Library) {
                        return false;
                    }

                    Library l = (Library) entry.getResource();

                    if (l.getUrl() == null) {
                        return false;
                    }

                    return l.getUrl().endsWith("/" + libraryId);
                })
                .findFirst()
                .map(entry -> (Library) entry.getResource());

        if (library.isEmpty()) {
            throw new ResponseStatusException(HttpStatus.NOT_FOUND, "Library not found in measure definition bundle");
        }

        // Get CQL from library's "content" and base64 decode it
        String cql = library.get().getContent().stream()
                .filter(content -> content.hasContentType() && content.getContentType().equals("text/cql"))
                .findFirst()
                .map(content -> new String(content.getData()))
                .orElseThrow(() -> new ResponseStatusException(HttpStatus.NOT_FOUND, "CQL content not found in library"));

        // Find range in CQL
        if (range != null) {
            // Split range into start and end line/column
            StringBuilder rangeCql = CqlUtils.getCqlRange(range, cql);

            return rangeCql.toString();
        }

        return cql;
    }

    private static StringBuilder getCqlRange(String range, String cql) {
        String[] rangeParts = range.split(":|-");
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
        return rangeCql;
    }
}
