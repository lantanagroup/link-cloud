package com.lantanagroup.link.validation.configs;

import ca.uhn.fhir.context.FhirContext;
import ca.uhn.fhir.fhirpath.IFhirPath;
import ca.uhn.fhir.rest.client.api.IRestfulClientFactory;
import org.apache.http.HttpResponse;
import org.apache.http.client.HttpRequestRetryHandler;
import org.apache.http.client.ServiceUnavailableRetryStrategy;
import org.apache.http.client.config.RequestConfig;
import org.apache.http.impl.client.CloseableHttpClient;
import org.apache.http.impl.client.HttpClients;
import org.apache.http.impl.conn.PoolingHttpClientConnectionManager;
import org.apache.http.protocol.HttpContext;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.beans.factory.annotation.Value;
import org.springframework.context.annotation.Bean;
import org.springframework.context.annotation.Configuration;

import javax.net.ssl.SSLException;
import java.io.IOException;
import java.util.concurrent.TimeUnit;


@Configuration
public class FhirConfig {
    private static final Logger logger = LoggerFactory.getLogger(FhirConfig.class);

    /**
     * Shared {@link FhirContext} whose REST clients retry transient failures (connection errors,
     * HTTP 429/5xx) so a brief terminology-service outage doesn't fail validation. 4xx responses
     * are never retried; retrying is safe because these calls are read-only.
     */
    @Bean
    public FhirContext fhirContext(
            @Value("${link.fhir-client-retry.max-attempts:5}") int maxAttempts,
            @Value("${link.fhir-client-retry.backoff-millis:5000}") long backoffMillis) {
        FhirContext fhirContext = FhirContext.forR4();
        fhirContext.getRestfulClientFactory().setHttpClient(buildHttpClient(maxAttempts, backoffMillis));
        logger.info("FHIR REST clients configured with transient-error retry: max {} attempts, {} ms backoff",
                maxAttempts, backoffMillis);
        return fhirContext;
    }

    /**
     * FHIRPath engine used by custom checks (e.g. {@code future-date}, {@code numeric-range}) to
     * evaluate FHIRPath expressions against resources. Derived from the shared {@link FhirContext}.
     */
    @Bean
    public IFhirPath fhirPath(FhirContext fhirContext) {
        return fhirContext.newFhirPath();
    }

    /**
     * Builds the HTTP client used by all HAPI REST clients, with HAPI's default pool/timeout
     * settings (bypassed when a custom client is supplied) plus retry of transient HTTP responses
     * and connection failures.
     */
    // Package-private for testing.
    static CloseableHttpClient buildHttpClient(int maxAttempts, long backoffMillis) {
        if (backoffMillis < 0) {
            throw new IllegalArgumentException("backoffMillis must be non-negative");
        }
        PoolingHttpClientConnectionManager connectionManager =
                new PoolingHttpClientConnectionManager(5000, TimeUnit.MILLISECONDS);
        connectionManager.setMaxTotal(IRestfulClientFactory.DEFAULT_POOL_MAX);
        connectionManager.setDefaultMaxPerRoute(IRestfulClientFactory.DEFAULT_POOL_MAX_PER_ROUTE);

        RequestConfig requestConfig = RequestConfig.custom()
                .setConnectTimeout(IRestfulClientFactory.DEFAULT_CONNECT_TIMEOUT)
                .setSocketTimeout(IRestfulClientFactory.DEFAULT_SOCKET_TIMEOUT)
                .setConnectionRequestTimeout(IRestfulClientFactory.DEFAULT_CONNECTION_REQUEST_TIMEOUT)
                .build();

        return HttpClients.custom()
                .setConnectionManager(connectionManager)
                .setDefaultRequestConfig(requestConfig)
                .setServiceUnavailableRetryStrategy(new TransientServerErrorRetryStrategy(maxAttempts, backoffMillis))
                .setRetryHandler(new TransientConnectionFailureRetryHandler(maxAttempts, backoffMillis))
                .disableCookieManagement()
                .build();
    }

    /**
     * Retries HTTP 429 and 5xx responses with fixed backoff; 2xx–4xx are never retried.
     */
    static class TransientServerErrorRetryStrategy implements ServiceUnavailableRetryStrategy {
        private final int maxAttempts;
        private final long backoffMillis;

        TransientServerErrorRetryStrategy(int maxAttempts, long backoffMillis) {
            this.maxAttempts = maxAttempts;
            this.backoffMillis = backoffMillis;
        }

        @Override
        public boolean retryRequest(HttpResponse response, int executionCount, HttpContext context) {
            int status = response.getStatusLine().getStatusCode();
            boolean transientFailure = status == 429 || status >= 500;
            boolean retry = transientFailure && executionCount < maxAttempts;
            if (retry) {
                logger.warn("Transient HTTP {} from FHIR server; attempt {}/{}, retrying in {} ms",
                        status, executionCount, maxAttempts, backoffMillis);
            } else if (transientFailure) {
                logger.warn("Transient HTTP {} from FHIR server; retries exhausted after {} attempts",
                        status, executionCount);
            }
            return retry;
        }

        @Override
        public long getRetryInterval() {
            return backoffMillis;
        }
    }

    /**
     * Retries connection-level I/O failures (connection refused, timeouts, unknown host) with fixed
     * backoff. Unknown host is included because a stopped container's DNS name stops resolving in
     * Docker networks. SSL failures are not retried — they indicate configuration problems, not a
     * transient outage.
     */
    static class TransientConnectionFailureRetryHandler implements HttpRequestRetryHandler {
        private final int maxAttempts;
        private final long backoffMillis;

        TransientConnectionFailureRetryHandler(int maxAttempts, long backoffMillis) {
            this.maxAttempts = maxAttempts;
            this.backoffMillis = backoffMillis;
        }

        @Override
        public boolean retryRequest(IOException exception, int executionCount, HttpContext context) {
            if (exception instanceof SSLException) {
                return false;
            }
            if (executionCount >= maxAttempts) {
                logger.warn("Connection failure ({}) from FHIR server; retries exhausted after {} attempts",
                        exception.toString(), executionCount);
                return false;
            }
            logger.warn("Connection failure ({}) from FHIR server; attempt {}/{}, retrying in {} ms",
                    exception.toString(), executionCount, maxAttempts, backoffMillis);
            try {
                Thread.sleep(backoffMillis);
            } catch (InterruptedException e) {
                Thread.currentThread().interrupt();
                return false;
            }
            return true;
        }
    }
}
