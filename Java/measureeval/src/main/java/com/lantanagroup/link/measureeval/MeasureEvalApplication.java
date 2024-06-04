package com.lantanagroup.link.measureeval;

import org.springframework.boot.Banner;
import org.springframework.boot.SpringApplication;
import org.springframework.boot.autoconfigure.SpringBootApplication;
import org.springframework.data.mongodb.config.EnableMongoAuditing;

import java.util.TimeZone;

@SpringBootApplication
@EnableMongoAuditing
public class MeasureEvalApplication {
    public static void main(String[] args) {
        SpringApplication application = new SpringApplication(MeasureEvalApplication.class);
        application.setBannerMode(Banner.Mode.OFF);
        application.run(args);

        //enforce UTC across service
        TimeZone.setDefault(TimeZone.getTimeZone("UTC"));
    }
}
