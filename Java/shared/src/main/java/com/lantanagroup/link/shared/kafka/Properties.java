package com.lantanagroup.link.shared.kafka;

public class Properties {
    /**
     * How long the <em>health</em> producer blocks when no broker answers. Read only by the
     * {@code healthKafkaTemplate} beans the services register for
     * {@link com.lantanagroup.link.shared.health.KafkaHealthCheckIndicator}; the templates that
     * carry real traffic keep Kafka's own default.
     * <p>
     * Kept small because Spring evaluates health indicators sequentially on the request thread and
     * the Admin BFF abandons a service's {@code /health} at its own deadline, reporting it
     * Unhealthy with no entries at all -- so a slow Kafka check hides whatever the other
     * indicators were about to say. At the previous 3000 (4000 including the margin on the
     * {@code get} below) an unreachable broker alone left under a second for everything else, and
     * a broker plus any one slow backend went over the deadline and blanked the dashboard.
     */
    public static final long MAX_BLOCK_MS_CONFIG = 1000; // in millis
}
