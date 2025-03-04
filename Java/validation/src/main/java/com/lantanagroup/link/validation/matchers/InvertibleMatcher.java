package com.lantanagroup.link.validation.matchers;

import com.lantanagroup.link.validation.entities.Result;
import lombok.Getter;
import lombok.Setter;

@Getter
@Setter
public abstract class InvertibleMatcher implements Matcher {
    private boolean inverted;

    @Override
    public boolean isMatch(Result result) {
        boolean isMatch = doIsMatch(result);
        return inverted ? !isMatch : isMatch;
    }

    protected abstract boolean doIsMatch(Result result);
}
