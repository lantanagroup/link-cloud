package com.lantanagroup.link.measureeval.models;

import java.util.*;

public enum DebugSections {
    GROUPS,
    EXPRESSIONS,
    LIBRARY_DEBUG,
    MESSAGES,
    TRACES,
    DEBUG_LOG;

    private static final Map<String, DebugSections> ALIASES = Map.of(
            "groups", GROUPS,
            "expressions", EXPRESSIONS,
            "librarydebug", LIBRARY_DEBUG,
            "messages", MESSAGES,
            "traces", TRACES,
            "debuglog", DEBUG_LOG
    );

    public static Set<DebugSections> parse(String input) {
        if (input == null || input.isBlank() || "false".equalsIgnoreCase(input.trim())) {
            return EnumSet.noneOf(DebugSections.class);
        }
        String trimmed = input.trim();
        if ("true".equalsIgnoreCase(trimmed) || "all".equalsIgnoreCase(trimmed)) {
            return EnumSet.allOf(DebugSections.class);
        }
        Set<DebugSections> result = EnumSet.noneOf(DebugSections.class);
        for (String token : trimmed.split(",")) {
            String key = token.trim().toLowerCase(Locale.ROOT);
            DebugSections section = ALIASES.get(key);
            if (section != null) {
                result.add(section);
            }
        }
        return result;
    }

    public static boolean needsDebugLogging(Set<DebugSections> sections) {
        return sections.contains(EXPRESSIONS)
                || sections.contains(LIBRARY_DEBUG)
                || sections.contains(MESSAGES)
                || sections.contains(DEBUG_LOG);
    }

    public static boolean needsTracing(Set<DebugSections> sections) {
        return sections.contains(TRACES)
                || sections.contains(DEBUG_LOG);
    }
}
