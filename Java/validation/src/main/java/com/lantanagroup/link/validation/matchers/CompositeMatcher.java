package com.lantanagroup.link.validation.matchers;

import com.lantanagroup.link.validation.entities.Result;
import io.swagger.v3.oas.annotations.media.Schema;
import lombok.Getter;
import lombok.Setter;
import org.apache.commons.collections4.CollectionUtils;

import java.util.List;

@Getter
@Setter
public class CompositeMatcher extends InvertibleMatcher {
    @Schema(
            description = "A list of rules (Matcher interface implementations) that should be executed.",
            oneOf = { CompositeMatcher.class, RegexMatcher.class }
    )
    private List<Matcher> children;

    @Schema(description = "Whether to require that all children be a match in order for this category to be associated with a validation rule.")
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
