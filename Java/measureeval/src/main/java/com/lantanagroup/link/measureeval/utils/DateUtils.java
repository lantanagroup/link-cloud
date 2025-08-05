package com.lantanagroup.link.measureeval.utils;

import org.hl7.fhir.r4.model.DateTimeType;

import java.time.Instant;
import java.time.ZoneId;
import java.time.ZonedDateTime;

public class DateUtils {

    public static ZonedDateTime getZonedDateTime(DateTimeType dateTime) {
        if (dateTime == null) {
            return null;
        }
        var zoneId = dateTime.getTimeZone() == null ? ZoneId.systemDefault() : dateTime.getTimeZone().toZoneId();
        return getZonedDateTime(dateTime.getValue().toInstant(), zoneId);
    }

    public static ZonedDateTime getZonedDateTime(Instant instant, ZoneId zoneId) {
        return ZonedDateTime.ofInstant(instant, zoneId);
    }
}
