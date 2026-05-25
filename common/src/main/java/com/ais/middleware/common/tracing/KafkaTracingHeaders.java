package com.ais.middleware.common.tracing;

/**
 * Kafka message header names used for W3C distributed trace context propagation.
 * Every message in the system must carry traceparent so one IssuePolicy command produces a single distributed trace visible in Grafana Tempo.
 */
public final class KafkaTracingHeaders {
    public static final String TRACE_PARENT = "traceparent";
    public static final String TRACE_STATE = "tracestate";
    public static final String CORRELATION_ID = "correlationId";
    public static final String ISSUANCE_ID = "issuanceId";

    private KafkaTracingHeaders() {
    }
}
