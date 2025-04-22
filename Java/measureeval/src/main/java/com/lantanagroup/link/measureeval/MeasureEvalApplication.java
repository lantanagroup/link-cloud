package com.lantanagroup.link.measureeval;

import ch.qos.logback.classic.LoggerContext;
import com.lantanagroup.link.measureeval.services.MeasureEvaluatorCache;
import com.lantanagroup.link.measureeval.utils.CqlLogAppender;
import org.slf4j.LoggerFactory;
import org.springframework.boot.Banner;
import org.springframework.boot.SpringApplication;
import org.springframework.boot.autoconfigure.SpringBootApplication;
import org.springframework.boot.context.properties.ConfigurationPropertiesScan;
import org.springframework.context.annotation.Bean;
import org.springframework.context.annotation.ComponentScan;
import org.springframework.data.mongodb.config.EnableMongoAuditing;

import java.util.TimeZone;

@SpringBootApplication
@EnableMongoAuditing
@ComponentScan(basePackages = {"com.lantanagroup.link.measureeval", "com.lantanagroup.link.shared.logging", "com.lantanagroup.link.shared.health"} )
@ConfigurationPropertiesScan("com.lantanagroup.link.shared.config")
public class MeasureEvalApplication {
    public static void main(String[] args) {
        TimeZone.setDefault(TimeZone.getTimeZone("UTC"));
        SpringApplication application = new SpringApplication(MeasureEvalApplication.class);
        application.setBannerMode(Banner.Mode.OFF);
        application.run(args);
    }

    @Bean
    public CqlLogAppender cqlLogAppender(MeasureEvaluatorCache measureEvaluatorCache) {
        LoggerContext loggerContext = (LoggerContext) LoggerFactory.getILoggerFactory();
        return CqlLogAppender.start(loggerContext, measureEvaluatorCache);
    }
}
