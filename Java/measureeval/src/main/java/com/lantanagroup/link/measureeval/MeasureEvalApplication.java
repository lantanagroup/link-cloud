package com.lantanagroup.link.measureeval;

import org.springframework.boot.Banner;
import org.springframework.boot.SpringApplication;
import org.springframework.boot.autoconfigure.SpringBootApplication;
import org.springframework.boot.context.properties.ConfigurationPropertiesScan;
import org.springframework.data.mongodb.config.EnableMongoAuditing;

@SpringBootApplication
@EnableMongoAuditing
@ConfigurationPropertiesScan("com.lantanagroup.link.shared.config")
public class MeasureEvalApplication {
    public static void main(String[] args) {
        SpringApplication application = new SpringApplication(MeasureEvalApplication.class);
        application.setBannerMode(Banner.Mode.OFF);
        application.run(args);
    }
}
