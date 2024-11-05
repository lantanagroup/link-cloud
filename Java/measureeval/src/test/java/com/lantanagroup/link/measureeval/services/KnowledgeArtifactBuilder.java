package com.lantanagroup.link.measureeval.services;

import org.hl7.fhir.r4.model.*;

import java.nio.charset.StandardCharsets;
import java.util.Arrays;

public class KnowledgeArtifactBuilder {

    private final static String BASE_MEASURE_URL = "https://example.com/Measure/";
    private final static String BASE_LIBRARY_URL = "https://example.com/Library/";

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
        private static final String MEASURE_URL = BASE_MEASURE_URL + MEASURE_ID;
        private static final String LIBRARY_URL = BASE_LIBRARY_URL + LIBRARY_ID;

        public static Measure measure() {
            return MeasureBuilder.build(MEASURE_ID, MEASURE_URL, LIBRARY_URL, "cohort", MeasurePopulationGroup.initialPopulation());
        }

        public static Library library() {
            return LibraryBuilder.build(LIBRARY_ID, "1.0.0", LIBRARY_ID, LIBRARY_URL, CqlLibraries.SIMPLE_COHORT_IP_TRUE);
        }

        public static Bundle bundle() {
            return BundleBuilder.build(library(), measure());
        }
    }

    static class SimpleCohortMeasureFalse {
        private static final String MEASURE_ID = "CohortMeasureFalse";
        private static final String LIBRARY_ID = "CohortLibraryFalse";
        private static final String MEASURE_URL = BASE_MEASURE_URL + MEASURE_ID;
        private static final String LIBRARY_URL = BASE_LIBRARY_URL + LIBRARY_ID;
        public static Measure measure() {
            return MeasureBuilder.build(MEASURE_ID, MEASURE_URL, LIBRARY_URL, "cohort", MeasurePopulationGroup.initialPopulation());
        }

        public static Library library() {
            return LibraryBuilder.build(LIBRARY_ID, "1.0.0", LIBRARY_ID, LIBRARY_URL, CqlLibraries.SIMPLE_COHORT_IP_FALSE);
        }

        public static Bundle bundle() {
            return BundleBuilder.build(library(), measure());
        }
    }

    static class CohortMeasureWithValueSet {
        private static final String MEASURE_ID = "CohortMeasureWithValueSet";
        private static final String LIBRARY_ID = "CohortLibraryWithValueSet";
        private static final String MEASURE_URL = BASE_MEASURE_URL + MEASURE_ID;
        private static final String LIBRARY_URL = BASE_LIBRARY_URL + LIBRARY_ID;
        public static Measure measure() {
            return MeasureBuilder.build(MEASURE_ID, MEASURE_URL, LIBRARY_URL, "cohort", MeasurePopulationGroup.initialPopulation());
        }

        public static Library library() {
            return LibraryBuilder.build(LIBRARY_ID, "1.0.0", LIBRARY_ID, LIBRARY_URL, CqlLibraries.COHORT_IP_TRUE_WITH_VALUESET);
        }

        public static Bundle bundle() {
            return BundleBuilder.build(library(), measure(), ValueSetBuilder.inpatientEncounter());
        }
    }

    static class CohortMeasureWithSDE {
        private static final String MEASURE_ID = "CohortMeasureWithSDE";
        private static final String LIBRARY_ID = "CohortLibraryWithSDE";
        private static final String MEASURE_URL = BASE_MEASURE_URL + MEASURE_ID;
        private static final String LIBRARY_URL = BASE_LIBRARY_URL + LIBRARY_ID;
        public static Measure measure() {
            return MeasureBuilder.buildSingleSde(MEASURE_ID, MEASURE_URL, LIBRARY_URL, "outcome", "cohort", "sde-condition", "SDE Condition", "SDE Condition", MeasurePopulationGroup.initialPopulation());
        }

        public static Library library() {
            return LibraryBuilder.build(LIBRARY_ID, "1.0.0", LIBRARY_ID, LIBRARY_URL, CqlLibraries.COHORT_IP_TRUE_WITH_SDE);
        }

        public static Bundle bundle() {
            return BundleBuilder.build(library(), measure(), ValueSetBuilder.inpatientEncounter());
        }
    }

    static class SimpleProportionMeasureAllTrueNoExclusion {
        private static final String MEASURE_ID = "ProportionMeasureAllTrueNoExclusion";
        private static final String LIBRARY_ID = "ProportionLibraryAllTrueNoExclusion";
        private static final String MEASURE_URL = BASE_MEASURE_URL + MEASURE_ID;
        private static final String LIBRARY_URL = BASE_LIBRARY_URL + LIBRARY_ID;
        public static Measure measure() {
            return MeasureBuilder.build(MEASURE_ID, MEASURE_URL, LIBRARY_URL, "proportion", MeasurePopulationGroup.initialPopulation(), MeasurePopulationGroup.numerator(), MeasurePopulationGroup.numeratorExclusion(), MeasurePopulationGroup.denominator(), MeasurePopulationGroup.denominatorExclusion());
        }

        public static Library library() {
            return LibraryBuilder.build(LIBRARY_ID, "1.0.0", LIBRARY_ID, LIBRARY_URL, CqlLibraries.SIMPLE_PROPORTION_ALL_TRUE_NO_EXCLUSION);
        }

        public static Bundle bundle() {
            return BundleBuilder.build(library(), measure());
        }
    }

    static class SimpleProportionMeasureAllFalse {
        private static final String MEASURE_ID = "ProportionMeasureAllFalse";
        private static final String LIBRARY_ID = "ProportionLibraryAllFalse";
        private static final String MEASURE_URL = BASE_MEASURE_URL + MEASURE_ID;
        private static final String LIBRARY_URL = BASE_LIBRARY_URL + LIBRARY_ID;
        public static Measure measure() {
            return MeasureBuilder.build(MEASURE_ID, MEASURE_URL, LIBRARY_URL, "proportion", MeasurePopulationGroup.initialPopulation(), MeasurePopulationGroup.numerator(), MeasurePopulationGroup.numeratorExclusion(), MeasurePopulationGroup.denominator(), MeasurePopulationGroup.denominatorExclusion());
        }

        public static Library library() {
            return LibraryBuilder.build(LIBRARY_ID, "1.0.0", LIBRARY_ID, LIBRARY_URL, CqlLibraries.SIMPLE_PROPORTION_ALL_FALSE);
        }

        public static Bundle bundle() {
            return BundleBuilder.build(library(), measure());
        }
    }

    static class SimpleRatioMeasure {
        private static final String MEASURE_ID = "RatioMeasure";
        private static final String LIBRARY_ID = "RatioLibrary";
        private static final String MEASURE_URL = BASE_MEASURE_URL + MEASURE_ID;
        private static final String LIBRARY_URL = BASE_LIBRARY_URL + LIBRARY_ID;

        public static Measure measure() {
            return MeasureBuilder.build(MEASURE_ID, MEASURE_URL, LIBRARY_URL, "ratio", MeasurePopulationGroup.initialPopulation(), MeasurePopulationGroup.numerator(), MeasurePopulationGroup.numeratorExclusion(), MeasurePopulationGroup.denominator(), MeasurePopulationGroup.denominatorExclusion());
        }

        public static Library library() {
            return LibraryBuilder.build(LIBRARY_ID, "1.0.0", LIBRARY_ID, LIBRARY_URL, CqlLibraries.SIMPLE_RATIO);
        }

        public static Bundle bundle() {
            return BundleBuilder.build(library(), measure());
        }
    }

    static class SimpleContinuousVariableMeasure {
        private static final String MEASURE_ID = "ContinuousVariableMeasure";
        private static final String LIBRARY_ID = "ContinuousVariableLibrary";
        private static final String MEASURE_URL = BASE_MEASURE_URL + MEASURE_ID;
        private static final String LIBRARY_URL = BASE_LIBRARY_URL + LIBRARY_ID;

        public static Measure measure() {
            return MeasureBuilder.build(MEASURE_ID, MEASURE_URL, LIBRARY_URL, "continuous-variable", MeasurePopulationGroup.initialPopulation(), MeasurePopulationGroup.measurePopulation(), MeasurePopulationGroup.measurePopulationExclusion());
        }

        public static Library library() {
            return LibraryBuilder.build(LIBRARY_ID, "1.0.0", LIBRARY_ID, LIBRARY_URL, CqlLibraries.SIMPLE_CONTINUOUS_VARIABLE);
        }

        public static Bundle bundle() {
            return BundleBuilder.build(library(), measure());
        }
    }

    static class MeasureBuilder {
        public static Measure build(String id, String url, String libraryUrl, String scoring, Measure.MeasureGroupPopulationComponent ... populations) {
            var measure = new Measure();
            measure.setUrl(url);
            measure.addLibrary(libraryUrl);
            measure.setScoring(new CodeableConcept().addCoding(new Coding().setCode(scoring)));
            measure.addGroup().setPopulation(Arrays.stream(populations).toList());
            measure.setId(id);
            return measure;
        }

        public static Measure buildSingleSde(String id, String url, String libraryUrl, String type, String scoring, String sdeId, String sdeDescription, String sdeExpression, Measure.MeasureGroupPopulationComponent ... populations) {
            var measure = new Measure();
            measure.setUrl(url);
            measure.addLibrary(libraryUrl);
            measure.setMeta(new Meta().addProfile(
                    "http://hl7.org/fhir/us/cqfmeasures/StructureDefinition/cohort-measure-cqfm").addProfile(
                    "http://hl7.org/fhir/us/cqfmeasures/StructureDefinition/computable-measure-cqfm"));
            measure.addExtension().setValue(new StringType("Encounter"))
                    .setUrl("http://hl7.org/fhir/us/cqfmeasures/StructureDefinition/cqfm-populationBasis");
            measure.addType().addCoding().setSystem("http://terminology.hl7.org/CodeSystem/measure-type").setCode(type);
            measure.setScoring(new CodeableConcept().addCoding(new Coding().setCode(scoring)));
            measure.addGroup().setPopulation(Arrays.stream(populations).toList());
            var sde = new Measure.MeasureSupplementalDataComponent();
            sde.setId(sdeId);
            sde.setDescription(sdeDescription);
            sde.setCriteria(new Expression().setLanguage("text/cql-identifier").setExpression(sdeExpression));
            sde.addUsage().addCoding().setCode("supplemental-data").setSystem("http://terminology.hl7.org/CodeSystem/measure-data-usage");
            measure.addSupplementalData(sde);
            measure.setId(id);
            return measure;
        }
    }

    static class LibraryBuilder {
        public static Library build(String id, String version, String name, String url, String cql) {
            var library = new Library().setVersion(version).setName(name).setUrl(url);
            library.addContent().setContentType("text/cql").setData(cql.getBytes(StandardCharsets.UTF_8));
            library.setId(id);
            return library;
        }
    }

    static class BundleBuilder {
        public static Bundle build(Resource ... resources) {
            var bundle = new Bundle();
            Arrays.stream(resources).forEach(resource -> bundle.addEntry().setResource(resource));
            return bundle;
        }
    }
}
