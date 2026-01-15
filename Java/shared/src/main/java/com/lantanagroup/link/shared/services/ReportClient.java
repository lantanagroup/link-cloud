package com.lantanagroup.link.shared.services;

import com.lantanagroup.link.shared.auth.JwtService;
import com.lantanagroup.link.shared.entities.PatientSubmissionModel;
import com.lantanagroup.link.shared.entities.ReportScheduleSummaryModel;
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

    public ReportScheduleSummaryModel getReportScheduleSummaryModel(String facilityId, String reportId) {
        URI uri = getUri(Routes.REPORT_SCHEDULE_SUMMARY_MODEL, Map.of(
                "facilityId", facilityId,
                "reportId", reportId));
        RestClient.RequestHeadersSpec<?> request = restClient.get().uri(uri);
        String token = jwtService.generateInterServiceToken();
        if (token != null) {
            request.header(HttpHeaders.AUTHORIZATION, "Bearer " + token);
        }
        return request.retrieve().body(ReportScheduleSummaryModel.class);
    }

    private static class Routes {
        public static final String SUBMISSION_MODEL = "api/Report/Submission";
        public static final String REPORT_SCHEDULE_SUMMARY_MODEL = "api/Report/Schedule";
    }
}
