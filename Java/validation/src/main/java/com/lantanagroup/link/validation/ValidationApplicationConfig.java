package com.lantanagroup.link.validation;

import com.lantanagroup.link.shared.BaseSpringConfig;
import org.springframework.boot.context.properties.ConfigurationPropertiesScan;
import org.springframework.context.annotation.Configuration;

@Configuration
@ConfigurationPropertiesScan("com.lantanagroup.link.shared.config")
public class ValidationApplicationConfig extends BaseSpringConfig {
}
