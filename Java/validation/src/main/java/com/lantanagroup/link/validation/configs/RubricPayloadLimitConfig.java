package com.lantanagroup.link.validation.configs;

import com.lantanagroup.link.validation.exceptions.PayloadParseException;
import jakarta.servlet.Filter;
import jakarta.servlet.ReadListener;
import jakarta.servlet.ServletInputStream;
import jakarta.servlet.http.HttpServletRequest;
import jakarta.servlet.http.HttpServletRequestWrapper;
import jakarta.servlet.http.HttpServletResponse;
import org.springframework.beans.factory.annotation.Value;
import org.springframework.boot.web.servlet.FilterRegistrationBean;
import org.springframework.context.annotation.Bean;
import org.springframework.context.annotation.Configuration;
import org.springframework.web.servlet.HandlerInterceptor;
import org.springframework.web.servlet.config.annotation.InterceptorRegistry;
import org.springframework.web.servlet.config.annotation.WebMvcConfigurer;

import java.io.BufferedReader;
import java.io.IOException;
import java.io.InputStreamReader;
import java.nio.charset.Charset;
import java.nio.charset.StandardCharsets;

/**
 * Rejects oversized rubric payloads before they are buffered into memory. Two layers: the
 * interceptor fast-rejects from the declared Content-Length, and the filter wraps the request
 * so the cap is enforced while the body streams in — which also covers chunked uploads that
 * carry no Content-Length. Both layers throw {@link PayloadParseException} during dispatch,
 * so the advice surfaces the documented 400.
 */
@Configuration
public class RubricPayloadLimitConfig implements WebMvcConfigurer {

    private final int maxPayloadBytes;

    public RubricPayloadLimitConfig(@Value("${link.rubric.max-payload-bytes:262144}") int maxPayloadBytes) {
        this.maxPayloadBytes = maxPayloadBytes;
    }

    @Override
    public void addInterceptors(InterceptorRegistry registry) {
        registry.addInterceptor(new DeclaredLengthInterceptor(maxPayloadBytes))
                .addPathPatterns("/api/validation/rubrics/**");
    }

    @Bean
    public FilterRegistrationBean<Filter> rubricPayloadSizeFilter() {
        FilterRegistrationBean<Filter> registration = new FilterRegistrationBean<>();
        registration.setFilter((request, response, chain) -> {
            if (request instanceof HttpServletRequest http) {
                chain.doFilter(new SizeLimitingRequestWrapper(http, maxPayloadBytes), response);
            } else {
                chain.doFilter(request, response);
            }
        });
        registration.addUrlPatterns("/api/validation/rubrics/*");
        return registration;
    }

    public static class DeclaredLengthInterceptor implements HandlerInterceptor {

        private final int maxPayloadBytes;

        public DeclaredLengthInterceptor(int maxPayloadBytes) {
            this.maxPayloadBytes = maxPayloadBytes;
        }

        @Override
        public boolean preHandle(HttpServletRequest request, HttpServletResponse response, Object handler) {
            if (request.getContentLengthLong() > maxPayloadBytes) {
                throw new PayloadParseException(
                        "Rubric payload exceeds maximum size of " + maxPayloadBytes + " bytes", null);
            }
            return true;
        }
    }

    /**
     * Counts body bytes as they are read and throws once the count passes the limit, so an
     * oversized body is cut off mid-stream instead of being buffered wholesale. A body of
     * exactly the limit is accepted.
     */
    public static class SizeLimitingRequestWrapper extends HttpServletRequestWrapper {

        private final long limit;
        private long bytesRead;

        public SizeLimitingRequestWrapper(HttpServletRequest request, long limit) {
            super(request);
            this.limit = limit;
        }

        @Override
        public ServletInputStream getInputStream() throws IOException {
            ServletInputStream delegate = super.getInputStream();
            return new ServletInputStream() {
                @Override
                public boolean isFinished() {
                    return delegate.isFinished();
                }

                @Override
                public boolean isReady() {
                    return delegate.isReady();
                }

                @Override
                public void setReadListener(ReadListener listener) {
                    delegate.setReadListener(listener);
                }

                @Override
                public int read() throws IOException {
                    int b = delegate.read();
                    if (b != -1) {
                        count(1);
                    }
                    return b;
                }

                @Override
                public int read(byte[] b, int off, int len) throws IOException {
                    int n = delegate.read(b, off, len);
                    if (n > 0) {
                        count(n);
                    }
                    return n;
                }
            };
        }

        @Override
        public BufferedReader getReader() throws IOException {
            String encoding = getCharacterEncoding();
            Charset charset = encoding != null ? Charset.forName(encoding) : StandardCharsets.ISO_8859_1;
            return new BufferedReader(new InputStreamReader(getInputStream(), charset));
        }

        private void count(int n) {
            bytesRead += n;
            if (bytesRead > limit) {
                throw new PayloadParseException(
                        "Rubric payload exceeds maximum size of " + limit + " bytes", null);
            }
        }
    }
}
