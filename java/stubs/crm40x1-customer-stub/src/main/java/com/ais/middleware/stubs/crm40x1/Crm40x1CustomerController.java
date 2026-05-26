package com.ais.middleware.stubs.crm40x1;

import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.http.ResponseEntity;
import org.springframework.web.bind.annotation.PostMapping;
import org.springframework.web.bind.annotation.RequestBody;
import org.springframework.web.bind.annotation.RequestMapping;
import org.springframework.web.bind.annotation.RestController;

import java.time.OffsetDateTime;
import java.util.Map;

@RestController
@RequestMapping("/customer")
public class Crm40x1CustomerController {
    private static final Logger log = LoggerFactory.getLogger(Crm40x1CustomerController.class);

    @PostMapping("/update")
    public ResponseEntity<Map<String, Object>> updateCustomer(@RequestBody(required = false) String body) {
        log.info("[CRM40X1] Customer record updated");
        return ResponseEntity.ok(Map.of(
            "status", "Updated",
            "updatedAt", OffsetDateTime.now().toString()
        ));
    }
}
