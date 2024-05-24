package com.lantanagroup.link.measureeval.services;

import com.lantanagroup.link.measureeval.entities.ReportScheduledEntity;
import com.lantanagroup.link.measureeval.kafka.Topics;
import com.lantanagroup.link.measureeval.records.ReportScheduled;
import com.lantanagroup.link.measureeval.repositories.ScheduledReportRepository;
import org.apache.kafka.clients.consumer.ConsumerRecord;
import org.springframework.kafka.annotation.KafkaListener;
import org.springframework.messaging.handler.annotation.Header;
import org.springframework.stereotype.Service;

import java.util.ArrayList;
import java.util.Date;
import java.util.List;

@Service
public class ReportScheduledConsumer {

    private final ScheduledReportRepository scheduledReportRepository;

    public ReportScheduledConsumer(ScheduledReportRepository scheduledReportRepository) {
        this.scheduledReportRepository = scheduledReportRepository;
    }

    @KafkaListener(topics = Topics.REPORT_SCHEDULED)
    public void consumer(@Header("correlationId") String correlationId, ConsumerRecord<ReportScheduled.Key, ReportScheduled> record) {
        var reportScheduledEntity = new ReportScheduledEntity();

        reportScheduledEntity.setFacilityId(record.key().getFacilityId());

        var reportTypes = new ArrayList<String>();
        reportTypes.add(record.key().getReportType());
        reportScheduledEntity.setReportTypes(reportTypes);

        Date periodStart = new Date(record.value().getParameters().stream().filter(p -> p.getKey().equals("periodStart")).findFirst().get().getValue());
        reportScheduledEntity.setPeriodStart(periodStart);

        Date periodEnd = new Date(record.value().getParameters().stream().filter(p -> p.getKey().equals("periodEnd")).findFirst().get().getValue());
        reportScheduledEntity.setPeriodEnd(periodEnd);

        scheduledReportRepository.addOne(reportScheduledEntity);
    }
}
