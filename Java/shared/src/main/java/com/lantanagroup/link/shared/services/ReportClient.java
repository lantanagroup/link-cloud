package com.lantanagroup.link.shared.services;

import com.lantanagroup.link.shared.auth.JwtService;
import com.lantanagroup.link.shared.entities.PatientSubmissionModel;
import com.lantanagroup.link.shared.entities.ReportScheduleModel;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.http.HttpHeaders;
import org.springframework.http.HttpStatus;
import org.springframework.web.client.HttpClientErrorException;
import org.springframework.web.client.RestClient;

import java.net.URI;
import java.util.Map;

public class ReportClient extends Router {
    private static final Logger logger = LoggerFactory.getLogger(ReportClient.class);
    private final JwtService jwtService;
    private final RestClient restClient;

    private final String REPORT_SCHEDULES_ROUTE_KEY = "report-schedules";
    private final String SUBMISSION_MODEL_ROUTE_KEY = "submission-model";

    public ReportClient(JwtService jwtService, RestClient restClient) {
        this.jwtService = jwtService;
        this.restClient = restClient;
    }

    public PatientSubmissionModel getSubmissionModel(String facilityId, String patientId, String reportId) {
        URI uri = getUri(SUBMISSION_MODEL_ROUTE_KEY, Map.of(
                "facilityId", facilityId,
                "patientId", patientId,
                "reportId", reportId));
        try {
            RestClient.RequestHeadersSpec<?> request = restClient.get().uri(uri);
            String token = jwtService.generateInterServiceToken();
            if (token != null) {
                request.header(HttpHeaders.AUTHORIZATION, "Bearer " + token);
            }
            return request.retrieve().body(PatientSubmissionModel.class);
        } catch (HttpClientErrorException e) {
            if (e.getStatusCode() == HttpStatus.NOT_FOUND) {
                logger.error("404 Not Found when requesting URI: {}", uri, e);
            }
            throw e;
        }
    }

    public ReportScheduleModel getReportSchedule(String reportId) {
        URI uri = getUri(REPORT_SCHEDULES_ROUTE_KEY, Map.of("reportId", reportId));
        try {
            RestClient.RequestHeadersSpec<?> request = restClient.get().uri(uri);
            String token = jwtService.generateInterServiceToken();
            if (token != null) {
                request.header(HttpHeaders.AUTHORIZATION, "Bearer " + token);
            }
            return request.retrieve().body(ReportScheduleModel.class);
        } catch (HttpClientErrorException e) {
            if (e.getStatusCode() == HttpStatus.NOT_FOUND) {
                logger.error("404 Not Found when requesting URI: {}", uri, e);
            }
            throw e;
        }
    }
}
