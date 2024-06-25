package com.lantanagroup.link.measureeval.filters;

import io.opentelemetry.api.trace.Span;
import jakarta.servlet.FilterChain;
import jakarta.servlet.ServletException;
import jakarta.servlet.ServletRequest;
import jakarta.servlet.ServletResponse;
import org.slf4j.MDC;
import org.springframework.stereotype.Component;
import org.springframework.web.filter.GenericFilterBean;

import java.io.IOException;

@Component
class TracingFilter extends GenericFilterBean {

  @Override
  public void doFilter(ServletRequest request, ServletResponse response,
                       FilterChain chain) throws IOException, ServletException {
    Span currentSpan = Span.current();
    if (currentSpan == null) {
      chain.doFilter(request, response);
      return;
    }
    MDC.put("traceId", currentSpan.getSpanContext().getTraceId());
    MDC.put("spanId", currentSpan.getSpanContext().getSpanId());
    chain.doFilter(request, response);
  }

}
