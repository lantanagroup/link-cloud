package com.lantanagroup.link.validation.configs;

import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.boot.autoconfigure.condition.ConditionalOnProperty;
import org.springframework.context.annotation.Configuration;
import org.springframework.security.config.annotation.method.configuration.EnableMethodSecurity;

/**
 * Turns on {@code @PreAuthorize} for the service's endpoints, which is how an endpoint says who may call
 * it: the rubric registry and the dry run are for Link administrators (the {@code IsLinkAdmin} permission),
 * as MeasureEval's measure definitions are.
 *
 * <p>It is left off when the service runs with anonymous access ({@code authentication.anonymous=true},
 * local development and the docker-compose stack): there is no caller to check there, and with it on every
 * annotated endpoint would refuse everyone. With the setting absent it is on, as the service's own default
 * for anonymous access is off.
 */
@Configuration
@ConditionalOnProperty(prefix = "authentication", name = "anonymous", havingValue = "false", matchIfMissing = true)
@EnableMethodSecurity(prePostEnabled = true)
public class MethodSecurityConfig {
    private static final Logger logger = LoggerFactory.getLogger(MethodSecurityConfig.class);

    public MethodSecurityConfig() {
        logger.info("Anonymous access is off: endpoint permissions (@PreAuthorize) are enforced");
    }
}
