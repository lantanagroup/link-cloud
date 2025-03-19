package com.lantanagroup.link.validation.services;

import com.lantanagroup.link.shared.services.Router;
import com.lantanagroup.link.validation.entities.PatientSubmissionModel;
import org.springframework.web.client.RestClient;

import java.net.URI;
import java.util.Map;

public class ReportClient extends Router {
    private final RestClient restClient;

    public ReportClient(RestClient restClient) {
        this.restClient = restClient;
    }

    public PatientSubmissionModel getSubmissionModel(String facilityId, String patientId, String reportId) {
        URI uri = getUri(Routes.SUBMISSION_MODEL, Map.of(
                "facilityId", facilityId,
                "patientId", patientId,
                "reportId", reportId));
        return restClient.get()
                .uri(uri)
                .retrieve()
                .body(PatientSubmissionModel.class);
    }

    private static class Routes {
        public static final String SUBMISSION_MODEL = "submission-model";
    }
}
