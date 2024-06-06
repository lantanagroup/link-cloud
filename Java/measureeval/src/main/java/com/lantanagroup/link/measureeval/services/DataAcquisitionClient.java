package com.lantanagroup.link.measureeval.services;

import com.azure.security.keyvault.secrets.SecretClient;
import com.lantanagroup.link.shared.auth.JwtService;
import com.lantanagroup.link.shared.auth.PrincipalUser;

import com.lantanagroup.link.measureeval.models.QueryResults;
import com.lantanagroup.link.measureeval.models.QueryType;
import org.apache.commons.lang3.StringUtils;
import org.springframework.security.core.GrantedAuthority;
import org.springframework.security.core.authority.SimpleGrantedAuthority;
import org.springframework.web.client.RestClient;

import java.net.URI;
import java.util.ArrayList;
import java.util.List;
import java.util.Map;

public class DataAcquisitionClient extends Router {
    private final RestClient restClient;
    private  SecretClient secretClient = null;
    private final JwtService jwtService;
    private String secret;

    public DataAcquisitionClient(RestClient restClient, JwtService jwtService, SecretClient... secretClient) {
        this.restClient = restClient;

        this.jwtService = jwtService;
        if(secretClient.length > 0){
            this.secretClient = secretClient[0];
        }
    }

    public QueryResults getQueryResults(String facilityId, String correlationId, QueryType queryType) {

        String token = "";

        if (StringUtils.isBlank(secret) && secretClient != null) {
            secret = secretClient.getSecret(JwtService.Link_Bearer_Key).getValue();
        }
        // generate the token
        if(!StringUtils.isBlank(secret)){
            token = jwtService.generateToken(createPrincipalUser(), secret);
        }

        URI uri = getUri(Routes.QUERY_RESULT, Map.of(
                "facilityId", facilityId,
                "correlationId", correlationId,
                "queryType", queryType));

        if (!StringUtils.isBlank(token)) {
            return restClient.get()
                    .uri(uri)
                    .header("Authorization", "Bearer " + token)
                    .retrieve()
                    .body(QueryResults.class);
        } else {
            return restClient.get()
                    .uri(uri)
                    .retrieve()
                    .body(QueryResults.class);
        }
    }

    private  PrincipalUser createPrincipalUser () {
        List<GrantedAuthority> authorities = new ArrayList<>();
        authorities.add(new SimpleGrantedAuthority(JwtService.RolePrefix + JwtService.LinkUserClaims_LinkAdministrator));
        authorities.add(new SimpleGrantedAuthority(JwtService.LinkSystemPermissions_IsLinkAdmin));

        return new PrincipalUser(JwtService.LinkUserClaims_LinkSystemAccount, " ", authorities);
    }

    private static class Routes {
        public static final String QUERY_RESULT = "query-result";
    }
}
