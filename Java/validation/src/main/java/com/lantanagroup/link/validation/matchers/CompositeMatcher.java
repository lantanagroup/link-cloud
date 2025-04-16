package com.lantanagroup.link.validation.matchers;

import com.lantanagroup.link.validation.entities.Result;
import lombok.Getter;
import lombok.Setter;
import org.apache.commons.collections4.CollectionUtils;

import java.util.List;

@Getter
@Setter
public class CompositeMatcher extends InvertibleMatcher {
    private List<Matcher> children;
    private boolean requiresAllChildren;

    @Override
    protected boolean doIsMatch(Result result) {
        if (CollectionUtils.isEmpty(children)) {
            throw new IllegalStateException("No children specified");
        }
        boolean earlyReturn = !requiresAllChildren;
        for (Matcher child : children) {
            if (child.isMatch(result) == earlyReturn) {
                return earlyReturn;
            }
        }
        return !earlyReturn;
    }
}
