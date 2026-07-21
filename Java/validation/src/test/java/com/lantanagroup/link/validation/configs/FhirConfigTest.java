package com.lantanagroup.link.validation.configs;

import com.sun.net.httpserver.HttpServer;
import org.apache.http.HttpResponse;
import org.apache.http.HttpVersion;
import org.apache.http.client.methods.CloseableHttpResponse;
import org.apache.http.client.methods.HttpGet;
import org.apache.http.impl.client.CloseableHttpClient;
import org.apache.http.message.BasicHttpResponse;
import org.apache.http.util.EntityUtils;
import org.junit.jupiter.api.AfterEach;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.Test;

import java.io.IOException;
import java.io.OutputStream;
import java.net.InetSocketAddress;
import java.nio.charset.StandardCharsets;
import java.util.concurrent.atomic.AtomicInteger;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertFalse;
import static org.junit.jupiter.api.Assertions.assertTrue;

/**
 * Verifies retry behavior of the HTTP client backing all HAPI REST clients: transient failures
 * (connection errors, 429/5xx) are retried, 4xx responses fail fast.
 */
class FhirConfigTest {

    // -------------------------------------------------------------------------
    // TransientServerErrorRetryStrategy classification
    // -------------------------------------------------------------------------

    private static HttpResponse response(int status) {
        return new BasicHttpResponse(HttpVersion.HTTP_1_1, status, null);
    }

    private static FhirConfig.TransientServerErrorRetryStrategy strategy(int maxAttempts) {
        return new FhirConfig.TransientServerErrorRetryStrategy(maxAttempts, 250L);
    }

    @Test
    void retryRequest_transientStatuses_retriedWhileAttemptsRemain() {
        FhirConfig.TransientServerErrorRetryStrategy strategy = strategy(3);
        for (int status : new int[]{429, 500, 502, 503, 504}) {
            assertTrue(strategy.retryRequest(response(status), 1, null), "HTTP " + status + " should retry");
            assertTrue(strategy.retryRequest(response(status), 2, null), "HTTP " + status + " should retry");
        }
    }

    @Test
    void retryRequest_transientStatus_notRetriedOnceAttemptsExhausted() {
        FhirConfig.TransientServerErrorRetryStrategy strategy = strategy(3);
        assertFalse(strategy.retryRequest(response(503), 3, null),
                "no retry once executionCount reaches maxAttempts");
    }

    @Test
    void retryRequest_nonTransientStatuses_neverRetried() {
        FhirConfig.TransientServerErrorRetryStrategy strategy = strategy(3);
        for (int status : new int[]{200, 201, 400, 401, 404, 422}) {
            assertFalse(strategy.retryRequest(response(status), 1, null), "HTTP " + status + " should not retry");
        }
    }

    @Test
    void getRetryInterval_returnsConfiguredBackoff() {
        assertEquals(250L, strategy(3).getRetryInterval());
    }

    // -------------------------------------------------------------------------
    // End-to-end: the built client retries against a real HTTP server
    // -------------------------------------------------------------------------

    private HttpServer server;
    private final AtomicInteger requestCount = new AtomicInteger();

    @BeforeEach
    void resetCount() {
        requestCount.set(0);
    }

    @AfterEach
    void stopServer() {
        if (server != null) {
            server.stop(0);
            server = null;
        }
    }

    /** Starts a local server that returns the given statuses in order (last one repeats). */
    private String startServer(int... statuses) throws IOException {
        server = HttpServer.create(new InetSocketAddress("127.0.0.1", 0), 0);
        server.createContext("/", exchange -> {
            int index = Math.min(requestCount.getAndIncrement(), statuses.length - 1);
            byte[] body = "{}".getBytes(StandardCharsets.UTF_8);
            exchange.getResponseHeaders().add("Content-Type", "application/json");
            exchange.sendResponseHeaders(statuses[index], body.length);
            try (OutputStream out = exchange.getResponseBody()) {
                out.write(body);
            }
        });
        server.start();
        return "http://127.0.0.1:" + server.getAddress().getPort() + "/";
    }

    private static int executeGet(CloseableHttpClient client, String url) throws IOException {
        try (CloseableHttpResponse response = client.execute(new HttpGet(url))) {
            EntityUtils.consume(response.getEntity());
            return response.getStatusLine().getStatusCode();
        }
    }

    @Test
    void builtClient_retries503ThenSucceeds() throws IOException {
        String url = startServer(503, 503, 200);
        try (CloseableHttpClient client = FhirConfig.buildHttpClient(3, 0L)) {
            assertEquals(200, executeGet(client, url));
        }
        assertEquals(3, requestCount.get(), "two 503s should be retried, third attempt succeeds");
    }

    @Test
    void builtClient_exhaustsRetriesAndReturnsLast503() throws IOException {
        String url = startServer(503);
        try (CloseableHttpClient client = FhirConfig.buildHttpClient(3, 0L)) {
            assertEquals(503, executeGet(client, url));
        }
        assertEquals(3, requestCount.get(), "should stop after maxAttempts");
    }

    @Test
    void builtClient_doesNotRetry4xx() throws IOException {
        String url = startServer(400);
        try (CloseableHttpClient client = FhirConfig.buildHttpClient(3, 0L)) {
            assertEquals(400, executeGet(client, url));
        }
        assertEquals(1, requestCount.get(), "4xx must not be retried");
    }

    // -------------------------------------------------------------------------
    // TransientConnectionFailureRetryHandler classification
    // -------------------------------------------------------------------------

    private static FhirConfig.TransientConnectionFailureRetryHandler connectionHandler(int maxAttempts) {
        return new FhirConfig.TransientConnectionFailureRetryHandler(maxAttempts, 0L);
    }

    @Test
    void connectionRetry_connectionRefusedAndTimeouts_retriedWhileAttemptsRemain() {
        FhirConfig.TransientConnectionFailureRetryHandler handler = connectionHandler(3);
        for (IOException exception : new IOException[]{
                new java.net.ConnectException("Connection refused"),
                new java.net.SocketTimeoutException("Read timed out"),
                new org.apache.http.conn.ConnectTimeoutException("Connect timed out"),
                // Unknown host is transient: a stopped container's name disappears from Docker DNS.
                new java.net.UnknownHostException("terminology")}) {
            assertTrue(handler.retryRequest(exception, 1, null), exception + " should retry");
            assertTrue(handler.retryRequest(exception, 2, null), exception + " should retry");
        }
    }

    @Test
    void connectionRetry_notRetriedOnceAttemptsExhausted() {
        FhirConfig.TransientConnectionFailureRetryHandler handler = connectionHandler(3);
        assertFalse(handler.retryRequest(new java.net.ConnectException("Connection refused"), 3, null),
                "no retry once executionCount reaches maxAttempts");
    }

    @Test
    void connectionRetry_configurationFailures_neverRetried() {
        FhirConfig.TransientConnectionFailureRetryHandler handler = connectionHandler(3);
        assertFalse(handler.retryRequest(new javax.net.ssl.SSLException("handshake failure"), 1, null),
                "SSL failures should not retry");
    }

    // -------------------------------------------------------------------------
    // End-to-end: connection refused, then recovery
    // -------------------------------------------------------------------------

    @Test
    void builtClient_retriesConnectionRefusedUntilServerComesBack() throws Exception {
        // Reserve a port, then close the socket so connections are refused.
        int port;
        try (java.net.ServerSocket socket = new java.net.ServerSocket(0, 0, java.net.InetAddress.getByName("127.0.0.1"))) {
            port = socket.getLocalPort();
        }
        String url = "http://127.0.0.1:" + port + "/";

        // Bring the server up on that port after a delay, while the client is mid-retry.
        Thread starter = new Thread(() -> {
            try {
                Thread.sleep(500);
                server = HttpServer.create(new InetSocketAddress("127.0.0.1", port), 0);
                server.createContext("/", exchange -> {
                    requestCount.incrementAndGet();
                    byte[] body = "{}".getBytes(StandardCharsets.UTF_8);
                    exchange.sendResponseHeaders(200, body.length);
                    try (OutputStream out = exchange.getResponseBody()) {
                        out.write(body);
                    }
                });
                server.start();
            } catch (Exception e) {
                throw new RuntimeException(e);
            }
        });
        starter.start();

        try (CloseableHttpClient client = FhirConfig.buildHttpClient(10, 250L)) {
            assertEquals(200, executeGet(client, url),
                    "connection-refused failures should be retried until the server is back");
        } finally {
            starter.join();
        }
        assertEquals(1, requestCount.get(), "exactly one request should reach the recovered server");
    }
}
