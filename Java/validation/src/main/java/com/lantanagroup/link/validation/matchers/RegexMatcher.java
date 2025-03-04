package com.lantanagroup.link.validation.matchers;

import com.fasterxml.jackson.annotation.JsonIgnore;
import com.lantanagroup.link.validation.entities.Result;
import com.lantanagroup.link.validation.entities.ResultField;
import lombok.AccessLevel;
import lombok.Getter;
import lombok.Setter;

import java.util.regex.Pattern;

@Getter
@Setter
public class RegexMatcher extends InvertibleMatcher {
    private ResultField field;
    private String regex;

    @JsonIgnore
    @Setter(AccessLevel.NONE)
    private Pattern pattern;

    public void setRegex(String regex) {
        this.regex = regex;
        pattern = Pattern.compile(regex);
    }

    @Override
    protected boolean doIsMatch(Result result) {
        if (field == null) {
            throw new IllegalStateException("No field specified");
        }
        if (pattern == null) {
            throw new IllegalStateException("No pattern specified");
        }
        String value = field.getValue(result);
        return value != null && pattern.matcher(value).find();
    }
}
