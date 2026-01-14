package com.lantanagroup.link.validation.matchers;

import com.fasterxml.jackson.annotation.JsonSubTypes;
import com.fasterxml.jackson.annotation.JsonTypeInfo;
import com.lantanagroup.link.validation.entities.Result;

@JsonTypeInfo(use = JsonTypeInfo.Id.DEDUCTION)
@JsonSubTypes({
        @JsonSubTypes.Type(CompositeMatcher.class),
        @JsonSubTypes.Type(RegexMatcher.class)
})
public interface Matcher {
    boolean isMatch(Result result);
}
