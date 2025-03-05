package com.lantanagroup.link.validation.services;

import com.lantanagroup.link.shared.services.Router;
import org.hl7.fhir.r4.model.Bundle;
import org.springframework.web.client.RestClient;

import java.net.URI;
import java.util.Map;

public class ReportClient extends Router {
    private final RestClient restClient;

    public ReportClient(RestClient restClient) {
        this.restClient = restClient;
    }

    public Bundle getSubmissionBundle(String facilityId, String patientId, String reportId) {
        URI uri = getUri(Routes.SUBMISSION_BUNDLE, Map.of(
                "facilityId", facilityId,
                "patientId", patientId,
                "reportId", reportId));
        return restClient.get()
                .uri(uri)
                .retrieve()
                .body(Bundle.class);
    }

    private static class Routes {
        public static final String SUBMISSION_BUNDLE = "submission-bundle";
    }
}
