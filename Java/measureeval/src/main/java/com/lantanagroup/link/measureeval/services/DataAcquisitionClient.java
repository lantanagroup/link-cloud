package com.lantanagroup.link.measureeval.services;

import com.lantanagroup.link.measureeval.models.QueryResults;
import com.lantanagroup.link.measureeval.models.QueryType;
import org.springframework.web.client.RestClient;

import java.net.URI;
import java.util.Map;

public class DataAcquisitionClient extends Router {
    private final RestClient restClient;

    public DataAcquisitionClient(RestClient restClient) {
        this.restClient = restClient;
    }

    public QueryResults getQueryResults(String facilityId, String correlationId, QueryType queryType) {
        URI uri = getUri(Routes.QUERY_RESULT, Map.of(
                "facilityId", facilityId,
                "correlationId", correlationId,
                "queryType", queryType));
        return restClient.get()
                .uri(uri)
                .retrieve()
                .body(QueryResults.class);
    }

    private static class Routes {
        public static final String QUERY_RESULT = "query-result";
    }
}