package ca.uhn.fhir.jpa.starter.link.filters;

import com.lantanagroup.link.hapi.utils.HttpServletRequestUtils;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.core.annotation.Order;
import org.springframework.stereotype.Component;
import org.springframework.web.filter.OncePerRequestFilter;

import javax.servlet.FilterChain;
import javax.servlet.ServletException;
import javax.servlet.http.HttpServletRequest;
import javax.servlet.http.HttpServletResponse;
import java.io.IOException;
import java.util.concurrent.ConcurrentHashMap;
import java.util.concurrent.ConcurrentMap;

@Component
@Order(-20)
public class RequestConcurrencyLoggingFilter extends OncePerRequestFilter {
    private static final Logger logger = LoggerFactory.getLogger(RequestConcurrencyLoggingFilter.class);

    private final ConcurrentMap<String, Integer> countsByAddress = new ConcurrentHashMap<>();

    public RequestConcurrencyLoggingFilter() {
        logger.info("Initializing {}", getClass().getSimpleName());
    }

    @Override
    protected void doFilterInternal(HttpServletRequest request, HttpServletResponse response, FilterChain chain)
            throws IOException, ServletException {
        String address = HttpServletRequestUtils.getClientIpAddress(request);
        int count = increment(address);
        try {
            logger.info("ADDRESS=[{}] COUNT=[{}]", address, count);
            chain.doFilter(request, response);
        } finally {
            decrement(address);
        }
    }

    private int increment(String address) {
        return countsByAddress.compute(address, (key, value) -> {
            if (value == null) {
                return 1;
            }
            if (value < 0) {
                logger.warn("Invalid pre-request count for {}: {}", address, value);
                return 1;
            }
            return value + 1;
        });
    }

    private void decrement(String address) {
        countsByAddress.compute(address, (key, value) -> {
            if (value == null) {
                logger.warn("Post-request count for {} not found", address);
                return null;
            }
            if (value < 1) {
                logger.warn("Invalid post-request count for {}: {}", address, value);
                return null;
            }
            return value == 1 ? null : value - 1;
        });
    }
}
