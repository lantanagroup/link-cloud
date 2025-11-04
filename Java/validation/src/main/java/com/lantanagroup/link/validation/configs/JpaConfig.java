package com.lantanagroup.link.validation.configs;

import org.springframework.context.annotation.Configuration;
import org.springframework.data.jpa.repository.config.EnableJpaRepositories;

@Configuration
@EnableJpaRepositories(basePackages = "com.lantanagroup.link.validation.repositories")
public class JpaConfig {
}
