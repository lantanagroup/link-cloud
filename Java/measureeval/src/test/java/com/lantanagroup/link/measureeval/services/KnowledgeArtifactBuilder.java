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

    static class CohortMeasure {
        private static final String MEASURE_ID = "CohortMeasure";
        private static final String LIBRARY_ID = "CohortLibrary";
        private static final String LIBRARY_URL = "https://example.com/Library/" + LIBRARY_ID;
        public static final String cql = """
                library CohortLibrary version '1.0.0'
                
                using FHIR version '4.0.1'
                
                context Patient

                define "Initial Population":
                  true""";
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
            library.addContent().setContentType("text/cql").setData(cql.getBytes(StandardCharsets.UTF_8));
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

    static class ProportionMeasure {
        private static final String MEASURE_ID = "ProportionMeasure";
        private static final String LIBRARY_ID = "ProportionLibrary";
        private static final String LIBRARY_URL = "https://example.com/Library/" + LIBRARY_ID;
        public static final String cql = """
                library ProportionLibrary version '1.0.0'
                
                using FHIR version '4.0.1'
                
                context Patient

                define "Initial Population":
                  true
                  
                define "Numerator":
                  true
                  
                define "Numerator Exclusion":
                  false
                
                define "Denominator":
                  true
                  
                define "Denominator Exclusion"
                  false""";
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
            library.addContent().setContentType("text/cql").setData(cql.getBytes(StandardCharsets.UTF_8));
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

    static class RatioMeasure {
        private static final String MEASURE_ID = "RatioMeasure";
        private static final String LIBRARY_ID = "RatioLibrary";
        private static final String LIBRARY_URL = "https://example.com/Library/" + LIBRARY_ID;
        public static final String cql = """
                library RatioLibrary version '1.0.0'
                
                using FHIR version '4.0.1'
                
                context Patient

                define "Initial Population":
                  ["Encounter"]
                  
                define "Numerator":
                  "Initial Population"
                
                define "Denominator":
                  "Initial Population"
                
                define function "Denominator Observation"(Encounter "Encounter"):
                  24
                  
                define function "Numerator Observation"(Encounter "Encounter"):
                  1""";

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
            library.addContent().setContentType("text/cql").setData(cql.getBytes(StandardCharsets.UTF_8));
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

    static class ContinuousVariableMeasure {
        private static final String MEASURE_ID = "ContinuousVariableMeasure";
        private static final String LIBRARY_ID = "ContinuousVariableLibrary";
        private static final String LIBRARY_URL = "https://example.com/Library/" + LIBRARY_ID;
        public static final String cql = """
                library ContinuousVariableLibrary version '1.0.0'
                
                using FHIR version '4.0.1'
                
                context Patient

                define "Initial Population":
                  ["Encounter"]
                  
                define "Measure Population":
                  "Initial Population"
                
                define "Measure Population Exclusion":
                  false
                
                define function "Measure Observation"(Encounter "Encounter"):
                  24""";

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
            library.addContent().setContentType("text/cql").setData(cql.getBytes(StandardCharsets.UTF_8));
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
