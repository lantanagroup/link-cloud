package com.lantanagroup.link.validation.matchers;

import com.lantanagroup.link.validation.entities.Result;
import io.swagger.v3.oas.annotations.media.Schema;
import lombok.Getter;
import lombok.Setter;

@Getter
@Setter
public abstract class InvertibleMatcher implements Matcher {
    @Schema(description = "If true, the rule is considered a match when it does NOT match the input (logical negation).")
    private boolean inverted;

    @Override
    public boolean isMatch(Result result) {
        boolean isMatch = doIsMatch(result);
        return inverted ? !isMatch : isMatch;
    }

    protected abstract boolean doIsMatch(Result result);
}
