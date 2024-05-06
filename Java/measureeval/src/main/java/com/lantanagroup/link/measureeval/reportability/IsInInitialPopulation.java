package com.lantanagroup.link.measureeval.reportability;

import org.hl7.fhir.r4.model.MeasureReport;
import org.hl7.fhir.r4.model.codesystems.MeasurePopulation;

import java.util.function.Predicate;

public class IsInInitialPopulation implements Predicate<MeasureReport> {
    @Override
    public boolean test(MeasureReport measureReport) {
        return measureReport.getGroup().stream()
                .flatMap(group -> group.getPopulation().stream())
                .filter(population -> population.getCode().hasCoding(
                        MeasurePopulation.INITIALPOPULATION.getSystem(),
                        MeasurePopulation.INITIALPOPULATION.toCode()))
                .anyMatch(population -> population.getCount() > 0);
    }
}
