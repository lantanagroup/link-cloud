package com.lantanagroup.link.validation.configs;

import com.lantanagroup.link.validation.entities.ResultField;
import lombok.Getter;
import lombok.Setter;

import java.util.List;

@Getter
@Setter
public class ValidationResultIgnoreRuleConfig {
    private String id;
    private String description;
    private MatcherConfig matcher;

    @Getter
    @Setter
    public static class MatcherConfig {
        private ResultField field;
        private String regex;
        private boolean inverted;
        private boolean requiresAllChildren;
        private List<MatcherConfig> children;
    }
}