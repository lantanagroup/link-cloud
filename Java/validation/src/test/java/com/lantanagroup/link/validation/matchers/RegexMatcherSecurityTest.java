package com.lantanagroup.link.validation.matchers;

import com.google.re2j.PatternSyntaxException;
import com.lantanagroup.link.validation.entities.Result;
import com.lantanagroup.link.validation.entities.ResultField;
import org.junit.jupiter.api.Test;

import java.time.Duration;

import static org.junit.jupiter.api.Assertions.*;

class RegexMatcherSecurityTest {
    @Test
    void nestedQuantifiersCompleteWithoutBacktracking() {
        RegexMatcher matcher = new RegexMatcher();
        matcher.setField(ResultField.MESSAGE);
        matcher.setRegex("(a+)+$");
        Result result = new Result();
        result.setMessage("a".repeat(100_000) + "!");

        assertTimeoutPreemptively(Duration.ofSeconds(2), () -> assertFalse(matcher.isMatch(result)));
        result.setMessage("aaa");
        assertTrue(matcher.isMatch(result));
    }

    @Test
    void rejectsUnsupportedBackreferences() {
        RegexMatcher matcher = new RegexMatcher();
        assertThrows(PatternSyntaxException.class, () -> matcher.setRegex("(a)\\1"));
    }
}
