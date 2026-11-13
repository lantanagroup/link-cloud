package com.lantanagroup.link.validation.controllers;

import com.fasterxml.jackson.core.JsonFactory;
import com.fasterxml.jackson.core.JsonParseException;
import com.fasterxml.jackson.core.JsonParser;
import com.lantanagroup.link.validation.exceptions.PayloadParseException;
import com.lantanagroup.link.validation.models.EvaluateRequestDto;
import org.springframework.core.MethodParameter;
import org.springframework.http.HttpHeaders;
import org.springframework.http.HttpInputMessage;
import org.springframework.http.converter.HttpMessageConverter;
import org.springframework.web.bind.annotation.ControllerAdvice;
import org.springframework.web.servlet.mvc.method.annotation.RequestBodyAdviceAdapter;

import java.io.ByteArrayInputStream;
import java.io.IOException;
import java.io.InputStream;
import java.lang.reflect.Type;

@ControllerAdvice
public class StrictDuplicateKeyRequestBodyAdvice extends RequestBodyAdviceAdapter {

    private final JsonFactory strictFactory = new JsonFactory()
            .enable(JsonParser.Feature.STRICT_DUPLICATE_DETECTION);

    @Override
    public boolean supports(MethodParameter methodParameter, Type targetType,
                            Class<? extends HttpMessageConverter<?>> converterType) {
        return EvaluateRequestDto.class.equals(targetType);
    }

    @Override
    public HttpInputMessage beforeBodyRead(HttpInputMessage inputMessage, MethodParameter parameter,
                                           Type targetType, Class<? extends HttpMessageConverter<?>> converterType)
            throws IOException {
        // Buffer the body once: the strict scan consumes a stream, then the real converter needs it again.
        byte[] body = inputMessage.getBody().readAllBytes();
        scanForDuplicateKeys(body);
        return new BufferedInputMessage(body, inputMessage.getHeaders());
    }

    // Walk every token so STRICT_DUPLICATE_DETECTION visits each field name, without building a tree.
    private void scanForDuplicateKeys(byte[] body) {
        try (JsonParser parser = strictFactory.createParser(body)) {
            while (parser.nextToken() != null) {
                // traversal alone triggers duplicate-key detection
            }
        } catch (JsonParseException e) {
            String message = e.getOriginalMessage() == null ? "" : e.getOriginalMessage();
            if (message.contains("Duplicate field")) {
                throw new PayloadParseException(
                        "Malformed JSON payload: " + message
                                + ". Duplicate keys silently drop data and are not allowed.", e);
            }
            // Not a duplicate-key problem (or an empty/partial body): leave it to the normal converter,
            // which already yields the standard "body is not valid JSON" / "body should not be empty" 400.
        } catch (IOException e) {
            // I/O reading a buffered byte[] should not happen; fall through and let the converter parse.
        }
    }

    private record BufferedInputMessage(byte[] body, HttpHeaders headers) implements HttpInputMessage {
        @Override
        public InputStream getBody() {
            return new ByteArrayInputStream(body);
        }

        @Override
        public HttpHeaders getHeaders() {
            return headers;
        }
    }
}
