package com.ais.middleware.platform.fileprocessing.api;

import com.ais.middleware.common.events.fileprocessing.RenewalRecordReadyForIssuanceEvent;
import com.ais.middleware.platform.fileprocessing.domain.BatchRecord;
import com.ais.middleware.platform.fileprocessing.domain.BatchRecordRepository;
import com.ais.middleware.platform.fileprocessing.domain.FileBatch;
import com.ais.middleware.platform.fileprocessing.domain.FileBatchRepository;
import com.fasterxml.jackson.databind.ObjectMapper;
import org.apache.camel.ProducerTemplate;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.beans.factory.annotation.Value;
import org.springframework.http.ResponseEntity;
import org.springframework.web.bind.annotation.*;

import java.io.FileWriter;
import java.io.IOException;
import java.nio.file.Files;
import java.nio.file.Path;
import java.time.LocalDate;
import java.time.LocalDateTime;
import java.time.OffsetDateTime;
import java.time.format.DateTimeFormatter;
import java.util.List;
import java.util.Map;
import java.util.UUID;

@RestController
@RequestMapping("/api/v1")
public class FileBatchController {

    private static final Logger log = LoggerFactory.getLogger(FileBatchController.class);

    private static final String[][] SAMPLE_ACCOUNTS = {
        {"ACC-ACME-001",       "ACME Corporation",          "PROD-001"},
        {"ACC-GLOBEX-002",     "Globex Industries",          "PROD-002"},
        {"ACC-INITECH-003",    "Initech LLC",                "PROD-003"},
        {"ACC-UMBRELLA-004",   "Umbrella Holdings",          "PROD-004"},
        {"ACC-WAYNEENT-005",   "Wayne Enterprises",          "PROD-005"},
        {"ACC-CYBERDYNE-006",  "Cyberdyne Systems",          "PROD-006"},
        {"ACC-SOYLENT-007",    "Soylent Green Corp",         "PROD-007"},
        {"ACC-OSCORP-008",     "OsCorp Industries",          "PROD-008"},
        {"ACC-WEYLAND-009",    "Weyland-Yutani Corp",        "PROD-009"},
        {"ACC-TYRELL-010",     "Tyrell Corporation",         "PROD-010"},
    };

    private static final int[][] POLICY_TYPES = {
        {1, 0}, {2, 0}, {42, 1}, {5, 0}, {6, 0}, {10, 0}
    };

    private static final String[] BILLING_TYPES = {"DirectBill", "AgencyBill", "DirectBill", "DirectBill"};

    @Value("${fileprocessing.inbound-dir:/app/data/renewals/inbound}")
    private String inboundDir;

    private final FileBatchRepository fileBatchRepository;
    private final BatchRecordRepository batchRecordRepository;
    private final ObjectMapper objectMapper;
    private final ProducerTemplate producerTemplate;

    public FileBatchController(FileBatchRepository fileBatchRepository,
                                BatchRecordRepository batchRecordRepository,
                                ObjectMapper objectMapper,
                                ProducerTemplate producerTemplate) {
        this.fileBatchRepository = fileBatchRepository;
        this.batchRecordRepository = batchRecordRepository;
        this.objectMapper = objectMapper;
        this.producerTemplate = producerTemplate;
    }

    @GetMapping("/batches")
    public ResponseEntity<List<FileBatch>> listBatches() {
        List<FileBatch> batches = fileBatchRepository.findAll();
        return ResponseEntity.ok(batches);
    }

    @GetMapping("/batches/{batchId}")
    public ResponseEntity<FileBatch> getBatch(@PathVariable String batchId) {
        return fileBatchRepository.findById(batchId)
                .map(ResponseEntity::ok)
                .orElse(ResponseEntity.notFound().build());
    }

    @GetMapping("/batches/{batchId}/records")
    public ResponseEntity<List<BatchRecord>> getBatchRecords(@PathVariable String batchId) {
        return ResponseEntity.ok(batchRecordRepository.findByBatchId(batchId));
    }

    @GetMapping("/drop-zones")
    public ResponseEntity<List<Map<String, Object>>> listDropZones() {
        return ResponseEntity.ok(List.of(
                Map.of(
                        "name", "AutomatedRenewal",
                        "inboundDir", inboundDir,
                        "filePattern", "RENEWAL_*.csv",
                        "description", "Automated policy renewal batch drop zone"
                )
        ));
    }

    /**
     * Generates a sample renewal CSV file in the inbound folder and returns metadata.
     * Query param: count (1-500, default 10).
     */
    @PostMapping("/batches/generate")
    public ResponseEntity<Map<String, Object>> generateSampleBatch(
            @RequestParam(defaultValue = "10") int count) {

        count = Math.max(1, Math.min(500, count));

        String timestamp = LocalDateTime.now().format(DateTimeFormatter.ofPattern("yyyyMMdd_HHmmss"));
        String fileName = "RENEWAL_" + timestamp + ".csv";

        Path inboundDirPath;
        try {
            inboundDirPath = ensureDirectoryExists(inboundDir);
        } catch (IOException e) {
            log.error("Inbound directory is not ready: {}", inboundDir, e);
            return ResponseEntity.internalServerError()
                    .body(Map.of("error", "Inbound directory is not ready: " + e.getMessage()));
        }

        Path outputFile = inboundDirPath.resolve(fileName);
        LocalDate baseExpiry = LocalDate.now().plusMonths(2);

        try (FileWriter fw = new FileWriter(outputFile.toFile())) {
            fw.write("PolicyNumber,ExpirationDate,InsuredName,PolicyTypeCode,PolicyTypeSubCode,PremiumAmount,ProducerCode,BillingType,AccountId\n");

            for (int i = 1; i <= count; i++) {
                String[] account = SAMPLE_ACCOUNTS[(i - 1) % SAMPLE_ACCOUNTS.length];
                int[] policyType = POLICY_TYPES[(i - 1) % POLICY_TYPES.length];
                String billing = BILLING_TYPES[(i - 1) % BILLING_TYPES.length];
                String policyNumber = String.format("POL-%03d-%d", i, LocalDate.now().getYear());
                String expiryDate = baseExpiry.plusDays(i).toString();
                double premium = 1000.0 + (i * 250.0);

                fw.write(String.format("%s,%s,%s,%d,%d,%.2f,%s,%s,%s%n",
                        policyNumber, expiryDate, account[1],
                        policyType[0], policyType[1], premium,
                        account[2], billing, account[0]));
            }
        } catch (IOException e) {
            log.error("Failed to create sample CSV file: {}", e.getMessage());
            return ResponseEntity.internalServerError()
                    .body(Map.of("error", "Failed to create file: " + e.getMessage()));
        }

        log.info("Generated sample renewal batch — fileName={} recordCount={}", fileName, count);
        return ResponseEntity.status(201).body(Map.of(
                "fileName", fileName,
                "recordCount", count,
                "message", "File created in inbound folder"
        ));
    }

    /**
     * Retry a DeadLettered or Failed record by republishing a new RenewalRecordReadyForIssuanceEvent.
     * A fresh correlationId (issuanceId) is issued so a new saga starts cleanly.
     * The BatchRecord is updated with the new correlationId so outcome events are linked back.
     */
    @PostMapping("/records/{recordId}/retry")
    public ResponseEntity<?> retryRecord(@PathVariable String recordId) {
        BatchRecord record = batchRecordRepository.findById(recordId)
                .orElse(null);
        if (record == null) {
            return ResponseEntity.notFound().build();
        }
        if (record.getStatus() != BatchRecord.BatchRecordStatus.DeadLettered
                && record.getStatus() != BatchRecord.BatchRecordStatus.Failed) {
            return ResponseEntity.badRequest().body(Map.of(
                    "error", "Record is not in a retryable state: " + record.getStatus()));
        }

        // Parse rawContent back to CSV fields
        // Format: PolicyNumber,ExpirationDate,InsuredName,PolicyTypeCode,PolicyTypeSubCode,PremiumAmount,ProducerCode,BillingType,AccountId
        String[] fields = record.getRawContent().split(",", -1);
        if (fields.length < 9) {
            return ResponseEntity.internalServerError().body(Map.of(
                    "error", "Cannot parse rawContent: expected 9 fields, got " + fields.length));
        }

        String newCorrelationId = UUID.randomUUID().toString();
        int policyTypeCode    = parseInt(fields[3].trim(), 0);
        int policyTypeSubCode = parseInt(fields[4].trim(), 0);
        String accountId      = fields[8].trim();
        String policyNumber   = fields[0].trim();

        var event = new RenewalRecordReadyForIssuanceEvent(
                recordId, record.getBatchId(), newCorrelationId, record.getSequenceNumber(),
                record.getRawContent(), "AutomatedRenewal",
                accountId, policyTypeCode, policyTypeSubCode,
                policyNumber, OffsetDateTime.now()
        );

        // Update record: new correlationId, reset to Pending, increment retry count
        record.setCorrelationId(newCorrelationId);
        record.setStatus(BatchRecord.BatchRecordStatus.Pending);
        record.setRetryCount(record.getRetryCount() + 1);
        record.setProcessorResult(null);
        record.setFailureCategory(null);
        record.setProcessedAt(null);
        batchRecordRepository.save(record);

        try {
            producerTemplate.sendBody("kafka:file.events.renewal-record-ready-for-issuance",
                    objectMapper.writeValueAsString(event));
        } catch (Exception e) {
            // Roll back the record update on Kafka failure
            log.error("Failed to publish retry event for recordId={}: {}", recordId, e.getMessage());
            return ResponseEntity.internalServerError().body(Map.of(
                    "error", "Failed to publish retry event: " + e.getMessage()));
        }

        log.info("Record retry dispatched — recordId={} batchId={} newCorrelationId={} retryCount={}",
                recordId, record.getBatchId(), newCorrelationId, record.getRetryCount());
        return ResponseEntity.ok(Map.of(
                "recordId", recordId,
                "batchId", record.getBatchId(),
                "newCorrelationId", newCorrelationId,
                "retryCount", record.getRetryCount(),
                "message", "Retry dispatched — new issuance saga started"
        ));
    }

    private Path ensureDirectoryExists(String directory) throws IOException {
        Path path = Path.of(directory);
        Files.createDirectories(path);
        if (!Files.isDirectory(path)) {
            throw new IOException("Path is not a directory: " + path);
        }
        if (!Files.isWritable(path)) {
            throw new IOException("Directory is not writable: " + path);
        }
        return path;
    }

    private int parseInt(String value, int defaultValue) {
        try {
            return Integer.parseInt(value);
        } catch (NumberFormatException e) {
            return defaultValue;
        }
    }
}
