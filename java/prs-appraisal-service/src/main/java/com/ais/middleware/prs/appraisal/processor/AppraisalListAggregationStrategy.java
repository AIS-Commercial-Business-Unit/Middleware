package com.ais.middleware.prs.appraisal.processor;

import com.ais.middleware.prs.appraisal.domain.AppraisalListItem;
import com.ais.middleware.prs.appraisal.domain.AppraisalListResponse;
import org.apache.camel.AggregationStrategy;
import org.apache.camel.Exchange;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;

import java.util.ArrayList;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;

/**
 * Merges and deduplicates AppraisalListItem results from the @Work and DEIPDE07 branches
 * of the GetAppraisalList scatter-gather.
 *
 * Deduplication key: (streetAdr + policyQuoteNbr) — normalised to uppercase (BR-APR-003).
 * partialResult: true if the DEIPDE07 branch set "mqPartialResult"=true (BR-APR-002).
 *
 * The aggregated body is set to an AppraisalListResponse record.
 */
public class AppraisalListAggregationStrategy implements AggregationStrategy {

    private static final Logger log = LoggerFactory.getLogger(AppraisalListAggregationStrategy.class);

    @Override
    public Exchange aggregate(Exchange oldExchange, Exchange newExchange) {
        if (oldExchange == null) {
            // First branch result — just return it as the accumulator
            return newExchange;
        }

        // Collect items from both exchanges
        List<AppraisalListItem> allItems = new ArrayList<>();
        boolean partialResult = false;

        allItems.addAll(extractItems(oldExchange));
        allItems.addAll(extractItems(newExchange));

        // partialResult flag comes from the DEIPDE07 branch processor
        if (Boolean.TRUE.equals(oldExchange.getProperty("mqPartialResult", Boolean.class))
                || Boolean.TRUE.equals(newExchange.getProperty("mqPartialResult", Boolean.class))) {
            partialResult = true;
        }

        // Deduplicate by (streetAdr + policyQuoteNbr) — last-in wins, preserves ordering
        Map<String, AppraisalListItem> deduped = new LinkedHashMap<>();
        for (AppraisalListItem item : allItems) {
            String key = normalise(item.streetAdr()) + "|" + normalise(item.policyQuoteNbr());
            deduped.put(key, item);
        }

        List<AppraisalListItem> mergedItems = new ArrayList<>(deduped.values());

        String policyNumber = oldExchange.getIn().getHeader("policyNumber", String.class);
        AppraisalListResponse response = new AppraisalListResponse(policyNumber, mergedItems, partialResult);

        log.info("AppraisalListAggregationStrategy.merge: policyNumber={} total={} partialResult={}",
            policyNumber, mergedItems.size(), partialResult);

        oldExchange.getIn().setBody(response);
        oldExchange.setProperty("mqPartialResult", partialResult);
        return oldExchange;
    }

    @SuppressWarnings("unchecked")
    private List<AppraisalListItem> extractItems(Exchange exchange) {
        Object body = exchange.getIn().getBody();
        if (body instanceof AppraisalListResponse r) {
            return r.items() != null ? r.items() : List.of();
        }
        if (body instanceof List<?> list) {
            return (List<AppraisalListItem>) list;
        }
        return List.of();
    }

    private String normalise(String value) {
        return value == null ? "" : value.toUpperCase().trim();
    }
}
