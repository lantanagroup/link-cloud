package com.lantanagroup.link.measureeval.records;

import com.fasterxml.jackson.annotation.JsonSetter;
import com.fasterxml.jackson.annotation.Nulls;
import lombok.Getter;
import lombok.Setter;

import java.util.List;

@Getter
@Setter
public class ReportScheduled {
    @JsonSetter(nulls = Nulls.AS_EMPTY)
    private List<Parameter> parameters;

    @Getter
    @Setter
    public static class Parameter {
        private String key;
        private String value;
    }

    @Getter
    @Setter
    public static class Key {
        private String facilityId;
        private String reportType;
    }
}
