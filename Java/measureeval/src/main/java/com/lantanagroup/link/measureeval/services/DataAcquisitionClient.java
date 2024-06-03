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
    private final SecretClient secretClient;
    private final JwtService jwtService;

    private String secret;

    public DataAcquisitionClient(RestClient restClient, SecretClient secretClient, JwtService jwtService) {
        this.restClient = restClient;
        this.secretClient = secretClient;
        this.jwtService = jwtService;
    }

    public QueryResults getQueryResults(String facilityId, String correlationId, QueryType queryType) {

        if(StringUtils.isBlank(secret )) {
            secret = secretClient.getSecret(JwtService.Link_Bearer_Key).getValue();
        }
        PrincipalUser user = createPrincipalUser();
        // generate the token
        String token = jwtService.generateToken(user, secret);
        URI uri = getUri(Routes.QUERY_RESULT, Map.of(
                "facilityId", facilityId,
                "correlationId", correlationId,
                "queryType", queryType));
        return restClient.get()
                .uri(uri)
                .header("Authorization", "Bearer " + token)
                .retrieve()
                .body(QueryResults.class);
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
