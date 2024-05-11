package com.lantanagroup.link.validation;

import com.lantanagroup.link.validation.config.ArtifactConfig;
import org.springframework.boot.Banner;
import org.springframework.boot.SpringApplication;
import org.springframework.boot.autoconfigure.SpringBootApplication;
import org.springframework.boot.context.properties.EnableConfigurationProperties;
import org.springframework.data.jpa.repository.config.EnableJpaRepositories;

@SpringBootApplication
@EnableJpaRepositories
@EnableConfigurationProperties({ArtifactConfig.class})
public class ValidationApplication {
    public static void main(String[] args) {
        SpringApplication application = new SpringApplication(ValidationApplication.class);
        application.setBannerMode(Banner.Mode.OFF);
        application.run(args);
    }
}
