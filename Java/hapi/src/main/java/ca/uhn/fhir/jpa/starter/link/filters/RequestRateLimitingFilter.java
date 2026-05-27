package ca.uhn.fhir.jpa.starter.link.filters;

import com.github.benmanes.caffeine.cache.Cache;
import com.github.benmanes.caffeine.cache.Caffeine;
import com.lantanagroup.link.hapi.utils.HttpServletRequestUtils;
import io.github.bucket4j.Bandwidth;
import io.github.bucket4j.Bucket;
import io.github.bucket4j.ConsumptionProbe;
import jakarta.servlet.FilterChain;
import jakarta.servlet.ServletException;
import jakarta.servlet.http.HttpServletRequest;
import jakarta.servlet.http.HttpServletResponse;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.beans.factory.annotation.Value;
import org.springframework.core.annotation.Order;
import org.springframework.http.HttpStatus;
import org.springframework.stereotype.Component;
import org.springframework.web.filter.OncePerRequestFilter;

import java.io.IOException;
import java.time.Duration;

@Component
@Order(-10)
public class RequestRateLimitingFilter extends OncePerRequestFilter {
    private static final Logger logger = LoggerFactory.getLogger(RequestRateLimitingFilter.class);

    private final int count;
    private final Duration duration;
    private final Cache<String, Bucket> clientBuckets;

    public RequestRateLimitingFilter(
            @Value("${hapi.fhir.rate-limiting.count:-1}") int count,
            @Value("${hapi.fhir.rate-limiting.duration:PT1M}") Duration duration) {
        logger.info("Initializing {}", getClass().getSimpleName());
        logger.info("COUNT=[{}]", count);
        logger.info("DURATION=[{}]", duration);
        this.count = count;
        this.duration = duration;
        clientBuckets = Caffeine.newBuilder()
                .expireAfterAccess(duration.multipliedBy(2))
                .build();
    }

    @Override
    protected void doFilterInternal(HttpServletRequest request, HttpServletResponse response, FilterChain chain)
            throws IOException, ServletException {
        
        // If count is < 0, no rate limiting is applied
        if (count < 0) {
            chain.doFilter(request, response);
            return;
        }

        // If count is 0, then no requests are allowed
        if(count == 0){
            response.setStatus(HttpStatus.TOO_MANY_REQUESTS.value());
            return;
        }

        String clientIp = HttpServletRequestUtils.getClientIpAddress(request);
        Bucket bucket = clientBuckets.get(clientIp, this::createBucket);

        ConsumptionProbe probe = bucket.tryConsumeAndReturnRemaining(1);
        long secondsUntilRefill = probe.getNanosToWaitForReset() / 1_000_000_000;

        response.setHeader("X-RateLimit-Limit", String.valueOf(count));
        response.setHeader("X-RateLimit-Remaining", String.valueOf(probe.getRemainingTokens()));
        response.setHeader("X-RateLimit-Reset", String.valueOf(secondsUntilRefill));

        if (probe.isConsumed()) {
            logger.debug("Request from {} allowed. Remaining tokens: {}", clientIp, bucket.getAvailableTokens());
            chain.doFilter(request, response);
        } else {
            logger.warn("Rate limit exceeded for client: {}. Request denied.", clientIp);
            response.setStatus(HttpStatus.TOO_MANY_REQUESTS.value());
            response.setHeader("Retry-After", String.valueOf(secondsUntilRefill));
        }
    }

    private Bucket createBucket(String clientIp) {
        logger.debug("Creating new rate limiting bucket for client: {}", clientIp);

        // simple bandwidth that completely refills every duration
        Bandwidth bandwidth = Bandwidth.builder()
                .capacity(count)
                .refillIntervally(count, duration)
                .build();

        return Bucket.builder()
                .addLimit(bandwidth)
                .build();
    }
}
