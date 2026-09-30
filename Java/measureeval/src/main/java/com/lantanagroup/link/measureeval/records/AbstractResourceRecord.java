package com.lantanagroup.link.measureeval.records;

import com.fasterxml.jackson.annotation.JsonSetter;
import com.fasterxml.jackson.annotation.Nulls;
import com.lantanagroup.link.measureeval.entities.QueryType;
import com.lantanagroup.link.measureeval.entities.ReportableEvent;
import lombok.Getter;
import lombok.Setter;

import java.util.ArrayList;
import java.util.Date;
import java.util.List;

@Getter
@Setter
public abstract class AbstractResourceRecord {

    private QueryType queryType;

    private ReportableEvent reportableEvent;

    @JsonSetter(nulls = Nulls.AS_EMPTY)
    private List<ScheduledReport> scheduledReports = new ArrayList<>();

    // cacheType was removed from the contract (LEGLINK-1279): the store is no longer chosen by
    // the message — reads are Redis-first with ABS fallback, and cleanup covers both stores.
    // Records from producers that still stamp it deserialize fine: FAIL_ON_UNKNOWN_PROPERTIES is
    // off (Spring Boot default, not overridden), so replayed and retry-topic messages are safe.

    private String cacheKey;

    @Getter
    @Setter
    public static class ScheduledReport {
        private String[] reportTypes;
        private Date startDate;
        private Date endDate;
        private String frequency;
        private String reportTrackingId;
    }
}
