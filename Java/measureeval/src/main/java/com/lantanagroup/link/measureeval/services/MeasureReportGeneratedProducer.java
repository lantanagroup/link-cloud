package com.lantanagroup.link.measureeval.services;

import com.lantanagroup.link.measureeval.models.MeasureReportGenerated;
import com.lantanagroup.link.shared.kafka.Headers;
import com.lantanagroup.link.shared.kafka.Topics;
import org.apache.kafka.clients.producer.ProducerRecord;
import org.apache.kafka.common.header.internals.RecordHeaders;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.kafka.core.KafkaTemplate;
import org.springframework.stereotype.Service;

@Service
public class MeasureReportGeneratedProducer {
    private static final Logger logger = LoggerFactory.getLogger(MeasureReportGeneratedProducer.class);
    private final KafkaTemplate<MeasureReportGenerated.Key, MeasureReportGenerated.Value> template;

    public MeasureReportGeneratedProducer(KafkaTemplate<MeasureReportGenerated.Key, MeasureReportGenerated.Value> template) {
        this.template = template;
    }

    public void produce(String correlationId, MeasureReportGenerated.Key key, MeasureReportGenerated.Value value) {
        if (logger.isDebugEnabled()) {
            logger.debug("Producing {} record: {}", Topics.MEASURE_REPORT_GENERATED, value.getMeasureReportId());
        }

        org.apache.kafka.common.header.Headers headers = new RecordHeaders()
                .add(Headers.CORRELATION_ID, Headers.getBytes(correlationId));

        template.send(new ProducerRecord<>(
                Topics.MEASURE_REPORT_GENERATED,
                null,
                key,
                value,
                headers));
    }
}
