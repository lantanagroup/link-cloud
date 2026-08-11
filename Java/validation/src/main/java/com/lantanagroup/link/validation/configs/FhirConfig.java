package com.lantanagroup.link.validation.configs;

import ca.uhn.fhir.context.FhirContext;
import ca.uhn.fhir.context.support.DefaultProfileValidationSupport;
import ca.uhn.fhir.fhirpath.IFhirPath;
import ca.uhn.fhir.rest.client.api.IRestfulClientFactory;
import ca.uhn.fhir.validation.FhirValidator;
import com.lantanagroup.link.validation.services.execution.ThreadLocalFhirPath;
import org.hl7.fhir.common.hapi.validation.support.CachingValidationSupport;
import org.hl7.fhir.common.hapi.validation.support.CommonCodeSystemsTerminologyService;
import org.hl7.fhir.common.hapi.validation.support.InMemoryTerminologyServerValidationSupport;
import org.hl7.fhir.common.hapi.validation.support.RemoteTerminologyServiceValidationSupport;
import org.hl7.fhir.common.hapi.validation.support.SnapshotGeneratingValidationSupport;
import org.hl7.fhir.common.hapi.validation.support.ValidationSupportChain;
import org.hl7.fhir.common.hapi.validation.validator.FhirInstanceValidator;
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
import java.util.concurrent.ForkJoinPool;
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
     *
     * <p>FHIRPath is evaluated concurrently by the rubric check pool, and HAPI's engine is not safe to
     * share across threads, so hand each thread its own.
     */
    @Bean
    public IFhirPath fhirPath(FhirContext fhirContext) {
        return new ThreadLocalFhirPath(fhirContext);
    }

    /**
     * Terminology/profile validation support chain used by the rubric execution engine's
     * FHIR-conformance, terminology, and value-set check executors. When
     * {@code vaas.terminology-service-url} is set, a remote terminology service is added to the chain.
     */
    @Bean
    public ValidationSupportChain validationSupportChain(
            FhirContext fhirContext,
            @Value("${vaas.terminology-service-url:}") String terminologyServiceUrl) {
        ValidationSupportChain chain = new ValidationSupportChain(
                new DefaultProfileValidationSupport(fhirContext),
                new InMemoryTerminologyServerValidationSupport(fhirContext),
                new CommonCodeSystemsTerminologyService(fhirContext),
                new SnapshotGeneratingValidationSupport(fhirContext)
        );
        if (terminologyServiceUrl != null && !terminologyServiceUrl.isBlank()) {
            chain.addValidationSupport(new RemoteTerminologyServiceValidationSupport(fhirContext, terminologyServiceUrl));
            logger.info("Validation support chain: DefaultProfile -> InMemoryTerminology -> CommonCodeSystems -> SnapshotGenerating -> RemoteTerminology({}); TERMINOLOGY/VALUESET checks will call the remote server for code systems the in-memory modules cannot answer", terminologyServiceUrl);
        } else {
            logger.warn("Validation support chain: no remote terminology server configured (vaas.terminology-service-url is blank) — TERMINOLOGY/VALUESET checks will SKIP codes in unknown code systems (silent pass)");
        }
        return chain;
    }

    /**
     * FHIR instance validator used by the rubric execution engine's FHIR-conformance check executor.
     */
    @Bean
    public FhirValidator fhirValidator(FhirContext fhirContext, ValidationSupportChain validationSupportChain) {
        CachingValidationSupport cachingSupport = new CachingValidationSupport(validationSupportChain);
        FhirInstanceValidator instanceValidator = new FhirInstanceValidator(cachingSupport);
        instanceValidator.setAnyExtensionsAllowed(true);
        FhirValidator validator = fhirContext.newValidator();
        validator.registerValidatorModule(instanceValidator);
        validator.setConcurrentBundleValidation(true);
        validator.setExecutorService(ForkJoinPool.commonPool());
        return validator;
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
