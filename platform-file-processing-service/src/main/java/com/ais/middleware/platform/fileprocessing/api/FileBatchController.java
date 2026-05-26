package com.ais.middleware.platform.fileprocessing.api;

import com.ais.middleware.platform.fileprocessing.domain.BatchRecord;
import com.ais.middleware.platform.fileprocessing.domain.BatchRecordRepository;
import com.ais.middleware.platform.fileprocessing.domain.FileBatch;
import com.ais.middleware.platform.fileprocessing.domain.FileBatchRepository;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.beans.factory.annotation.Value;
import org.springframework.http.ResponseEntity;
import org.springframework.web.bind.annotation.*;

import java.io.File;
import java.io.FileWriter;
import java.io.IOException;
import java.time.LocalDate;
import java.time.LocalDateTime;
import java.time.format.DateTimeFormatter;
import java.util.List;
import java.util.Map;

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

    public FileBatchController(FileBatchRepository fileBatchRepository,
                                BatchRecordRepository batchRecordRepository) {
        this.fileBatchRepository = fileBatchRepository;
        this.batchRecordRepository = batchRecordRepository;
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
     * Query param: count (1-50, default 10).
     */
    @PostMapping("/batches/generate")
    public ResponseEntity<Map<String, Object>> generateSampleBatch(
            @RequestParam(defaultValue = "10") int count) {

        count = Math.max(1, Math.min(50, count));

        String timestamp = LocalDateTime.now().format(DateTimeFormatter.ofPattern("yyyyMMdd_HHmmss"));
        String fileName = "RENEWAL_" + timestamp + ".csv";

        File inboundDirFile = new File(inboundDir);
        if (!inboundDirFile.exists()) {
            inboundDirFile.mkdirs();
        }

        File outputFile = new File(inboundDirFile, fileName);
        LocalDate baseExpiry = LocalDate.now().plusMonths(2);

        try (FileWriter fw = new FileWriter(outputFile)) {
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
}
