package com.ais.middleware.stubs.crm19x1;

import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.http.ResponseEntity;
import org.springframework.web.bind.annotation.PostMapping;
import org.springframework.web.bind.annotation.RequestBody;
import org.springframework.web.bind.annotation.RequestMapping;
import org.springframework.web.bind.annotation.RestController;

import java.time.OffsetDateTime;
import java.util.Map;
import java.util.UUID;

@RestController
@RequestMapping("/billing")
public class Crm19x1BillingController {
    private static final Logger log = LoggerFactory.getLogger(Crm19x1BillingController.class);

    @PostMapping("/associate")
    public ResponseEntity<Map<String, Object>> associateBilling(@RequestBody(required = false) String body) {
        String billingAccountId = "BILL-" + UUID.randomUUID().toString().substring(0, 8).toUpperCase();
        log.info("[CRM19X1] Billing account associated: {}", billingAccountId);
        return ResponseEntity.ok(Map.of(
            "billingAccountId", billingAccountId,
            "status", "Associated",
            "createdAt", OffsetDateTime.now().toString()
        ));
    }
}
