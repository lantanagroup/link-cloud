package com.lantanagroup.link.shared.audit;

import jakarta.persistence.EntityManagerFactory;
import org.springframework.beans.factory.ObjectProvider;
import org.springframework.boot.autoconfigure.condition.ConditionalOnClass;
import org.springframework.context.annotation.Bean;
import org.springframework.context.annotation.Configuration;

/**
 * The part of the audit mechanism that needs Hibernate: property changes captured from the saves themselves.
 * Kept apart from {@link AuditAutoConfiguration} and behind a condition on class names, so a service without
 * Hibernate never loads a class that mentions it.
 */
@Configuration(proxyBeanMethods = false)
@ConditionalOnClass(name = {
        "org.hibernate.event.spi.PostCommitUpdateEventListener",
        "jakarta.persistence.EntityManagerFactory"
})
class AuditJpaConfiguration {
    @Bean
    HibernateAuditChangeListener hibernateAuditChangeListener(AuditDiff auditDiff) {
        return new HibernateAuditChangeListener(auditDiff);
    }

    @Bean
    HibernateAuditRegistrar hibernateAuditRegistrar(ObjectProvider<EntityManagerFactory> factories,
                                                    HibernateAuditChangeListener listener) {
        return new HibernateAuditRegistrar(factories, listener);
    }
}
