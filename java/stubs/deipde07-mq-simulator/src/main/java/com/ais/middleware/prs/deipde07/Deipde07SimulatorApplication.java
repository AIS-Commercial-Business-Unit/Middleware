package com.ais.middleware.prs.deipde07;

import org.springframework.boot.SpringApplication;
import org.springframework.boot.autoconfigure.SpringBootApplication;
import org.springframework.jms.annotation.EnableJms;

@SpringBootApplication
@EnableJms
public class Deipde07SimulatorApplication {

    public static void main(String[] args) {
        SpringApplication.run(Deipde07SimulatorApplication.class, args);
    }
}
