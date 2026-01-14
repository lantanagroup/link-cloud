package ca.uhn.fhir.jpa.starter.link.filters;

import io.github.bucket4j.Bandwidth;
import io.github.bucket4j.Bucket;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.beans.factory.annotation.Value;
import org.springframework.core.annotation.Order;
import org.springframework.http.HttpStatus;
import org.springframework.stereotype.Component;
import org.springframework.web.filter.OncePerRequestFilter;

import javax.servlet.FilterChain;
import javax.servlet.ServletException;
import javax.servlet.http.HttpServletRequest;
import javax.servlet.http.HttpServletResponse;
import java.io.IOException;
import java.time.Duration;
import java.time.Instant;
import java.util.concurrent.ConcurrentHashMap;

@Component
@Order(-10)
public class RequestRateLimitingFilter extends OncePerRequestFilter {
    private static final Logger logger = LoggerFactory.getLogger(RequestRateLimitingFilter.class);

    private final int count;
    private final Duration duration;
    private final ConcurrentHashMap<String, Bucket> clientBuckets = new ConcurrentHashMap<>();
    private final ConcurrentHashMap<String, Instant> bucketCreationTimes = new ConcurrentHashMap<>();

    public RequestRateLimitingFilter(
            @Value("${hapi.fhir.rate-limiting.count:-1}") int count,
            @Value("${hapi.fhir.rate-limiting.duration:PT1M}") Duration duration) {
        logger.info("Initializing {}", getClass().getSimpleName());
        logger.info("COUNT=[{}]", count);
        logger.info("DURATION=[{}]", duration);
        this.count = count;
        this.duration = duration;
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

        String clientIp = getClientIpAddress(request);
        Bucket bucket = clientBuckets.computeIfAbsent(clientIp, this::createBucket);

        // Calculate when the bucket refills
        Instant bucketCreated = bucketCreationTimes.get(clientIp);
        Instant nextRefill = getNextRefillTime(bucketCreated);
        long secondsUntilRefill = Duration.between(Instant.now(), nextRefill).getSeconds();
        // Ensure we don't show negative seconds
        secondsUntilRefill = Math.max(0, secondsUntilRefill);

        response.setHeader("X-RateLimit-Limit", String.valueOf(count));
        response.setHeader("X-RateLimit-Remaining", String.valueOf(bucket.getAvailableTokens()));
        response.setHeader("X-RateLimit-Reset", String.valueOf(secondsUntilRefill));

        if (bucket.tryConsume(1)) {
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
        
        // Track when this bucket was created
        bucketCreationTimes.put(clientIp, Instant.now());

        // simple bandwidth that completely refills every duration
        Bandwidth bandwidth = Bandwidth.builder()
                .capacity(count)
                .refillIntervally(count, duration)
                .build();

        return Bucket.builder()
                .addLimit(bandwidth)
                .build();
    }

    private Instant getNextRefillTime(Instant bucketCreated) {
        if (bucketCreated == null) {
            return Instant.now().plus(duration);
        }
        
        Instant now = Instant.now();
        long intervalsPassed = Duration.between(bucketCreated, now).toMillis() / duration.toMillis();
        return bucketCreated.plus(duration.multipliedBy(intervalsPassed + 1));
    }

    private String getClientIpAddress(HttpServletRequest request) {
        String xForwardedFor = request.getHeader("X-Forwarded-For");
        if (xForwardedFor != null && !xForwardedFor.isEmpty()) {
            return xForwardedFor.split(",")[0].trim();
        }
        
        String xRealIp = request.getHeader("X-Real-IP");
        if (xRealIp != null && !xRealIp.isEmpty()) {
            return xRealIp;
        }
        
        return request.getRemoteAddr();
    }
}
