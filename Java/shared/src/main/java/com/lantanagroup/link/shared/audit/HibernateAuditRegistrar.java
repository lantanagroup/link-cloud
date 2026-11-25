package com.lantanagroup.link.shared.audit;

import jakarta.persistence.EntityManagerFactory;
import org.hibernate.engine.spi.SessionFactoryImplementor;
import org.hibernate.event.service.spi.EventListenerRegistry;
import org.hibernate.event.spi.EventType;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.beans.factory.ObjectProvider;
import org.springframework.beans.factory.SmartInitializingSingleton;

/**
 * Attaches {@link HibernateAuditChangeListener} to every Hibernate persistence unit the service has, once all
 * of its beans exist. A service with Hibernate on its classpath but no persistence unit attaches nothing. A
 * unit it cannot attach to is reported and left alone: the service starts, and updates through that unit are
 * recorded without property changes.
 */
final class HibernateAuditRegistrar implements SmartInitializingSingleton {
    private static final Logger logger = LoggerFactory.getLogger(HibernateAuditRegistrar.class);

    private final ObjectProvider<EntityManagerFactory> factories;
    private final HibernateAuditChangeListener listener;

    HibernateAuditRegistrar(ObjectProvider<EntityManagerFactory> factories, HibernateAuditChangeListener listener) {
        this.factories = factories;
        this.listener = listener;
    }

    @Override
    public void afterSingletonsInstantiated() {
        int attached = 0;
        try {
            for (EntityManagerFactory factory : factories) {
                if (attach(factory)) {
                    attached++;
                }
            }
        } catch (RuntimeException e) {
            logger.warn("Audit could not look up the service's persistence units: {}", AuditJson.describe(e));
        }
        if (attached > 0) {
            logger.info("Audit captures property changes from {} Hibernate persistence unit(s)", attached);
        }
    }

    private boolean attach(EntityManagerFactory factory) {
        try {
            EventListenerRegistry registry = factory.unwrap(SessionFactoryImplementor.class)
                    .getServiceRegistry()
                    .getService(EventListenerRegistry.class);
            if (registry == null) {
                logger.warn("Audit found no event listener registry in a persistence unit, so updates through it "
                        + "are recorded without property changes");
                return false;
            }
            registry.appendListeners(EventType.POST_COMMIT_UPDATE, listener);
            return true;
        } catch (RuntimeException e) {
            logger.warn("Audit could not attach to a persistence unit, so updates through it are recorded "
                    + "without property changes: {}", AuditJson.describe(e));
            return false;
        }
    }
}
