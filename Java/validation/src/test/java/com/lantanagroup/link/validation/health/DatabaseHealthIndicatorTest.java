package com.lantanagroup.link.validation.health;

import org.junit.jupiter.api.Test;
import org.springframework.boot.actuate.health.Health;
import org.springframework.boot.actuate.health.Status;

import javax.sql.DataSource;
import java.sql.Connection;
import java.util.concurrent.CountDownLatch;
import java.util.concurrent.TimeUnit;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertFalse;
import static org.junit.jupiter.api.Assertions.assertTrue;
import static org.mockito.ArgumentMatchers.anyInt;
import static org.mockito.Mockito.mock;
import static org.mockito.Mockito.when;

class DatabaseHealthIndicatorTest {

    private final DataSource dataSource = mock(DataSource.class);

    @Test
    void whenTheDatabaseAnswers_thenUp() throws Exception {
        Connection connection = mock(Connection.class);
        when(connection.isValid(anyInt())).thenReturn(true);
        when(dataSource.getConnection()).thenReturn(connection);

        Health health = new DatabaseHealthIndicator(dataSource, 2000).health();

        assertEquals(Status.UP, health.getStatus());
        assertEquals("Available", health.getDetails().get("Database"));
    }

    @Test
    void whenTheConnectionIsNotValid_thenDown() throws Exception {
        Connection connection = mock(Connection.class);
        when(connection.isValid(anyInt())).thenReturn(false);
        when(dataSource.getConnection()).thenReturn(connection);

        Health health = new DatabaseHealthIndicator(dataSource, 2000).health();

        assertEquals(Status.DOWN, health.getStatus());
        assertEquals("Unavailable", health.getDetails().get("Database"));
    }

    /**
     * The case this indicator exists for. Spring's auto indicator asks the pool for a connection
     * with nothing but HikariCP's connectionTimeout behind it -- 30s by default -- which keeps
     * /health from answering inside the BFF's health-check window and costs the dashboard every row for the
     * service, Redis included.
     */
    @Test
    void whenTheConnectionBlocksPastTheTimeout_thenDownWithoutWaitingForThePool() throws Exception {
        CountDownLatch released = new CountDownLatch(1);
        when(dataSource.getConnection()).thenAnswer(invocation -> {
            released.await(30, TimeUnit.SECONDS);
            return mock(Connection.class);
        });

        long startedAt = System.nanoTime();
        Health health = new DatabaseHealthIndicator(dataSource, 200).health();
        long elapsedMs = (System.nanoTime() - startedAt) / 1_000_000;
        released.countDown();

        assertEquals(Status.DOWN, health.getStatus());
        assertTrue(elapsedMs < 2000,
                "health() must return at its own deadline, not the pool's; took " + elapsedMs + "ms");
    }

    /**
     * /health is reachable without authentication and served with show-details: always, and a JDBC
     * failure names the host, the instance and sometimes the login.
     */
    @Test
    void whenTheDatabaseFails_thenTheDetailCarriesNoDriverText() throws Exception {
        when(dataSource.getConnection())
                .thenThrow(new IllegalStateException("login failed for user 'link' on sql-0.internal:1433"));

        Health health = new DatabaseHealthIndicator(dataSource, 2000).health();

        assertEquals(Status.DOWN, health.getStatus());
        String details = health.getDetails().toString();
        assertFalse(details.contains("sql-0.internal"), "driver text must not reach the health detail");
        assertFalse(details.contains("link"), "the login must not reach the health detail");
    }
}
