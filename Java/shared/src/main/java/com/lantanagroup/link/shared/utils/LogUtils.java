package com.lantanagroup.link.shared.utils;

import java.util.HashMap;
import java.util.Map;

public class LogUtils {
    private static final Map<Character, Character> SAFE_CHARACTERS;

    static {
        SAFE_CHARACTERS = new HashMap<>();
        for (char ch = 32; ch <= 255; ch++) {
            if (ch == 127) {
                continue;
            }
            SAFE_CHARACTERS.put(ch, ch);
        }
    }

    public static String sanitize(Object value) {
        if (value == null) {
            return null;
        }
        String str = value.toString();
        StringBuilder builder = new StringBuilder(str.length());
        for (int index = 0; index < str.length(); index++) {
            char ch = str.charAt(index);
            builder.append(sanitize(ch));
        }
        return builder.toString();
    }

    private static char sanitize(char value) {
        return SAFE_CHARACTERS.getOrDefault(value, ' ');
    }
}
