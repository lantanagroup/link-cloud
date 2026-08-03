package com.lantanagroup.link.validation.configs;

import com.lantanagroup.link.validation.exceptions.PayloadParseException;
import jakarta.servlet.http.HttpServletRequest;
import jakarta.servlet.http.HttpServletResponse;
import org.springframework.beans.factory.annotation.Value;
import org.springframework.context.annotation.Configuration;
import org.springframework.web.servlet.HandlerInterceptor;
import org.springframework.web.servlet.config.annotation.InterceptorRegistry;
import org.springframework.web.servlet.config.annotation.WebMvcConfigurer;

/**
 * Rejects oversized rubric payloads from the declared Content-Length before Spring reads the
 * body into memory. Chunked requests carry no Content-Length and fall through to the secondary
 * check in {@link com.lantanagroup.link.validation.controllers.RubricController}; both paths
 * throw {@link PayloadParseException} and surface the documented 400.
 */
@Configuration
public class RubricPayloadLimitConfig implements WebMvcConfigurer {

    private final int maxPayloadBytes;

    public RubricPayloadLimitConfig(@Value("${link.rubric.max-payload-bytes:262144}") int maxPayloadBytes) {
        this.maxPayloadBytes = maxPayloadBytes;
    }

    @Override
    public void addInterceptors(InterceptorRegistry registry) {
        registry.addInterceptor(new HandlerInterceptor() {
            @Override
            public boolean preHandle(HttpServletRequest request, HttpServletResponse response, Object handler) {
                if (request.getContentLengthLong() > maxPayloadBytes) {
                    throw new PayloadParseException(
                            "Rubric payload exceeds maximum size of " + maxPayloadBytes + " bytes", null);
                }
                return true;
            }
        }).addPathPatterns("/api/validation/rubrics/**");
    }
}
