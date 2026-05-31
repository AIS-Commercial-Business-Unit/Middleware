package com.ais.middleware.prs.deipde07.config;

import org.springframework.context.annotation.Configuration;

/**
 * JMS configuration is provided via Spring Boot Artemis autoconfiguration
 * driven by spring.artemis.* properties in application.yml.
 * No manual bean wiring is needed; this class is a placeholder for future
 * pool or session-factory customisation.
 */
@Configuration
public class JmsConfig {
    // Spring Boot's ArtemisAutoConfiguration creates the ConnectionFactory
    // and JmsTemplate automatically from application.yml properties.
}
