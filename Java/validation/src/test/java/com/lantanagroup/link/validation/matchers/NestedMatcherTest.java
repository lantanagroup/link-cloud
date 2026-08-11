package com.lantanagroup.link.validation.matchers;

import com.fasterxml.jackson.databind.ObjectMapper;
import com.lantanagroup.link.validation.converters.MatcherConverter;
import com.lantanagroup.link.validation.entities.Result;
import org.junit.jupiter.api.Test;

import static org.junit.jupiter.api.Assertions.assertFalse;
import static org.junit.jupiter.api.Assertions.assertInstanceOf;
import static org.junit.jupiter.api.Assertions.assertTrue;

/**
 * The category rules that need "(one of these positives) AND (none of these exclusions)" nest a
 * CompositeMatcher inside another one. Since {@link Matcher} resolves subtypes with
 * {@code JsonTypeInfo.Id.DEDUCTION} — no explicit type field to fall back on — this verifies
 * nesting actually round-trips, through the DB converter as well as plain Jackson, before any
 * category rule depends on it.
 */
class NestedMatcherTest {

    private final ObjectMapper objectMapper = new ObjectMapper();
    private final MatcherConverter converter = new MatcherConverter(objectMapper);

    /** (starts with A OR starts with B) AND NOT containing "skip". */
    private static final String NESTED_JSON = """
            {
              "children": [
                {
                  "children": [
                    { "field": "MESSAGE", "regex": "^Alpha" },
                    { "field": "MESSAGE", "regex": "^Bravo" }
                  ],
                  "requiresAllChildren": false
                },
                { "field": "MESSAGE", "regex": "skip", "inverted": true }
              ],
              "requiresAllChildren": true
            }
            """;

    private static Result withMessage(String message) {
        Result result = new Result();
        result.setMessage(message);
        return result;
    }

    @Test
    void deductionResolvesANestedComposite() throws Exception {
        Matcher matcher = objectMapper.readValue(NESTED_JSON, Matcher.class);

        CompositeMatcher outer = assertInstanceOf(CompositeMatcher.class, matcher);
        assertTrue(outer.isRequiresAllChildren());
        assertInstanceOf(CompositeMatcher.class, outer.getChildren().get(0));
        assertInstanceOf(RegexMatcher.class, outer.getChildren().get(1));
    }

    @Test
    void nestedCompositeAppliesInnerOrAndOuterExclusion() throws Exception {
        Matcher matcher = objectMapper.readValue(NESTED_JSON, Matcher.class);

        assertTrue(matcher.isMatch(withMessage("Alpha happened")));
        assertTrue(matcher.isMatch(withMessage("Bravo happened")));
        assertFalse(matcher.isMatch(withMessage("Charlie happened")), "neither positive branch matched");
        assertFalse(matcher.isMatch(withMessage("Alpha but skip this")), "exclusion must veto a positive match");
    }

    @Test
    void nestedCompositeSurvivesTheDatabaseConverterRoundTrip() throws Exception {
        Matcher original = objectMapper.readValue(NESTED_JSON, Matcher.class);

        Matcher restored = converter.convertToEntityAttribute(converter.convertToDatabaseColumn(original));

        assertInstanceOf(CompositeMatcher.class, restored);
        assertTrue(restored.isMatch(withMessage("Alpha happened")));
        assertFalse(restored.isMatch(withMessage("Alpha but skip this")));
        assertFalse(restored.isMatch(withMessage("Charlie happened")));
    }

    /**
     * The defect the structural guard in CategoriesJsonStructureTest exists for: OR-ing an inverted
     * child makes the matcher a catch-all, because "does not match X" is true for almost every
     * message.
     */
    @Test
    void orWithAnInvertedChildMatchesEverything() throws Exception {
        String broken = """
                {
                  "children": [
                    { "field": "MESSAGE", "regex": "^Alpha" },
                    { "field": "MESSAGE", "regex": "skip", "inverted": true }
                  ],
                  "requiresAllChildren": false
                }
                """;
        Matcher matcher = objectMapper.readValue(broken, Matcher.class);

        assertTrue(matcher.isMatch(withMessage("totally unrelated message")),
                "demonstrates the catch-all; the shipped rules must not behave this way");
    }
}
