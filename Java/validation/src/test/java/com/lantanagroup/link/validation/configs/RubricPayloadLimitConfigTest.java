package com.lantanagroup.link.validation.configs;

import com.lantanagroup.link.validation.exceptions.PayloadParseException;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.springframework.mock.web.MockHttpServletRequest;
import org.springframework.mock.web.MockHttpServletResponse;

import java.io.BufferedReader;
import java.io.Writer;

import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assertions.assertThatThrownBy;

class RubricPayloadLimitConfigTest {

    private static final int LIMIT = 64;

    private final RubricPayloadLimitConfig.DeclaredLengthInterceptor interceptor =
            new RubricPayloadLimitConfig.DeclaredLengthInterceptor(LIMIT);

    private static MockHttpServletRequest requestWithBody(int size) {
        MockHttpServletRequest request = new MockHttpServletRequest();
        request.setContent(new byte[size]);
        return request;
    }

    @Test
    @DisplayName("declared Content-Length over the limit -> rejected before the body is read")
    void declaredOverLimitRejected() {
        assertThatThrownBy(() ->
                interceptor.preHandle(requestWithBody(LIMIT + 1), new MockHttpServletResponse(), new Object()))
                .isInstanceOf(PayloadParseException.class);
    }

    @Test
    @DisplayName("declared Content-Length exactly at the limit -> accepted")
    void declaredAtLimitAccepted() {
        assertThat(interceptor.preHandle(requestWithBody(LIMIT), new MockHttpServletResponse(), new Object()))
                .isTrue();
    }

    @Test
    @DisplayName("no declared Content-Length (chunked) passes the interceptor")
    void chunkedPassesInterceptor() {
        assertThat(interceptor.preHandle(new MockHttpServletRequest(), new MockHttpServletResponse(), new Object()))
                .isTrue();
    }

    @Test
    @DisplayName("body exactly at the limit streams through fully")
    void bodyAtLimitReadsFully() throws Exception {
        var wrapper = new RubricPayloadLimitConfig.SizeLimitingRequestWrapper(requestWithBody(LIMIT), LIMIT);

        assertThat(wrapper.getInputStream().readAllBytes()).hasSize(LIMIT);
    }

    @Test
    @DisplayName("oversized body is cut off mid-stream regardless of declared length (chunked path)")
    void oversizedBodyCutOffMidStream() {
        var wrapper = new RubricPayloadLimitConfig.SizeLimitingRequestWrapper(requestWithBody(LIMIT * 2), LIMIT);

        assertThatThrownBy(() -> wrapper.getInputStream().readAllBytes())
                .isInstanceOf(PayloadParseException.class);
    }

    @Test
    @DisplayName("the reader path counts against the limit too")
    void readerPathCounts() throws Exception {
        var wrapper = new RubricPayloadLimitConfig.SizeLimitingRequestWrapper(requestWithBody(LIMIT * 2), LIMIT);
        BufferedReader reader = wrapper.getReader();

        assertThatThrownBy(() -> reader.transferTo(Writer.nullWriter()))
                .isInstanceOf(PayloadParseException.class);
    }
}
