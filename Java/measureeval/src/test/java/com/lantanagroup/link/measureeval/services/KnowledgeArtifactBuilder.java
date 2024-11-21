package com.lantanagroup.link.measureeval.services;

import org.hl7.fhir.r4.model.*;

import java.nio.charset.StandardCharsets;
import java.util.Arrays;

/**
 * Utility class for building FHIR knowledge artifacts such as {@link Measure}, {@link Library},
 * and {@link Bundle} for various test cases. The class also provides predefined population
 * components for measures and builder utilities for creating FHIR resources.
 */
public class KnowledgeArtifactBuilder {

    private final static String BASE_MEASURE_URL = "https://example.com/Measure/";
    private final static String BASE_LIBRARY_URL = "https://example.com/Library/";

    /**
     * Nested utility class for creating {@link Measure.MeasureGroupPopulationComponent}
     * elements representing different population components for FHIR measures.
     */
    static class MeasurePopulationGroup {
        /**
         * Creates a population component for the "Initial Population".
         *
         * @return A {@link Measure.MeasureGroupPopulationComponent} for the initial population.
         */
        public static Measure.MeasureGroupPopulationComponent initialPopulation() {
            var initialPopulationGroup = new Measure.MeasureGroupPopulationComponent();
            initialPopulationGroup
                    .setCode(new CodeableConcept().addCoding(new Coding().setCode("initial-population")))
                    .setCriteria(new Expression().setLanguage("text/cql").setExpression("Initial Population"));
            initialPopulationGroup.setId("InitialPopulation");
            return initialPopulationGroup;
        }

        /**
         * Creates a population component for the "Numerator".
         *
         * @return A {@link Measure.MeasureGroupPopulationComponent} for the numerator.
         */
        public static Measure.MeasureGroupPopulationComponent numerator() {
            var numerator = new Measure.MeasureGroupPopulationComponent();
            numerator.setCode(new CodeableConcept().addCoding(new Coding().setCode("numerator")))
                    .setCriteria(new Expression().setLanguage("text/cql").setExpression("Numerator"));
            numerator.setId("Numerator");
            return numerator;
        }

        /**
         * Creates a population component for the "Numerator Exclusion".
         *
         * @return A {@link Measure.MeasureGroupPopulationComponent} for the numerator exclusion.
         */
        public static Measure.MeasureGroupPopulationComponent numeratorExclusion() {
            var numerator = new Measure.MeasureGroupPopulationComponent();
            numerator.setCode(new CodeableConcept().addCoding(new Coding().setCode("numerator-exclusion")))
                    .setCriteria(new Expression().setLanguage("text/cql").setExpression("Numerator Exclusion"));
            numerator.setId("NumeratorExclusion");
            return numerator;
        }

        /**
         * Creates a population component for the "Denominator".
         *
         * @return A {@link Measure.MeasureGroupPopulationComponent} for the denominator.
         */
        public static Measure.MeasureGroupPopulationComponent denominator() {
            var denominator = new Measure.MeasureGroupPopulationComponent();
            denominator.setCode(new CodeableConcept().addCoding(new Coding().setCode("denominator")))
                    .setCriteria(new Expression().setLanguage("text/cql").setExpression("Denominator"));
            denominator.setId("Denominator");
            return denominator;
        }

        /**
         * Creates a population component for the "Denominator Exclusion".
         *
         * @return A {@link Measure.MeasureGroupPopulationComponent} for the denominator exclusion.
         */
        public static Measure.MeasureGroupPopulationComponent denominatorExclusion() {
            var denominator = new Measure.MeasureGroupPopulationComponent();
            denominator.setCode(new CodeableConcept().addCoding(new Coding().setCode("denominator-exclusion")))
                    .setCriteria(new Expression().setLanguage("text/cql").setExpression("Denominator Exclusion"));
            denominator.setId("DenominatorExclusion");
            return denominator;
        }

        /**
         * Creates a {@link Measure.MeasureGroupPopulationComponent} representing a numerator observation
         * for a measure. This component is used to define the observation criteria for the numerator
         * and includes relevant extensions for criteria reference and aggregation method.
         *
         * @return A {@link Measure.MeasureGroupPopulationComponent} configured for numerator observation.
         */
        public static Measure.MeasureGroupPopulationComponent numeratorObservation() {
            var numeratorObservation = new Measure.MeasureGroupPopulationComponent();
            numeratorObservation.addExtension().setUrl("http://hl7.org/fhir/us/cqfmeasures/StructureDefinition/cqfm-criteriaReference").setValue(new StringType("numerator"));
            numeratorObservation.addExtension().setUrl("http://hl7.org/fhir/us/cqfmeasures/StructureDefinition/cqfm-aggregateMethod").setValue(new StringType("sum"));
            numeratorObservation.setCode(new CodeableConcept().addCoding(new Coding().setCode("measure-observation")))
                    .setCriteria(new Expression().setLanguage("text/cql-identifier").setExpression("Numerator Observation"));
            numeratorObservation.setId("numerator-observation");
            return numeratorObservation;
        }

        /**
         * Creates a {@link Measure.MeasureGroupPopulationComponent} representing a denominator observation
         * for a measure. This component is used to define the observation criteria for the denominator
         * and includes relevant extensions for criteria reference and aggregation method.
         *
         * @return A {@link Measure.MeasureGroupPopulationComponent} configured for denominator observation.
         */
        public static Measure.MeasureGroupPopulationComponent denominatorObservation() {
            var denominatorObservation = new Measure.MeasureGroupPopulationComponent();
            denominatorObservation.addExtension().setUrl("http://hl7.org/fhir/us/cqfmeasures/StructureDefinition/cqfm-criteriaReference").setValue(new StringType("denominator"));
            denominatorObservation.addExtension().setUrl("http://hl7.org/fhir/us/cqfmeasures/StructureDefinition/cqfm-aggregateMethod").setValue(new StringType("sum"));
            denominatorObservation.setCode(new CodeableConcept().addCoding(new Coding().setCode("measure-observation")))
                    .setCriteria(new Expression().setLanguage("text/cql-identifier").setExpression("Denominator Observation"));
            denominatorObservation.setId("denominator-observation");
            return denominatorObservation;
        }

        /**
         * Creates a population component for the "Measure Population".
         *
         * @return A {@link Measure.MeasureGroupPopulationComponent} for the measure population.
         */
        public static Measure.MeasureGroupPopulationComponent measurePopulation() {
            var measurePopulation = new Measure.MeasureGroupPopulationComponent();
            measurePopulation.setCode(new CodeableConcept().addCoding(new Coding().setCode("measure-population")))
                    .setCriteria(new Expression().setLanguage("text/cql-identifier").setExpression("Measure Population"));
            measurePopulation.setId("measure-population");
            return measurePopulation;
        }

        /**
         * Creates a population component for the "Measure Population Exclusion".
         *
         * @return A {@link Measure.MeasureGroupPopulationComponent} for the measure population exclusion.
         */
        public static Measure.MeasureGroupPopulationComponent measurePopulationExclusion() {
            var measurePopulationExclusion = new Measure.MeasureGroupPopulationComponent();
            measurePopulationExclusion.setCode(new CodeableConcept().addCoding(new Coding().setCode("measure-population-exclusion")))
                    .setCriteria(new Expression().setLanguage("text/cql-identifier").setExpression("Measure Population Exclusion"));
            measurePopulationExclusion.setId("measure-population-exclusion");
            return measurePopulationExclusion;
        }
    }

    /**
     * Nested utility class for building resources for a "Simple Cohort Measure" scenario where the evaluation result is expected to be true.
     */
    static class SimpleCohortMeasureTrue {
        private static final String MEASURE_ID = "CohortMeasureTrue";
        private static final String LIBRARY_ID = "CohortLibraryTrue";
        private static final String MEASURE_URL = BASE_MEASURE_URL + MEASURE_ID;
        private static final String LIBRARY_URL = BASE_LIBRARY_URL + LIBRARY_ID;

        /**
         * Builds the {@link Measure} resource for the "Simple Cohort Measure True" scenario.
         *
         * @return A {@link Measure} representing the simple cohort measure.
         */
        public static Measure measure() {
            return MeasureBuilder.build(MEASURE_ID, MEASURE_URL, LIBRARY_URL, "cohort", MeasurePopulationGroup.initialPopulation());
        }

        /**
         * Builds the {@link Library} resource for the "Simple Cohort Measure True" scenario.
         *
         * @return A {@link Library} associated with the measure.
         */
        public static Library library() {
            return LibraryBuilder.build(LIBRARY_ID, "1.0.0", LIBRARY_ID, LIBRARY_URL, CqlLibraries.SIMPLE_COHORT_IP_TRUE);
        }

        /**
         * Builds the {@link Bundle} resource containing the measure and library for the "Simple Cohort Measure True" scenario.
         *
         * @return A {@link Bundle} containing the measure and library.
         */
        public static Bundle bundle() {
            return BundleBuilder.build(library(), measure());
        }
    }

    /**
     * Nested utility class for building resources for a "Simple Cohort Measure" scenario where the evaluation result is expected to be false.
     */
    static class SimpleCohortMeasureFalse {
        private static final String MEASURE_ID = "CohortMeasureFalse";
        private static final String LIBRARY_ID = "CohortLibraryFalse";
        private static final String MEASURE_URL = BASE_MEASURE_URL + MEASURE_ID;
        private static final String LIBRARY_URL = BASE_LIBRARY_URL + LIBRARY_ID;

        /**
         * Builds the {@link Measure} resource for the "Simple Cohort Measure False" scenario.
         *
         * @return A {@link Measure} resource with the cohort scoring type and an initial population definition.
         */
        public static Measure measure() {
            return MeasureBuilder.build(MEASURE_ID, MEASURE_URL, LIBRARY_URL, "cohort", MeasurePopulationGroup.initialPopulation());
        }

        /**
         * Builds the {@link Library} resource for the "Simple Cohort Measure False" scenario.
         *
         * @return A {@link Library} resource containing CQL logic for evaluating the initial population to false.
         */
        public static Library library() {
            return LibraryBuilder.build(LIBRARY_ID, "1.0.0", LIBRARY_ID, LIBRARY_URL, CqlLibraries.SIMPLE_COHORT_IP_FALSE);
        }

        /**
         * Builds a {@link Bundle} resource containing the {@link Measure} and {@link Library} for the "Simple Cohort Measure False" scenario.
         *
         * @return A {@link Bundle} resource containing the measure and library.
         */
        public static Bundle bundle() {
            return BundleBuilder.build(library(), measure());
        }
    }

    /**
     * Nested utility class for building resources for a "Cohort Measure with Value Set" scenario where the evaluation result is expected to be true.
     * This class creates a {@link Measure}, {@link Library}, and {@link Bundle} that includes a reference to a value set for encounter types.
     */
    static class CohortMeasureWithValueSetTrue {
        private static final String MEASURE_ID = "CohortMeasureWithValueSetTrue";
        private static final String LIBRARY_ID = "CohortLibraryWithValueSetTrue";
        private static final String MEASURE_URL = BASE_MEASURE_URL + MEASURE_ID;
        private static final String LIBRARY_URL = BASE_LIBRARY_URL + LIBRARY_ID;

        /**
         * Builds the {@link Measure} resource for the "Cohort Measure with Value Set True" scenario.
         *
         * @return A {@link Measure} resource with the cohort scoring type and an initial population definition.
         */
        public static Measure measure() {
            return MeasureBuilder.build(MEASURE_ID, MEASURE_URL, LIBRARY_URL, "cohort", MeasurePopulationGroup.initialPopulation());
        }

        /**
         * Builds the {@link Library} resource for the "Cohort Measure with Value Set True" scenario.
         *
         * @return A {@link Library} resource containing CQL logic for evaluating the initial population using a value set, expected to evaluate to true.
         */
        public static Library library() {
            return LibraryBuilder.build(LIBRARY_ID, "1.0.0", LIBRARY_ID, LIBRARY_URL, CqlLibraries.COHORT_IP_TRUE_WITH_VALUESET);
        }

        /**
         * Builds a {@link Bundle} resource containing the {@link Measure}, {@link Library}, and value set for the "Cohort Measure with Value Set True" scenario.
         *
         * @return A {@link Bundle} resource containing the measure, library, and a value set for inpatient encounters.
         */
        public static Bundle bundle() {
            return BundleBuilder.build(library(), measure(), ValueSetBuilder.inpatientEncounter());
        }
    }

    /**
     * Nested utility class for building resources for a "Cohort Measure with Value Set" scenario where the evaluation result is expected to be false.
     * This class creates a {@link Measure}, {@link Library}, and {@link Bundle} that includes a reference to a value set for encounter types.
     */
    static class CohortMeasureWithValueSetFalse {
        private static final String MEASURE_ID = "CohortMeasureWithValueSetFalse";
        private static final String LIBRARY_ID = "CohortLibraryWithValueSetFalse";
        private static final String MEASURE_URL = BASE_MEASURE_URL + MEASURE_ID;
        private static final String LIBRARY_URL = BASE_LIBRARY_URL + LIBRARY_ID;

        /**
         * Builds the {@link Measure} resource for the "Cohort Measure with Value Set False" scenario.
         *
         * @return A {@link Measure} resource with the cohort scoring type and an initial population definition.
         */
        public static Measure measure() {
            return MeasureBuilder.build(MEASURE_ID, MEASURE_URL, LIBRARY_URL, "cohort", MeasurePopulationGroup.initialPopulation());
        }

        /**
         * Builds the {@link Library} resource for the "Cohort Measure with Value Set False" scenario.
         *
         * @return A {@link Library} resource containing CQL logic for evaluating the initial population using a value set, expected to evaluate to false.
         */
        public static Library library() {
            return LibraryBuilder.build(LIBRARY_ID, "1.0.0", LIBRARY_ID, LIBRARY_URL, CqlLibraries.COHORT_IP_FALSE_WITH_VALUESET);
        }

        /**
         * Builds a {@link Bundle} resource containing the {@link Measure}, {@link Library}, and value set for the "Cohort Measure with Value Set False" scenario.
         *
         * @return A {@link Bundle} resource containing the measure, library, and a value set for inpatient encounters.
         */
        public static Bundle bundle() {
            return BundleBuilder.build(library(), measure(), ValueSetBuilder.inpatientEncounter());
        }
    }

    /**
     * Nested utility class for building resources for a "Cohort Measure with Supplemental Data Element (SDE)" scenario.
     * This class creates a {@link Measure}, {@link Library}, and {@link Bundle} that includes a supplemental data element (SDE)
     * and a value set for inpatient encounters.
     */
    static class CohortMeasureWithSDE {
        private static final String MEASURE_ID = "CohortMeasureWithSDE";
        private static final String LIBRARY_ID = "CohortLibraryWithSDE";
        private static final String MEASURE_URL = BASE_MEASURE_URL + MEASURE_ID;
        private static final String LIBRARY_URL = BASE_LIBRARY_URL + LIBRARY_ID;

        /**
         * Builds the {@link Measure} resource for the "Cohort Measure with SDE" scenario.
         * The measure is configured with the cohort scoring type, an initial population, and a supplemental data element (SDE).
         *
         * @return A {@link Measure} resource containing an initial population definition and an SDE.
         */
        public static Measure measure() {
            return MeasureBuilder.buildSingleSde(MEASURE_ID, MEASURE_URL, LIBRARY_URL, "outcome", "cohort", "sde-condition", "SDE Condition", "SDE Condition", MeasurePopulationGroup.initialPopulation());
        }

        /**
         * Builds the {@link Library} resource for the "Cohort Measure with SDE" scenario.
         * The library contains CQL logic for evaluating the initial population and supplemental data.
         *
         * @return A {@link Library} resource containing CQL logic for the cohort measure with SDE.
         */
        public static Library library() {
            return LibraryBuilder.build(LIBRARY_ID, "1.0.0", LIBRARY_ID, LIBRARY_URL, CqlLibraries.COHORT_IP_TRUE_WITH_SDE);
        }

        /**
         * Builds a {@link Bundle} resource containing the {@link Measure}, {@link Library}, and value set for the "Cohort Measure with SDE" scenario.
         *
         * @return A {@link Bundle} resource containing the measure, library, and a value set for inpatient encounters.
         */
        public static Bundle bundle() {
            return BundleBuilder.build(library(), measure(), ValueSetBuilder.inpatientEncounter());
        }
    }

    /**
     * Nested utility class for building resources for a "Simple Proportion Measure" scenario where all conditions are true and there are no exclusions.
     * This class creates a {@link Measure}, {@link Library}, and {@link Bundle} to represent a proportion measure with an initial population, numerator, and denominator.
     */
    static class SimpleProportionMeasureAllTrueNoExclusion {
        private static final String MEASURE_ID = "ProportionMeasureAllTrueNoExclusion";
        private static final String LIBRARY_ID = "ProportionLibraryAllTrueNoExclusion";
        private static final String MEASURE_URL = BASE_MEASURE_URL + MEASURE_ID;
        private static final String LIBRARY_URL = BASE_LIBRARY_URL + LIBRARY_ID;

        /**
         * Builds the {@link Measure} resource for the "Simple Proportion Measure All True No Exclusion" scenario.
         * The measure is configured with the proportion scoring type, an initial population, numerator, and denominator, without exclusions.
         *
         * @return A {@link Measure} resource with the proportion scoring type and specified population groups.
         */
        public static Measure measure() {
            return MeasureBuilder.build(MEASURE_ID, MEASURE_URL, LIBRARY_URL, "proportion", MeasurePopulationGroup.initialPopulation(), MeasurePopulationGroup.numerator(), MeasurePopulationGroup.numeratorExclusion(), MeasurePopulationGroup.denominator(), MeasurePopulationGroup.denominatorExclusion());
        }

        /**
         * Builds the {@link Library} resource for the "Simple Proportion Measure All True No Exclusion" scenario.
         * The library contains CQL logic for evaluating the measure.
         *
         * @return A {@link Library} resource containing CQL logic for the proportion measure.
         */
        public static Library library() {
            return LibraryBuilder.build(LIBRARY_ID, "1.0.0", LIBRARY_ID, LIBRARY_URL, CqlLibraries.SIMPLE_PROPORTION_ALL_TRUE_NO_EXCLUSION);
        }

        /**
         * Builds a {@link Bundle} resource containing the {@link Measure} and {@link Library} for the "Simple Proportion Measure All True No Exclusion" scenario.
         *
         * @return A {@link Bundle} resource containing the measure and library.
         */
        public static Bundle bundle() {
            return BundleBuilder.build(library(), measure());
        }
    }

    /**
     * Nested utility class for building resources for a "Simple Proportion Measure" scenario where all conditions are false.
     * This class creates a {@link Measure}, {@link Library}, and {@link Bundle} to represent a proportion measure with an initial population, numerator, and denominator.
     */
    static class SimpleProportionMeasureAllFalse {
        private static final String MEASURE_ID = "ProportionMeasureAllFalse";
        private static final String LIBRARY_ID = "ProportionLibraryAllFalse";
        private static final String MEASURE_URL = BASE_MEASURE_URL + MEASURE_ID;
        private static final String LIBRARY_URL = BASE_LIBRARY_URL + LIBRARY_ID;

        /**
         * Builds the {@link Measure} resource for the "Simple Proportion Measure All False" scenario.
         * The measure is configured with the proportion scoring type, an initial population, numerator, and denominator, with all conditions set to false.
         *
         * @return A {@link Measure} resource with the proportion scoring type and specified population groups.
         */
        public static Measure measure() {
            return MeasureBuilder.build(MEASURE_ID, MEASURE_URL, LIBRARY_URL, "proportion", MeasurePopulationGroup.initialPopulation(), MeasurePopulationGroup.numerator(), MeasurePopulationGroup.numeratorExclusion(), MeasurePopulationGroup.denominator(), MeasurePopulationGroup.denominatorExclusion());
        }

        /**
         * Builds the {@link Library} resource for the "Simple Proportion Measure All False" scenario.
         * The library contains CQL logic for evaluating the measure, where all conditions evaluate to false.
         *
         * @return A {@link Library} resource containing CQL logic for the proportion measure.
         */
        public static Library library() {
            return LibraryBuilder.build(LIBRARY_ID, "1.0.0", LIBRARY_ID, LIBRARY_URL, CqlLibraries.SIMPLE_PROPORTION_ALL_FALSE);
        }

        /**
         * Builds a {@link Bundle} resource containing the {@link Measure} and {@link Library} for the "Simple Proportion Measure All False" scenario.
         *
         * @return A {@link Bundle} resource containing the measure and library.
         */
        public static Bundle bundle() {
            return BundleBuilder.build(library(), measure());
        }
    }

    /**
     * Nested utility class for building resources for a "Simple Ratio Measure" scenario.
     * This class creates a {@link Measure}, {@link Library}, and {@link Bundle} to represent a ratio measure
     * with an initial population, numerator, and denominator, including exclusions.
     */
    static class SimpleRatioMeasure {
        private static final String MEASURE_ID = "RatioMeasure";
        private static final String LIBRARY_ID = "RatioLibrary";
        private static final String MEASURE_URL = BASE_MEASURE_URL + MEASURE_ID;
        private static final String LIBRARY_URL = BASE_LIBRARY_URL + LIBRARY_ID;

        /**
         * Builds the {@link Measure} resource for the "Simple Ratio Measure" scenario.
         * The measure is configured with the ratio scoring type, an initial population, numerator, and denominator,
         * including numerator and denominator exclusions.
         *
         * @return A {@link Measure} resource with the ratio scoring type and specified population groups.
         */
        public static Measure measure() {
            return MeasureBuilder.build(MEASURE_ID, MEASURE_URL, LIBRARY_URL, "ratio", MeasurePopulationGroup.initialPopulation(), MeasurePopulationGroup.numerator(), MeasurePopulationGroup.numeratorExclusion(), MeasurePopulationGroup.denominator(), MeasurePopulationGroup.denominatorExclusion());
        }

        /**
         * Builds the {@link Library} resource for the "Simple Ratio Measure" scenario.
         * The library contains CQL logic for evaluating the measure.
         *
         * @return A {@link Library} resource containing CQL logic for the ratio measure.
         */
        public static Library library() {
            return LibraryBuilder.build(LIBRARY_ID, "1.0.0", LIBRARY_ID, LIBRARY_URL, CqlLibraries.SIMPLE_RATIO);
        }

        /**
         * Builds a {@link Bundle} resource containing the {@link Measure} and {@link Library} for the "Simple Ratio Measure" scenario.
         *
         * @return A {@link Bundle} resource containing the measure and library.
         */
        public static Bundle bundle() {
            return BundleBuilder.build(library(), measure());
        }
    }

    /**
     * Nested utility class for building resources for a "Simple Continuous Variable Measure" scenario.
     * This class creates a {@link Measure}, {@link Library}, and {@link Bundle} to represent a continuous variable measure
     * with an initial population, measure population, and measure population exclusions.
     */
    static class SimpleContinuousVariableMeasure {
        private static final String MEASURE_ID = "ContinuousVariableMeasure";
        private static final String LIBRARY_ID = "ContinuousVariableLibrary";
        private static final String MEASURE_URL = BASE_MEASURE_URL + MEASURE_ID;
        private static final String LIBRARY_URL = BASE_LIBRARY_URL + LIBRARY_ID;

        /**
         * Builds the {@link Measure} resource for the "Simple Continuous Variable Measure" scenario.
         * The measure is configured with the continuous variable scoring type, an initial population,
         * a measure population, and measure population exclusions.
         *
         * @return A {@link Measure} resource with the continuous variable scoring type and specified population groups.
         */
        public static Measure measure() {
            return MeasureBuilder.build(MEASURE_ID, MEASURE_URL, LIBRARY_URL, "continuous-variable", MeasurePopulationGroup.initialPopulation(), MeasurePopulationGroup.measurePopulation(), MeasurePopulationGroup.measurePopulationExclusion());
        }

        /**
         * Builds the {@link Library} resource for the "Simple Continuous Variable Measure" scenario.
         * The library contains CQL logic for evaluating the measure.
         *
         * @return A {@link Library} resource containing CQL logic for the continuous variable measure.
         */
        public static Library library() {
            return LibraryBuilder.build(LIBRARY_ID, "1.0.0", LIBRARY_ID, LIBRARY_URL, CqlLibraries.SIMPLE_CONTINUOUS_VARIABLE);
        }

        /**
         * Builds a {@link Bundle} resource containing the {@link Measure} and {@link Library} for the "Simple Continuous Variable Measure" scenario.
         *
         * @return A {@link Bundle} resource containing the measure and library.
         */
        public static Bundle bundle() {
            return BundleBuilder.build(library(), measure());
        }
    }

    /**
     * Utility class for building {@link Measure} resources.
     */
    static class MeasureBuilder {
        /**
         * Creates a {@link Measure} with the specified parameters.
         *
         * @param id         The ID of the measure.
         * @param url        The URL of the measure.
         * @param libraryUrl The URL of the associated library.
         * @param scoring    The scoring type of the measure (e.g., "cohort", "proportion").
         * @param populations The population components of the measure.
         * @return A {@link Measure} with the specified configuration.
         */
        public static Measure build(String id, String url, String libraryUrl, String scoring, Measure.MeasureGroupPopulationComponent ... populations) {
            var measure = new Measure();
            measure.setUrl(url);
            measure.addLibrary(libraryUrl);
            measure.setScoring(new CodeableConcept().addCoding(new Coding().setCode(scoring)));
            measure.addGroup().setPopulation(Arrays.stream(populations).toList());
            measure.setId(id);
            return measure;
        }

        /**
         * Builds a FHIR {@link Measure} with a single Supplemental Data Element (SDE).
         *
         * @param id            The unique identifier for the measure.
         * @param url           The canonical URL for the measure.
         * @param libraryUrl    The canonical URL for the associated library.
         * @param type          The type of the measure (e.g., "outcome").
         * @param scoring       The scoring type of the measure (e.g., "cohort", "proportion").
         * @param sdeId         The unique identifier for the supplemental data element.
         * @param sdeDescription A human-readable description of the supplemental data element.
         * @param sdeExpression The CQL expression defining the supplemental data element.
         * @param populations   The population components for the measure (e.g., initial population, numerator, denominator).
         * @return A {@link Measure} resource configured with the specified attributes and a single SDE.
         */
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

    /**
     * Utility class for building {@link Library} resources.
     */
    static class LibraryBuilder {
        /**
         * Creates a {@link Library} with the specified parameters.
         *
         * @param id      The ID of the library.
         * @param version The version of the library.
         * @param name    The name of the library.
         * @param url     The URL of the library.
         * @param cql     The CQL content of the library.
         * @return A {@link Library} with the specified configuration.
         */
        public static Library build(String id, String version, String name, String url, String cql) {
            var library = new Library().setVersion(version).setName(name).setUrl(url);
            library.addContent().setContentType("text/cql").setData(cql.getBytes(StandardCharsets.UTF_8));
            library.setId(id);
            return library;
        }
    }

    /**
     * Utility class for building {@link Bundle} resources.
     */
    static class BundleBuilder {
        /**
         * Creates a {@link Bundle} containing the specified resources.
         *
         * @param resources The resources to include in the bundle.
         * @return A {@link Bundle} containing the specified resources.
         */
        public static Bundle build(Resource ... resources) {
            var bundle = new Bundle();
            Arrays.stream(resources).forEach(resource -> bundle.addEntry().setResource(resource));
            return bundle;
        }
    }
}
