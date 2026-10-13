package com.lantanagroup.link.measureeval.health;
import com.mongodb.MongoCredential;
import com.mongodb.MongoSecurityException;
import org.bson.Document;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.Test;
import org.springframework.boot.actuate.health.Status;
import org.springframework.data.mongodb.core.MongoTemplate;
import org.springframework.boot.actuate.health.Health;

import java.util.concurrent.CountDownLatch;
import java.util.concurrent.TimeUnit;

import static org.junit.jupiter.api.Assertions.*;
import static org.mockito.Mockito.*;

public class MongoHealthIndicatorTest {

    private static final long TIMEOUT_MS = 2000;

    private MongoTemplate mongoTemplate;
    private MongoHealthIndicator mongoHealthIndicator;

    @BeforeEach
    void setUp() {
        mongoTemplate = mock(MongoTemplate.class);
        mongoHealthIndicator = new MongoHealthIndicator(mongoTemplate, TIMEOUT_MS);
    }

    @Test
    void whenMongoIsUp_thenHealthStatusIsUp() {
        // Arrange
        Document result = new Document("ok", 1.0);
        when(mongoTemplate.executeCommand("{ ping: 1 }")).thenReturn(result);

        // Act
        Health health = mongoHealthIndicator.health();

        // Assert
        assertEquals(Status.UP, health.getStatus());
        assertEquals("{Database=Available}", health.getDetails().toString());
    }

    /**
     * GET /health is permitAll in both authentication modes and is served with
     * show-details: always, so the reason reaches anyone who can reach the port. The driver's own
     * text carries server addresses, replica-set topology and credential details, so the detail
     * names the failure by type and the message stays in the log.
     */
    @Test
    void whenMongoIsDown_thenHealthIsDownWithoutTheDriverMessage() {
        // Arrange: a driver message of the shape that carries internal addresses.
        when(mongoTemplate.executeCommand("{ ping: 1 }"))
                .thenThrow(new RuntimeException("no server found for mongo-0.internal:27017"));

        // Act
        Health health = mongoHealthIndicator.health();

        // Assert
        assertEquals(Status.DOWN, health.getStatus());
        assertEquals("Unavailable", health.getDetails().get("Database"));
        String error = health.getDetails().get("error").toString();
        assertEquals("Mongo ping failed: RuntimeException", error);
        assertFalse(error.contains("mongo-0.internal"),
                "the driver's message must not reach the health detail, got: " + error);
    }

    /**
     * A refused credential is the one failure an operator acts on differently from an unreachable
     * cluster, so it is named -- without repeating the driver's text, which identifies the user and
     * the mechanism it tried.
     */
    @Test
    void whenMongoRefusesCredentials_thenTheReasonSaysSoWithoutNamingTheUser() {
        // Arrange
        MongoSecurityException refused = new MongoSecurityException(
                MongoCredential.createCredential("link", "admin", "secret".toCharArray()),
                "Exception authenticating MongoCredential{mechanism=SCRAM-SHA-1, userName='link'}",
                new RuntimeException("auth failed"));
        when(mongoTemplate.executeCommand("{ ping: 1 }")).thenThrow(refused);

        // Act
        Health health = mongoHealthIndicator.health();

        // Assert
        assertEquals(Status.DOWN, health.getStatus());
        String error = health.getDetails().get("error").toString();
        assertEquals("Mongo rejected the service's credentials", error);
        assertFalse(error.contains("link"), "the credential must not reach the health detail");
    }

    /**
     * An unreachable mongod leaves the driver blocking for its full serverSelectionTimeoutMS -- 30s on
     * the default -- which pushes /health past the BFF's 5s deadline, so the BFF reports the service
     * Unhealthy with no entries at all and the Admin dashboard loses both the MongoDB and the Resource
     * Cache rows. The indicator must give up on its own well before that.
     */
    @Test
    void whenPingBlocksPastTheTimeout_thenHealthIsDownWithoutWaitingForThePing() throws Exception {
        // Arrange: a ping that blocks far longer than the indicator's own deadline.
        CountDownLatch released = new CountDownLatch(1);
        MongoHealthIndicator indicator = new MongoHealthIndicator(mongoTemplate, 200);
        when(mongoTemplate.executeCommand("{ ping: 1 }")).thenAnswer(invocation -> {
            released.await(30, TimeUnit.SECONDS);
            return new Document("ok", 1.0);
        });

        // Act
        long startedAt = System.nanoTime();
        Health health = indicator.health();
        long elapsedMs = (System.nanoTime() - startedAt) / 1_000_000;
        released.countDown();

        // Assert
        assertEquals(Status.DOWN, health.getStatus());
        assertTrue(elapsedMs < 2000,
                "health() must return at its own deadline, not the driver's; took " + elapsedMs + "ms");
        // The Database detail is still reported so the Admin dashboard keeps rendering the row.
        assertEquals("Unavailable", health.getDetails().get("Database"));
        assertTrue(health.getDetails().get("error").toString().contains("timed out"),
                "details should say the check timed out, got: " + health.getDetails());
    }

    /**
     * The container healthcheck polls /health every few seconds, so the BFF's request routinely lands
     * while a ping is still in flight. Turning that caller away described the executor rather than
     * Mongo, so concurrent callers share the running ping and get the same verdict.
     */
    @Test
    void whenAPingIsAlreadyRunning_thenConcurrentCallersShareIt() throws Exception {
        // Arrange: a ping that stays stuck, so the worker is still busy on the second call.
        CountDownLatch released = new CountDownLatch(1);
        MongoHealthIndicator indicator = new MongoHealthIndicator(mongoTemplate, 100);
        when(mongoTemplate.executeCommand("{ ping: 1 }")).thenAnswer(invocation -> {
            released.await(30, TimeUnit.SECONDS);
            return new Document("ok", 1.0);
        });

        // Act
        Health first = indicator.health();
        Health second = indicator.health();
        released.countDown();

        // Assert
        assertEquals(Status.DOWN, first.getStatus());
        assertEquals(Status.DOWN, second.getStatus());
        // The second caller gets the same reason, naming Mongo rather than the thread pool.
        assertEquals("Mongo health check timed out after 100ms", second.getDetails().get("error"));
        // One ping served both callers: the second joined the first instead of starting another.
        verify(mongoTemplate, times(1)).executeCommand("{ ping: 1 }");
    }
}
