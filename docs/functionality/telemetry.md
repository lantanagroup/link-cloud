[← Back Home](../README.md)

# Telemetry

Telemetry is a key component of the system, providing observability into the performance, health, and behavior of the services. The system leverages a combination of tools and frameworks to collect, aggregate, and visualize trace, metric, and log data.

## Key Components

- **Open Telemetry (OTEL)**:
    - Used by all services to report trace, metric, and log data.
    - Provides a standardized approach to telemetry data collection.

- **Loki and Prometheus**:
    - Loki: Used for collecting and managing logs.
    - Prometheus: Used for collecting and managing metrics.

- **OTEL Collector**:
    - Aggregates and collects trace, metrics, and logs data into a centralized location.
    - Acts as the intermediary, forwarding data to visualization and analysis tools.

- **Grafana**:
    - Visualizes the trace, metric, and log data.
    - Enables building and sharing dashboards for monitoring and analysis.
    - Dashboards for specific insights are actively being developed and can be exported/imported.

## Current Capabilities

- **Default Telemetry**:
    - Provides insights into CPU and RAM usage for all services.
    - Logs, metrics, and traces are collected and accessible for analysis.

- **Dashboards**:
    - Work is ongoing to create Grafana dashboards that provide actionable insights into system performance and health.

- **Correlation ID for Tracing**:
    - A newly generated correlationId is created when a request is initiated. This correlationId is passed along with subsequent requests, either via REST API headers or through Kafka message headers. This allows trace data to link related requests across services, providing end-to-end visibility into the flow of operations within the system.
    
- **Metrics**:
    - TODO: Document custom metrics that are already being collected.

## Future Enhancements

- Exploration of additional metrics to further understand system health.
- Defining key indicators that provide deeper insights into the performance and reliability of the system.