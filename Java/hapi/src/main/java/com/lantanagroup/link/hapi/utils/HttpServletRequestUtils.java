package com.lantanagroup.link.hapi.utils;

import javax.servlet.http.HttpServletRequest;
import java.util.regex.Pattern;

public class HttpServletRequestUtils {
    private static final Pattern LOG_CONTROL_CHARACTERS = Pattern.compile("[\\p{Cc}\\p{Zl}\\p{Zp}]");

    public static String sanitizeForLog(String value) {
        return value == null ? null : LOG_CONTROL_CHARACTERS.matcher(value).replaceAll("_");
    }

    public static String getClientIpAddress(HttpServletRequest request) {
        String xForwardedFor = request.getHeader("X-Forwarded-For");
        if (xForwardedFor != null && !xForwardedFor.isEmpty()) {
            return xForwardedFor.split(",")[0].trim();
        }

        String xRealIp = request.getHeader("X-Real-IP");
        if (xRealIp != null && !xRealIp.isEmpty()) {
            return xRealIp;
        }

        String remoteAddr = request.getRemoteAddr();
        if (remoteAddr != null && !remoteAddr.isEmpty()) {
            return remoteAddr;
        }

        return "0.0.0.0";
    }
}
