package com.lantanagroup.link.measureeval.utils;

import org.hl7.fhir.r4.model.Parameters;
import org.hl7.fhir.r4.model.Resource;
import org.hl7.fhir.r4.model.Type;

import java.util.function.Function;

public class ParametersUtils {
    private static <T extends S, S> T get(
            Parameters parameters,
            String name,
            Class<T> type,
            Function<Parameters.ParametersParameterComponent, S> getter) {
        Parameters.ParametersParameterComponent parameter = parameters.getParameter(name);
        if (parameter == null) {
            throw new IllegalArgumentException(String.format("Parameter not found: %s", name));
        }
        S value = getter.apply(parameter);
        if (!type.isInstance(value)) {
            throw new IllegalArgumentException(
                    String.format("Parameter not instance of type %s: %s", type.getName(), name));
        }
        return type.cast(value);
    }

    public static <T extends Type> T getValue(Parameters parameters, String name, Class<T> type) {
        return get(parameters, name, type, Parameters.ParametersParameterComponent::getValue);
    }

    public static <T extends Resource> T getResource(Parameters parameters, String name, Class<T> resourceType) {
        return get(parameters, name, resourceType, Parameters.ParametersParameterComponent::getResource);
    }
}
