package com.lantanagroup.link.validation.configs;

import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.config.YamlPropertiesFactoryBean;
import org.springframework.core.io.ClassPathResource;

import java.util.Properties;

import static org.junit.jupiter.api.Assertions.assertEquals;

/**
 * Validation's string columns are varchar, so the driver must send string parameters as varchar for SQL Server to
 * seek on them. See dev-docs/validation-category-counts.md.
 */
class DataSourceSettingsTest {

    @Test
    void applicationYml_sendsStringParametersAsVarchar() {
        YamlPropertiesFactoryBean yaml = new YamlPropertiesFactoryBean();
        yaml.setResources(new ClassPathResource("application.yml"));
        Properties properties = yaml.getObject();

        assertEquals("false",
                String.valueOf(properties.get(
                        "spring.datasource.hikari.data-source-properties.sendStringParametersAsUnicode")),
                "Without this setting, every lookup on a varchar column scans its index instead of seeking");
    }
}
