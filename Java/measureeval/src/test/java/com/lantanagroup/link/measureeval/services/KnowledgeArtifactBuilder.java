package com.lantanagroup.link.measureeval.services;

import org.hl7.fhir.r4.model.*;

import java.nio.charset.StandardCharsets;

public class KnowledgeArtifactBuilder {

    static class MeasurePopulationGroup {
        public static Measure.MeasureGroupPopulationComponent initialPopulation() {
            var initialPopulationGroup = new Measure.MeasureGroupPopulationComponent();
            initialPopulationGroup
                    .setCode(new CodeableConcept().addCoding(new Coding().setCode("initial-population")))
                    .setCriteria(new Expression().setLanguage("text/cql").setExpression("Initial Population"));
            initialPopulationGroup.setId("InitialPopulation");
            return initialPopulationGroup;
        }

        public static Measure.MeasureGroupPopulationComponent numerator() {
            var numerator = new Measure.MeasureGroupPopulationComponent();
            numerator.setCode(new CodeableConcept().addCoding(new Coding().setCode("numerator")))
                    .setCriteria(new Expression().setLanguage("text/cql").setExpression("Numerator"));
            numerator.setId("Numerator");
            return numerator;
        }

        public static Measure.MeasureGroupPopulationComponent numeratorExclusion() {
            var numerator = new Measure.MeasureGroupPopulationComponent();
            numerator.setCode(new CodeableConcept().addCoding(new Coding().setCode("numerator-exclusion")))
                    .setCriteria(new Expression().setLanguage("text/cql").setExpression("Numerator Exclusion"));
            numerator.setId("NumeratorExclusion");
            return numerator;
        }

        public static Measure.MeasureGroupPopulationComponent denominator() {
            var denominator = new Measure.MeasureGroupPopulationComponent();
            denominator.setCode(new CodeableConcept().addCoding(new Coding().setCode("denominator")))
                    .setCriteria(new Expression().setLanguage("text/cql").setExpression("Denominator"));
            denominator.setId("Denominator");
            return denominator;
        }

        public static Measure.MeasureGroupPopulationComponent denominatorExclusion() {
            var denominator = new Measure.MeasureGroupPopulationComponent();
            denominator.setCode(new CodeableConcept().addCoding(new Coding().setCode("denominator-exclusion")))
                    .setCriteria(new Expression().setLanguage("text/cql").setExpression("Denominator Exclusion"));
            denominator.setId("DenominatorExclusion");
            return denominator;
        }

        public static Measure.MeasureGroupPopulationComponent numeratorObservation() {
            var numeratorObservation = new Measure.MeasureGroupPopulationComponent();
            numeratorObservation.addExtension().setUrl("http://hl7.org/fhir/us/cqfmeasures/StructureDefinition/cqfm-criteriaReference").setValue(new StringType("numerator"));
            numeratorObservation.addExtension().setUrl("http://hl7.org/fhir/us/cqfmeasures/StructureDefinition/cqfm-aggregateMethod").setValue(new StringType("sum"));
            numeratorObservation.setCode(new CodeableConcept().addCoding(new Coding().setCode("measure-observation")))
                    .setCriteria(new Expression().setLanguage("text/cql-identifier").setExpression("Numerator Observation"));
            numeratorObservation.setId("numerator-observation");
            return numeratorObservation;
        }

        public static Measure.MeasureGroupPopulationComponent denominatorObservation() {
            var denominatorObservation = new Measure.MeasureGroupPopulationComponent();
            denominatorObservation.addExtension().setUrl("http://hl7.org/fhir/us/cqfmeasures/StructureDefinition/cqfm-criteriaReference").setValue(new StringType("denominator"));
            denominatorObservation.addExtension().setUrl("http://hl7.org/fhir/us/cqfmeasures/StructureDefinition/cqfm-aggregateMethod").setValue(new StringType("sum"));
            denominatorObservation.setCode(new CodeableConcept().addCoding(new Coding().setCode("measure-observation")))
                    .setCriteria(new Expression().setLanguage("text/cql-identifier").setExpression("Denominator Observation"));
            denominatorObservation.setId("denominator-observation");
            return denominatorObservation;
        }

        public static Measure.MeasureGroupPopulationComponent measurePopulation() {
            var measurePopulation = new Measure.MeasureGroupPopulationComponent();
            measurePopulation.setCode(new CodeableConcept().addCoding(new Coding().setCode("measure-population")))
                    .setCriteria(new Expression().setLanguage("text/cql-identifier").setExpression("Measure Population"));
            measurePopulation.setId("measure-population");
            return measurePopulation;
        }

        public static Measure.MeasureGroupPopulationComponent measurePopulationExclusion() {
            var measurePopulationExclusion = new Measure.MeasureGroupPopulationComponent();
            measurePopulationExclusion.setCode(new CodeableConcept().addCoding(new Coding().setCode("measure-population-exclusion")))
                    .setCriteria(new Expression().setLanguage("text/cql-identifier").setExpression("Measure Population Exclusion"));
            measurePopulationExclusion.setId("measure-population-exclusion");
            return measurePopulationExclusion;
        }
    }

    static class SimpleCohortMeasureTrue {
        private static final String MEASURE_ID = "CohortMeasureTrue";
        private static final String LIBRARY_ID = "CohortLibraryTrue";
        private static final String LIBRARY_URL = "https://example.com/Library/" + LIBRARY_ID;
        public static Measure measure() {
            Measure measure = new Measure();
            measure.addLibrary(LIBRARY_URL);
            measure.setScoring(new CodeableConcept().addCoding(new Coding().setCode("cohort")));
            measure.addGroup().addPopulation(MeasurePopulationGroup.initialPopulation());
            measure.setId(MEASURE_ID);
            return measure;
        }

        public static Library library() {
            Library library = new Library().setVersion("1.0.0").setName(LIBRARY_ID).setUrl(LIBRARY_URL);
            library.addContent().setContentType("text/cql").setData(CqlLibraries.SIMPLE_COHORT_IP_TRUE.getBytes(StandardCharsets.UTF_8));
            library.setId(LIBRARY_ID);
            return library;
        }

        public static Bundle bundle() {
            Bundle bundle = new Bundle();
            bundle.addEntry().setResource(library());
            bundle.addEntry().setResource(measure());
            return bundle;
        }
    }

    static class SimpleCohortMeasureFalse {
        private static final String MEASURE_ID = "CohortMeasureFalse";
        private static final String LIBRARY_ID = "CohortLibraryFalse";
        private static final String LIBRARY_URL = "https://example.com/Library/" + LIBRARY_ID;
        public static Measure measure() {
            Measure measure = new Measure();
            measure.addLibrary(LIBRARY_URL);
            measure.setScoring(new CodeableConcept().addCoding(new Coding().setCode("cohort")));
            measure.addGroup().addPopulation(MeasurePopulationGroup.initialPopulation());
            measure.setId(MEASURE_ID);
            return measure;
        }

        public static Library library() {
            Library library = new Library().setVersion("1.0.0").setName(LIBRARY_ID).setUrl(LIBRARY_URL);
            library.addContent().setContentType("text/cql").setData(CqlLibraries.SIMPLE_COHORT_IP_FALSE.getBytes(StandardCharsets.UTF_8));
            library.setId(LIBRARY_ID);
            return library;
        }

        public static Bundle bundle() {
            Bundle bundle = new Bundle();
            bundle.addEntry().setResource(library());
            bundle.addEntry().setResource(measure());
            return bundle;
        }
    }

    static class CohortMeasureWithValueSet {
        private static final String MEASURE_ID = "CohortMeasureWithValueSet";
        private static final String LIBRARY_ID = "CohortLibraryWithValueSet";
        private static final String LIBRARY_URL = "https://example.com/Library/" + LIBRARY_ID;
        public static Measure measure() {
            Measure measure = new Measure();
            measure.addLibrary(LIBRARY_URL);
            measure.setScoring(new CodeableConcept().addCoding(new Coding().setCode("cohort")));
            measure.addGroup().addPopulation(MeasurePopulationGroup.initialPopulation());
            measure.setId(MEASURE_ID);
            return measure;
        }

        public static Library library() {
            Library library = new Library().setVersion("1.0.0").setName(LIBRARY_ID).setUrl(LIBRARY_URL);
            library.addContent().setContentType("text/cql").setData(CqlLibraries.COHORT_IP_TRUE_WITH_VALUESET.getBytes(StandardCharsets.UTF_8));
            library.setId(LIBRARY_ID);
            return library;
        }

        public static Bundle bundle() {
            Bundle bundle = new Bundle();
            bundle.addEntry().setResource(library());
            bundle.addEntry().setResource(measure());
            bundle.addEntry().setResource(ValueSetBuilder.inpatientEncounter());
            return bundle;
        }
    }

    static class CohortMeasureWithSDE {
        private static final String MEASURE_ID = "CohortMeasureWithSDE";
        private static final String LIBRARY_ID = "CohortLibraryWithSDE";
        private static final String LIBRARY_URL = "https://example.com/Library/" + LIBRARY_ID;
        public static Measure measure() {
            Measure measure = new Measure();
            measure.setId(MEASURE_ID);
            measure.setMeta(new Meta().addProfile(
                    "http://hl7.org/fhir/us/cqfmeasures/StructureDefinition/cohort-measure-cqfm").addProfile(
                            "http://hl7.org/fhir/us/cqfmeasures/StructureDefinition/computable-measure-cqfm"));
            measure.addExtension().setValue(new StringType("Encounter"))
                    .setUrl("http://hl7.org/fhir/us/cqfmeasures/StructureDefinition/cqfm-populationBasis");
            measure.addLibrary(LIBRARY_URL);
            measure.addType().addCoding().setSystem("http://terminology.hl7.org/CodeSystem/measure-type").setCode("outcome");
            measure.setScoring(new CodeableConcept().addCoding(new Coding().setCode("cohort")));
            measure.addGroup().addPopulation(MeasurePopulationGroup.initialPopulation());
            var sde = new Measure.MeasureSupplementalDataComponent();
            sde.setId("sde-condition");
            sde.setDescription("SDE Condition");
            sde.setCriteria(new Expression().setLanguage("text/cql-identifier").setExpression("SDE Condition"));
            sde.addUsage().addCoding().setCode("supplemental-data").setSystem("http://terminology.hl7.org/CodeSystem/measure-data-usage");
            measure.addSupplementalData(sde);
            return measure;
        }

        public static Library library() {
            Library library = new Library().setVersion("1.0.0").setName(LIBRARY_ID).setUrl(LIBRARY_URL);
            library.addContent().setContentType("text/cql").setData(CqlLibraries.COHORT_IP_TRUE_WITH_SDE.getBytes(StandardCharsets.UTF_8));
            library.setId(LIBRARY_ID);
            return library;
        }

        public static Bundle bundle() {
            Bundle bundle = new Bundle();
            bundle.addEntry().setResource(library());
            bundle.addEntry().setResource(measure());
            bundle.addEntry().setResource(ValueSetBuilder.inpatientEncounter());
            return bundle;
        }
    }

    static class SimpleProportionMeasureAllTrueNoExclusion {
        private static final String MEASURE_ID = "ProportionMeasureAllTrueNoExclusion";
        private static final String LIBRARY_ID = "ProportionLibraryAllTrueNoExclusion";
        private static final String LIBRARY_URL = "https://example.com/Library/" + LIBRARY_ID;
        public static Measure measure() {
            Measure measure = new Measure();
            measure.addLibrary(LIBRARY_URL);
            measure.setScoring(new CodeableConcept().addCoding(new Coding().setCode("proportion")));
            measure.addGroup()
                    .addPopulation(MeasurePopulationGroup.initialPopulation())
                    .addPopulation(MeasurePopulationGroup.numerator())
                    .addPopulation(MeasurePopulationGroup.numeratorExclusion())
                    .addPopulation(MeasurePopulationGroup.denominator())
                    .addPopulation(MeasurePopulationGroup.denominatorExclusion());
            measure.setId(MEASURE_ID);
            return measure;
        }

        public static Library library() {
            Library library = new Library().setVersion("1.0.0").setName(LIBRARY_ID).setUrl(LIBRARY_URL);
            library.addContent().setContentType("text/cql").setData(CqlLibraries.SIMPLE_PROPORTION_ALL_TRUE_NO_EXCLUSION.getBytes(StandardCharsets.UTF_8));
            library.setId(LIBRARY_ID);
            return library;
        }

        public static Bundle bundle() {
            Bundle bundle = new Bundle();
            bundle.addEntry().setResource(library());
            bundle.addEntry().setResource(measure());
            return bundle;
        }
    }

    static class SimpleProportionMeasureAllFalse {
        private static final String MEASURE_ID = "ProportionMeasureAllFalse";
        private static final String LIBRARY_ID = "ProportionLibraryAllFalse";
        private static final String LIBRARY_URL = "https://example.com/Library/" + LIBRARY_ID;
        public static Measure measure() {
            Measure measure = new Measure();
            measure.addLibrary(LIBRARY_URL);
            measure.setScoring(new CodeableConcept().addCoding(new Coding().setCode("proportion")));
            measure.addGroup()
                    .addPopulation(MeasurePopulationGroup.initialPopulation())
                    .addPopulation(MeasurePopulationGroup.numerator())
                    .addPopulation(MeasurePopulationGroup.numeratorExclusion())
                    .addPopulation(MeasurePopulationGroup.denominator())
                    .addPopulation(MeasurePopulationGroup.denominatorExclusion());
            measure.setId(MEASURE_ID);
            return measure;
        }

        public static Library library() {
            Library library = new Library().setVersion("1.0.0").setName(LIBRARY_ID).setUrl(LIBRARY_URL);
            library.addContent().setContentType("text/cql").setData(CqlLibraries.SIMPLE_PROPORTION_ALL_FALSE.getBytes(StandardCharsets.UTF_8));
            library.setId(LIBRARY_ID);
            return library;
        }

        public static Bundle bundle() {
            Bundle bundle = new Bundle();
            bundle.addEntry().setResource(library());
            bundle.addEntry().setResource(measure());
            return bundle;
        }
    }

    static class SimpleRatioMeasure {
        private static final String MEASURE_ID = "RatioMeasure";
        private static final String LIBRARY_ID = "RatioLibrary";
        private static final String LIBRARY_URL = "https://example.com/Library/" + LIBRARY_ID;

        public static Measure measure() {
            Measure measure = new Measure();
            measure.addLibrary(LIBRARY_URL);
            measure.setScoring(new CodeableConcept().addCoding(new Coding().setCode("ratio")));
            measure.addGroup()
                    .addPopulation(MeasurePopulationGroup.initialPopulation())
                    .addPopulation(MeasurePopulationGroup.numerator())
                    .addPopulation(MeasurePopulationGroup.numeratorObservation())
                    .addPopulation(MeasurePopulationGroup.denominator())
                    .addPopulation(MeasurePopulationGroup.denominatorObservation());
            measure.setId(MEASURE_ID);
            return measure;
        }

        public static Library library() {
            Library library = new Library().setVersion("1.0.0").setName(LIBRARY_ID).setUrl(LIBRARY_URL);
            library.addContent().setContentType("text/cql").setData(CqlLibraries.SIMPLE_RATIO.getBytes(StandardCharsets.UTF_8));
            library.setId(LIBRARY_ID);
            return library;
        }

        public static Bundle bundle() {
            Bundle bundle = new Bundle();
            bundle.addEntry().setResource(library());
            bundle.addEntry().setResource(measure());
            return bundle;
        }
    }

    static class SimpleContinuousVariableMeasure {
        private static final String MEASURE_ID = "ContinuousVariableMeasure";
        private static final String LIBRARY_ID = "ContinuousVariableLibrary";
        private static final String LIBRARY_URL = "https://example.com/Library/" + LIBRARY_ID;

        public static Measure measure() {
            Measure measure = new Measure();
            measure.addLibrary(LIBRARY_URL);
            measure.setScoring(new CodeableConcept().addCoding(new Coding().setCode("continuous-variable")));
            measure.addGroup()
                    .addPopulation(MeasurePopulationGroup.initialPopulation())
                    .addPopulation(MeasurePopulationGroup.measurePopulation())
                    .addPopulation(MeasurePopulationGroup.measurePopulationExclusion());
            measure.setId(MEASURE_ID);
            return measure;
        }

        public static Library library() {
            Library library = new Library().setVersion("1.0.0").setName(LIBRARY_ID).setUrl(LIBRARY_URL);
            library.addContent().setContentType("text/cql").setData(CqlLibraries.SIMPLE_CONTINUOUS_VARIABLE.getBytes(StandardCharsets.UTF_8));
            library.setId(LIBRARY_ID);
            return library;
        }

        public static Bundle bundle() {
            Bundle bundle = new Bundle();
            bundle.addEntry().setResource(library());
            bundle.addEntry().setResource(measure());
            return bundle;
        }
    }
}
