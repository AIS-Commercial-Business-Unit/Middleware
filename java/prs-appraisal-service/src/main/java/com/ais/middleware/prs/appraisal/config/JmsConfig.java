package com.ais.middleware.prs.appraisal.config;

import org.apache.camel.component.jms.JmsComponent;
import org.springframework.context.annotation.Bean;
import org.springframework.context.annotation.Configuration;
import org.springframework.jms.annotation.EnableJms;
import org.springframework.jms.config.DefaultJmsListenerContainerFactory;
import org.springframework.jms.config.JmsListenerContainerFactory;

import jakarta.jms.ConnectionFactory;

/**
 * JMS configuration for the ActiveMQ Artemis broker.
 *
 * Uses Spring Boot's auto-configured Artemis ConnectionFactory.
 * The Camel JMS component is wired to the same factory so that all
 * Camel JMS send/receive operations share the managed connection pool.
 *
 * Production swap: replace the Spring Artemis auto-config with an IBM MQ
 * ConnectionFactory bean. The Camel route DSL is identical — only this
 * config class changes.
 */
@Configuration
@EnableJms
public class JmsConfig {

    /**
     * JmsListenerContainerFactory for @JmsListener annotations (simulator/other components).
     * prs-appraisal-service uses ConsumerTemplate in processors instead of @JmsListener,
     * but this factory is required by Spring JMS if any @JmsListener is present.
     */
    @Bean
    public JmsListenerContainerFactory<?> jmsListenerContainerFactory(ConnectionFactory connectionFactory) {
        DefaultJmsListenerContainerFactory factory = new DefaultJmsListenerContainerFactory();
        factory.setConnectionFactory(connectionFactory);
        factory.setSessionTransacted(false);
        return factory;
    }

    /**
     * Camel JMS component wired to the Spring Boot auto-configured Artemis ConnectionFactory.
     * Route DSL references "jms:queue:..." and resolves to this component.
     */
    @Bean
    public JmsComponent jms(ConnectionFactory connectionFactory) {
        JmsComponent jmsComponent = new JmsComponent();
        jmsComponent.setConnectionFactory(connectionFactory);
        return jmsComponent;
    }
}
