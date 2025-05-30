package com.lantanagroup.link.validation.services;

import com.lantanagroup.link.shared.auth.JwtService;
import com.lantanagroup.link.shared.services.Router;
import com.lantanagroup.link.validation.entities.PatientSubmissionModel;
import org.springframework.http.HttpHeaders;
import org.springframework.web.client.RestClient;

import java.net.URI;
import java.util.Map;

public class ReportClient extends Router {
    private final JwtService jwtService;
    private final RestClient restClient;

    public ReportClient(JwtService jwtService, RestClient restClient) {
        this.jwtService = jwtService;
        this.restClient = restClient;
    }

    public PatientSubmissionModel getSubmissionModel(String facilityId, String patientId, String reportId) {
        URI uri = getUri(Routes.SUBMISSION_MODEL, Map.of(
                "facilityId", facilityId,
                "patientId", patientId,
                "reportId", reportId));
        RestClient.RequestHeadersSpec<?> request = restClient.get().uri(uri);
        String token = jwtService.generateInterServiceToken();
        if (token != null) {
            request.header(HttpHeaders.AUTHORIZATION, "Bearer " + token);
        }
        return request.retrieve().body(PatientSubmissionModel.class);
    }

    private static class Routes {
        public static final String SUBMISSION_MODEL = "submission-model";
    }
}
