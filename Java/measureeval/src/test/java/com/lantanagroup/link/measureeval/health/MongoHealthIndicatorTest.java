package com.lantanagroup.link.measureeval.health;
import org.bson.Document;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.Test;
import org.springframework.boot.actuate.health.Status;
import org.springframework.data.mongodb.core.MongoTemplate;
import org.springframework.boot.actuate.health.Health;

import static org.junit.jupiter.api.Assertions.*;
import static org.mockito.Mockito.*;

public class MongoHealthIndicatorTest {

    private MongoTemplate mongoTemplate;
    private MongoHealthIndicator mongoHealthIndicator;

    @BeforeEach
    void setUp() {
        mongoTemplate = mock(MongoTemplate.class);
        mongoHealthIndicator = new MongoHealthIndicator(mongoTemplate);
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

    @Test
    void whenMongoIsDown_thenHealthStatusIsDown() {
        // Arrange
        when(mongoTemplate.executeCommand("{ ping: 1 }"))
                .thenThrow(new RuntimeException("Mongo is down"));

        // Act
        Health health = mongoHealthIndicator.health();

        // Assert
        assertEquals(Status.DOWN, health.getStatus());
        assertEquals("{Database=Unavailable}", health.getDetails().toString());
    }
}
