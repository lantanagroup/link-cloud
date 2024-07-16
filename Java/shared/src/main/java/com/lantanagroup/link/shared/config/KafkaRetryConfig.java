package com.lantanagroup.link.shared.config;

import lombok.Getter;
import lombok.Setter;
import org.springframework.boot.context.properties.ConfigurationProperties;

@ConfigurationProperties(prefix="spring.kafka.retry")
@Getter
@Setter
public class KafkaRetryConfig {

  private int maxAttempts;

  private long retryBackoffMs;

}
