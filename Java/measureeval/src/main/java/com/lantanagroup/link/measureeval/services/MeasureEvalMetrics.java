package com.lantanagroup.link.measureeval.services;

import io.opentelemetry.api.OpenTelemetry;
import io.opentelemetry.api.common.Attributes;
import io.opentelemetry.api.metrics.LongCounter;
import io.opentelemetry.api.metrics.LongHistogram;
import io.opentelemetry.api.metrics.Meter;
import org.springframework.stereotype.Service;


@Service
public class MeasureEvalMetrics {

  private final LongCounter patientReportableCounter;
  private final LongCounter patientNonReportableCounter;
  private final LongCounter measureEvaluatedCounter;
  private final LongCounter recordsReceivedCounter;
  private final LongHistogram evaluationDuration;

  public MeasureEvalMetrics(OpenTelemetry openTelemetry)
  {

    Meter meter = openTelemetry.getMeter("com.lantanagroup.link.measureeval.services.ResourceNormalizedConsumer");

    patientReportableCounter = meter
            .counterBuilder("Patient_Reportable_Counter")
            .build();
    patientNonReportableCounter = meter
            .counterBuilder("Patient_Non_Reportable_Counter")
            .build();
    measureEvaluatedCounter = meter
            .counterBuilder("Patient_Evaluated_Resource_Counter")
            .build();

    recordsReceivedCounter = meter.
                    counterBuilder("Records_Consumed")
                    .build();

    evaluationDuration = meter.histogramBuilder("MeasureEval.evaluation.duration")
          .ofLongs()
          .setDescription("The duration of the evaluation of a measure").setUnit("ms").build();
  }

  public void IncrementPatientReportableCounter(Attributes attributes)
  {
    patientReportableCounter.add(1, attributes);
  }

  public void IncrementPatientNonReportableCounter(Attributes attributes)
  {
    patientNonReportableCounter.add(1, attributes);
  }

  public void IncrementMeasureEvaluatedCounter(Attributes attributes)
  {
    measureEvaluatedCounter.add(1, attributes);
  }

  public void IncrementRecordsReceivedCounter(Attributes attributes)
  {
    recordsReceivedCounter.add(1, attributes);
  }

  void MeasureEvalDuration(long elapsedTime, Attributes attributes) {
    evaluationDuration.record(elapsedTime, attributes);
  }

}
