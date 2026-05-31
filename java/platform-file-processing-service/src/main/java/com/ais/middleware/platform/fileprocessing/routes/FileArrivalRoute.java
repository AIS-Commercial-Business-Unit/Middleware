package com.ais.middleware.platform.fileprocessing.routes;

import com.ais.middleware.common.events.fileprocessing.*;
import com.ais.middleware.platform.fileprocessing.domain.*;
import com.fasterxml.jackson.databind.ObjectMapper;
import com.opencsv.CSVReader;
import com.opencsv.exceptions.CsvException;
import org.apache.camel.Exchange;
import org.apache.camel.ProducerTemplate;
import org.apache.camel.builder.RouteBuilder;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.slf4j.MDC;
import org.springframework.beans.factory.annotation.Value;
import org.springframework.stereotype.Component;

import java.io.StringReader;
import java.time.OffsetDateTime;
import java.util.ArrayList;
import java.util.List;
import java.util.UUID;

/**
 * Camel route that polls the inbound drop-zone directory for CSV files.
 * Each file triggers: parse → create BatchRecords → publish RenewalRecordReadyForIssuanceEvents.
 */
@Component
public class FileArrivalRoute extends RouteBuilder {

    private static final Logger log = LoggerFactory.getLogger(FileArrivalRoute.class);

    private static final String[] EXPECTED_HEADERS = {
        "PolicyNumber", "ExpirationDate", "InsuredName", "PolicyTypeCode",
        "PolicyTypeSubCode", "PremiumAmount", "ProducerCode", "BillingType", "AccountId"
    };

    @Value("${fileprocessing.inbound-dir:/app/data/renewals/inbound}")
    private String inboundDir;

    @Value("${fileprocessing.processed-dir:/app/data/renewals/processed}")
    private String processedDir;

    @Value("${fileprocessing.error-dir:/app/data/renewals/error}")
    private String errorDir;

    private final FileBatchRepository fileBatchRepository;
    private final BatchRecordRepository batchRecordRepository;
    private final ObjectMapper objectMapper;
    private final ProducerTemplate producerTemplate;
    private final FailureSimulator failureSimulator;

    public FileArrivalRoute(FileBatchRepository fileBatchRepository,
                            BatchRecordRepository batchRecordRepository,
                            ObjectMapper objectMapper,
                            ProducerTemplate producerTemplate,
                            FailureSimulator failureSimulator) {
        this.fileBatchRepository = fileBatchRepository;
        this.batchRecordRepository = batchRecordRepository;
        this.objectMapper = objectMapper;
        this.producerTemplate = producerTemplate;
        this.failureSimulator = failureSimulator;
    }

    @Override
    public void configure() {
        // File consumer DLQ handler: log at ERROR level and publish to DLQ topic.
        // NOTE: handled(true) is intentionally OMITTED so the exception propagates back to the
        // Camel file consumer, which then moves the file to moveFailed (errorDir). Using
        // handled(true) here would cause the file to be moved to move (processedDir) instead,
        // silently losing the error file.
        onException(Exception.class)
            .process(exchange -> {
                Exception cause = exchange.getProperty(Exchange.EXCEPTION_CAUGHT, Exception.class);
                String fileName = exchange.getIn().getHeader(Exchange.FILE_NAME, "unknown", String.class);
                log.error("File processing failed — file will be moved to errorDir. fileName={} error={}",
                        fileName, cause != null ? cause.getMessage() : "unknown", cause);
                exchange.getIn().setHeader("X-DLQ-Error", cause != null ? cause.getMessage() : "unknown");
                exchange.getIn().setHeader("X-DLQ-FileName", fileName);
                exchange.getIn().setBody("{\"fileName\":\"" + fileName.replace("\"", "\\\"")
                        + "\",\"error\":\"file-processing-failed\"}");
            })
            .to("kafka:file.dlq.file-arrival");

        from("file://" + inboundDir
                + "?include=RENEWAL_.*\\.csv"
                + "&move=" + processedDir
                + "&moveFailed=" + errorDir
                + "&delay=5000"
                + "&maxMessagesPerPoll=10"
                + "&readLock=changed"
                + "&readLockCheckInterval=2000"
                + "&charset=UTF-8")
            .routeId("file-arrival")
            .process(this::processFile)
            .split(body())
                .parallelProcessing(false)
                .to("kafka:file.events.renewal-record-ready-for-issuance")
            .end();
    }

    private void processFile(Exchange exchange) throws Exception {
        String fileName = exchange.getIn().getHeader(Exchange.FILE_NAME, String.class);
        String fileBody = exchange.getIn().getBody(String.class);
        long fileSize = fileBody != null ? fileBody.length() : 0;

        String batchId = UUID.randomUUID().toString();
        MDC.put("batchId", batchId);
        log.info("File arrived — batchId={} fileName={}", batchId, fileName);

        // Create FileBatch in Received state
        FileBatch batch = new FileBatch();
        batch.setBatchId(batchId);
        batch.setFileName(fileName);
        batch.setDropZoneName("AutomatedRenewal");
        batch.setFileType("AutomatedRenewal");
        batch.setFileLocationReference(inboundDir + "/" + fileName);
        batch.setFileSizeBytes(fileSize);
        batch.setStatus(FileBatch.FileBatchStatus.Received);
        batch.setReceivedAt(OffsetDateTime.now());
        batch.setProcessingMode("Parallel");
        fileBatchRepository.save(batch);

        // Parse CSV
        batch.setStatus(FileBatch.FileBatchStatus.Parsing);
        fileBatchRepository.save(batch);

        List<String> eventJsonList = new ArrayList<>();
        List<RenewalRecordFailedEvent> preFailedEvents = new ArrayList<>();
        int sequenceNumber = 0;

        try (CSVReader reader = new CSVReader(new StringReader(fileBody))) {
            String[] header = reader.readNext();
            if (header == null) {
                log.error("Empty CSV file — batchId={} fileName={}", batchId, fileName);
                batch.setStatus(FileBatch.FileBatchStatus.Failed);
                fileBatchRepository.save(batch);
                exchange.getIn().setBody(eventJsonList);
                MDC.clear();
                return;
            }

            validateHeader(header);

            String[] row;
            while ((row = reader.readNext()) != null) {
                if (row.length < EXPECTED_HEADERS.length) {
                    log.warn("Skipping malformed row at seq={} — not enough columns", sequenceNumber + 1);
                    continue;
                }
                sequenceNumber++;

                String recordId = UUID.randomUUID().toString();
                String correlationId = UUID.randomUUID().toString(); // becomes issuanceId
                String rawContent = String.join(",", row);

                String policyNumber   = row[0].trim();
                // row[1] = ExpirationDate, row[2] = InsuredName
                int policyTypeCode    = parseInt(row[3].trim(), 0);
                int policyTypeSubCode = parseInt(row[4].trim(), 0);
                // row[5] = PremiumAmount, row[6] = ProducerCode, row[7] = BillingType
                String accountId      = row[8].trim();

                // Persist BatchRecord
                BatchRecord record = new BatchRecord();
                record.setRecordId(recordId);
                record.setBatchId(batchId);
                record.setSequenceNumber(sequenceNumber);
                record.setRawContent(rawContent);
                record.setCorrelationId(correlationId);

                // Check for simulated failures before dispatching to issuance
                FailureSimulator.FailureResult failure = failureSimulator.evaluate(sequenceNumber, policyTypeCode, accountId);
                if (failure.shouldFail()) {
                    record.setStatus(BatchRecord.BatchRecordStatus.DeadLettered);
                    record.setProcessorResult(failure.reason());
                    record.setFailureCategory(failure.category());
                    record.setProcessedAt(OffsetDateTime.now());
                    batchRecordRepository.save(record);
                    log.warn("Record pre-failed [{}] — seq={} batchId={} reason={}",
                            failure.category(), sequenceNumber, batchId, failure.reason());
                    // Collect for deferred publish (after batch totalRecords is saved)
                    preFailedEvents.add(new RenewalRecordFailedEvent(
                            recordId, batchId, correlationId,
                            failure.reason(), failure.category(), OffsetDateTime.now()));
                    continue;
                }

                record.setStatus(BatchRecord.BatchRecordStatus.Pending);
                batchRecordRepository.save(record);

                // Build event
                var event = new RenewalRecordReadyForIssuanceEvent(
                        recordId, batchId, correlationId, sequenceNumber,
                        rawContent, "AutomatedRenewal",
                        accountId, policyTypeCode, policyTypeSubCode,
                        policyNumber, OffsetDateTime.now()
                );
                logEdaFlow(correlationId, "RenewalRecordReadyForIssuanceEvent",
                        "FileProcessing", "PolicyIssuance",
                        "file.events.renewal-record-ready-for-issuance", "published");
                eventJsonList.add(objectMapper.writeValueAsString(event));
            }
        } catch (CsvException e) {
            log.error("CSV parse error — batchId={} error={}", batchId, e.getMessage());
            batch.setStatus(FileBatch.FileBatchStatus.Failed);
            fileBatchRepository.save(batch);
            exchange.getIn().setBody(eventJsonList);
            MDC.clear();
            return;
        }

        // Update FileBatch to Processing (totalRecords includes ALL records: both successful and pre-failed)
        batch.setTotalRecords(sequenceNumber);
        batch.setParsingCompletedAt(OffsetDateTime.now());
        batch.setStatus(FileBatch.FileBatchStatus.Processing);
        fileBatchRepository.save(batch);

        // Publish FileBatchStartedEvent via producer template
        var startedEvent = new FileBatchStartedEvent(
                batchId, fileName, "AutomatedRenewal", sequenceNumber, "AutomatedRenewal", OffsetDateTime.now()
        );
        log.info("Batch parsed — batchId={} totalRecords={} preFailedRecords={} — dispatching record events",
                batchId, sequenceNumber, preFailedEvents.size());
        producerTemplate.sendBody("kafka:file.events.file-batch-started",
                        objectMapper.writeValueAsString(startedEvent));

        // Publish pre-failed events now that totalRecords is persisted — RecordOutcomeRoute will
        // pick these up, increment counters, and check batch completion correctly.
        for (RenewalRecordFailedEvent failedEvent : preFailedEvents) {
            logEdaFlow(failedEvent.issuanceId(), "RenewalRecordFailedEvent",
                    "FileProcessing", "PolicyIssuance",
                    "policy.events.renewal-record-failed", "published");
            producerTemplate.sendBody("kafka:policy.events.renewal-record-failed",
                    objectMapper.writeValueAsString(failedEvent));
        }

        exchange.getIn().setBody(eventJsonList);
        MDC.clear();
    }

    private void logEdaFlow(String issuanceId, String messageType, String from, String to, String topic, String direction) {
        MDC.put("EDA_Event", "EDA_FLOW");
        MDC.put("EDA_IssuanceId", issuanceId);
        MDC.put("EDA_MessageType", messageType);
        MDC.put("EDA_From", from);
        MDC.put("EDA_To", to);
        MDC.put("EDA_Topic", topic);
        MDC.put("EDA_Direction", direction);
        MDC.put("EDA_Stack", "java");
        try {
            log.info("EDA_FLOW {} {} -> {}", messageType, from, to);
        } finally {
            MDC.remove("EDA_Event");
            MDC.remove("EDA_IssuanceId");
            MDC.remove("EDA_MessageType");
            MDC.remove("EDA_From");
            MDC.remove("EDA_To");
            MDC.remove("EDA_Topic");
            MDC.remove("EDA_Direction");
            MDC.remove("EDA_Stack");
        }
    }

    private void validateHeader(String[] header) {
        for (int i = 0; i < EXPECTED_HEADERS.length; i++) {
            if (i >= header.length || !EXPECTED_HEADERS[i].equalsIgnoreCase(header[i].trim())) {
                throw new IllegalArgumentException(
                        "Invalid CSV header at column " + i + ": expected=" + EXPECTED_HEADERS[i]
                        + " actual=" + (i < header.length ? header[i] : "<missing>"));
            }
        }
    }

    private int parseInt(String value, int defaultValue) {
        try {
            return Integer.parseInt(value);
        } catch (NumberFormatException e) {
            return defaultValue;
        }
    }
}
