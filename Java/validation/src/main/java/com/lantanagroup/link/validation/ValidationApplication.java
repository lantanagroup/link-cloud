package com.lantanagroup.link.validation;

import org.springframework.boot.Banner;
import org.springframework.boot.SpringApplication;
import org.springframework.boot.autoconfigure.SpringBootApplication;
import org.springframework.context.annotation.PropertySource;

@SpringBootApplication
@PropertySource("classpath:application.yml")
public class ValidationApplication {
    public static void main(String[] args) {
        SpringApplication application = new SpringApplication(ValidationApplication.class);
        application.setBannerMode(Banner.Mode.OFF);
        application.run(args);
    }
}
