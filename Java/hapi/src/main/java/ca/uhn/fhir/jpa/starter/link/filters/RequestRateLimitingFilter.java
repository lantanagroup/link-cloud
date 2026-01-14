package ca.uhn.fhir.jpa.starter.link.filters;

import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.beans.factory.annotation.Value;
import org.springframework.core.annotation.Order;
import org.springframework.stereotype.Component;
import org.springframework.web.filter.OncePerRequestFilter;

import javax.servlet.FilterChain;
import javax.servlet.ServletException;
import javax.servlet.http.HttpServletRequest;
import javax.servlet.http.HttpServletResponse;
import java.io.IOException;
import java.time.Duration;

@Component
@Order(-10)
public class RequestRateLimitingFilter extends OncePerRequestFilter {
    private static final Logger logger = LoggerFactory.getLogger(RequestRateLimitingFilter.class);

    private final int count;
    private final Duration duration;

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
        // TODO: Implement
        chain.doFilter(request, response);
    }
}
