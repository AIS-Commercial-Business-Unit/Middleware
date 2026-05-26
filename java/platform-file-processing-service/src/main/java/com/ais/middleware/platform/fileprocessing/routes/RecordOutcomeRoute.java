package com.ais.middleware.platform.fileprocessing.routes;

import com.ais.middleware.common.events.fileprocessing.*;
import com.ais.middleware.platform.fileprocessing.domain.*;
import com.fasterxml.jackson.databind.ObjectMapper;
import org.apache.camel.Exchange;
import org.apache.camel.ProducerTemplate;
import org.apache.camel.builder.RouteBuilder;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.slf4j.MDC;
import org.springframework.stereotype.Component;

import java.time.OffsetDateTime;

/**
 * Listens for RenewalRecordProcessed and RenewalRecordFailed outcomes from policy-issuance-service,
 * and updates batch progress counters. Publishes progress and completion events at thresholds.
 */
@Component
public class RecordOutcomeRoute extends RouteBuilder {

    private static final Logger log = LoggerFactory.getLogger(RecordOutcomeRoute.class);

    private final FileBatchRepository fileBatchRepository;
    private final BatchRecordRepository batchRecordRepository;
    private final ObjectMapper objectMapper;
    private final ProducerTemplate producerTemplate;

    public RecordOutcomeRoute(FileBatchRepository fileBatchRepository,
                               BatchRecordRepository batchRecordRepository,
                               ObjectMapper objectMapper,
                               ProducerTemplate producerTemplate) {
        this.fileBatchRepository = fileBatchRepository;
        this.batchRecordRepository = batchRecordRepository;
        this.objectMapper = objectMapper;
        this.producerTemplate = producerTemplate;
    }

    @Override
    public void configure() {

        // Global DLQ handler: 2 retries with exponential backoff, then dead-letter.
        onException(Exception.class)
            .maximumRedeliveries(2)
            .redeliveryDelay(1000)
            .backOffMultiplier(2)
            .useExponentialBackOff()
            .handled(true)
            .process(exchange -> {
                Exception cause = exchange.getProperty(Exchange.EXCEPTION_CAUGHT, Exception.class);
                log.error("Unhandled exception in record-outcome route — routing to DLQ. routeId={} error={}",
                        exchange.getFromRouteId(),
                        cause != null ? cause.getMessage() : "unknown", cause);
                exchange.getIn().setHeader("X-DLQ-Error", cause != null ? cause.getMessage() : "unknown");
                exchange.getIn().setHeader("X-DLQ-RouteId", exchange.getFromRouteId());
            })
            .to("kafka:file.dlq.record-outcome");

        // Route 1: RenewalRecordProcessed
        from("kafka:policy.events.renewal-record-processed?groupId=platform-file-processing-service")
            .routeId("record-outcome-processed")
            .process(exchange -> {
                String json = exchange.getIn().getBody(String.class);
                RenewalRecordProcessedEvent event = objectMapper.readValue(json, RenewalRecordProcessedEvent.class);
                MDC.put("batchId", event.batchId());
                MDC.put("recordId", event.recordId());
                MDC.put("issuanceId", event.issuanceId());

                batchRecordRepository.findByCorrelationId(event.issuanceId()).ifPresent(record -> {
                    record.setStatus(BatchRecord.BatchRecordStatus.Succeeded);
                    record.setProcessedAt(OffsetDateTime.now());
                    batchRecordRepository.save(record);
                });

                fileBatchRepository.incrementCounters(event.batchId(), true);
                log.info("Record succeeded — batchId={} recordId={}", event.batchId(), event.recordId());
                checkBatchProgress(event.batchId(), producerTemplate);
                MDC.clear();
            });

        // Route 2: RenewalRecordFailed
        from("kafka:policy.events.renewal-record-failed?groupId=platform-file-processing-service")
            .routeId("record-outcome-failed")
            .process(exchange -> {
                String json = exchange.getIn().getBody(String.class);
                RenewalRecordFailedEvent event = objectMapper.readValue(json, RenewalRecordFailedEvent.class);
                MDC.put("batchId", event.batchId());
                MDC.put("recordId", event.recordId());
                MDC.put("issuanceId", event.issuanceId());

                batchRecordRepository.findByCorrelationId(event.issuanceId()).ifPresent(record -> {
                    record.setStatus(BatchRecord.BatchRecordStatus.DeadLettered);
                    record.setProcessedAt(OffsetDateTime.now());
                    record.setProcessorResult(event.failureReason());
                    batchRecordRepository.save(record);
                });

                fileBatchRepository.incrementCounters(event.batchId(), false);
                log.warn("Record failed — batchId={} recordId={} reason={}", event.batchId(), event.recordId(), event.failureReason());
                checkBatchProgress(event.batchId(), producerTemplate);
                MDC.clear();
            });
    }

    private void checkBatchProgress(String batchId, ProducerTemplate pt) throws Exception {
        FileBatch batch = fileBatchRepository.findById(batchId).orElse(null);
        if (batch == null) {
            log.warn("FileBatch not found for progress check — batchId={}", batchId);
            return;
        }

        int processed = batch.getProcessedRecords();
        int total = batch.getTotalRecords() != null ? batch.getTotalRecords() : 0;
        if (total == 0) return;

        double percentComplete = (double) processed / total * 100.0;

        if (processed % 5 == 0 || processed == total) {
            var progressEvent = new FileBatchProgressUpdatedEvent(
                    batchId, batch.getFileName(),
                    processed, batch.getSucceededRecords(), batch.getFailedRecords(),
                    total, percentComplete, OffsetDateTime.now()
            );
            pt.sendBody("kafka:file.events.file-batch-progress-updated",
                    objectMapper.writeValueAsString(progressEvent));
            log.info("Batch progress — batchId={} processed={}/{}", batchId, processed, total);
        }

        if (processed == total) {
            if (batch.getFailedRecords() == 0) {
                batch.setStatus(FileBatch.FileBatchStatus.Completed);
                batch.setProcessingCompletedAt(OffsetDateTime.now());
                fileBatchRepository.save(batch);

                var completedEvent = new FileBatchCompletedEvent(
                        batchId, batch.getFileName(), total, batch.getSucceededRecords(), OffsetDateTime.now()
                );
                pt.sendBody("kafka:file.events.file-batch-completed",
                        objectMapper.writeValueAsString(completedEvent));
                log.info("Batch COMPLETED — batchId={} totalRecords={}", batchId, total);
            } else {
                batch.setStatus(FileBatch.FileBatchStatus.PartialFailure);
                batch.setProcessingCompletedAt(OffsetDateTime.now());
                fileBatchRepository.save(batch);

                var partialEvent = new FileBatchPartialFailureEvent(
                        batchId, batch.getFileName(), total,
                        batch.getSucceededRecords(), batch.getFailedRecords(), OffsetDateTime.now()
                );
                pt.sendBody("kafka:file.events.file-batch-partial-failure",
                        objectMapper.writeValueAsString(partialEvent));
                log.warn("Batch PARTIAL FAILURE — batchId={} succeeded={} failed={}", batchId,
                        batch.getSucceededRecords(), batch.getFailedRecords());
            }
        }
    }
}
