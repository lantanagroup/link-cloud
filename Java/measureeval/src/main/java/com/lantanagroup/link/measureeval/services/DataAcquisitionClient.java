package com.lantanagroup.link.measureeval.services;

import com.lantanagroup.link.measureeval.models.QueryResults;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.web.client.RestClient;
import org.springframework.web.client.RestClientException;

import java.net.URI;
import java.util.Map;

public class DataAcquisitionClient extends Router {
    private static final Logger logger = LoggerFactory.getLogger(DataAcquisitionClient.class);

    private final RestClient restClient;

    public DataAcquisitionClient(RestClient restClient) {
        this.restClient = restClient;
    }

    public QueryResults getQueryResults(String facilityId, String patientId) {
        URI uri = getUri(Routes.QUERY_RESULT, Map.of(
                "facilityId", facilityId,
                "patientId", patientId));
        logger.debug("Retrieving query results: {}", uri);
        try {
            return restClient.get()
                    .uri(uri)
                    .retrieve()
                    .body(QueryResults.class);
        } catch (RestClientException e) {
            logger.error("Failed to retrieve query results", e);
            return null;
        }
    }

    private static class Routes {
        public static final String QUERY_RESULT = "query-result";
    }
}
