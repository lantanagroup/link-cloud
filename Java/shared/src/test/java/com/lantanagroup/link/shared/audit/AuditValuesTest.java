package com.lantanagroup.link.shared.audit;

import org.junit.jupiter.api.Assertions;
import org.junit.jupiter.api.Test;

import java.math.BigDecimal;
import java.sql.Timestamp;
import java.time.Instant;
import java.time.OffsetDateTime;
import java.util.Date;
import java.util.concurrent.TimeUnit;

public class AuditValuesTest {
    @Test
    public void text_null_isEmpty() {
        Assertions.assertEquals("", AuditValues.text(null));
    }

    @Test
    public void text_enum_isName() {
        Assertions.assertEquals("SECONDS", AuditValues.text(TimeUnit.SECONDS));
    }

    @Test
    public void text_instantAndOffsetDateTime_areIso8601() {
        Assertions.assertEquals("2026-11-23T15:04:05.123Z",
                AuditValues.text(Instant.parse("2026-11-23T15:04:05.123Z")));
        Assertions.assertEquals("2026-11-23T15:04:05+02:00",
                AuditValues.text(OffsetDateTime.parse("2026-11-23T15:04:05+02:00")));
    }

    @Test
    public void text_sqlTimestampAndUtilDate_sameInstant_areEqual() {
        Date date = new Date(1763910245123L);
        Timestamp timestamp = new Timestamp(1763910245123L);

        Assertions.assertNotEquals(timestamp, date);
        Assertions.assertEquals(AuditValues.text(date), AuditValues.text(timestamp));
        Assertions.assertEquals(date.toInstant().toString(), AuditValues.text(date));
    }

    @Test
    public void text_sqlDate_isIsoDate() {
        Assertions.assertEquals("2026-11-23", Assertions.assertDoesNotThrow(
                () -> AuditValues.text(java.sql.Date.valueOf("2026-11-23"))));
    }

    @Test
    public void text_sqlTime_isIsoTime() {
        Assertions.assertEquals("15:04:05", Assertions.assertDoesNotThrow(
                () -> AuditValues.text(java.sql.Time.valueOf("15:04:05"))));
    }

    @Test
    public void text_bigDecimal_isPlainAndKeepsScale() {
        Assertions.assertEquals("1.50", AuditValues.text(new BigDecimal("1.50")));
        Assertions.assertEquals("1000", AuditValues.text(new BigDecimal("1E+3")));
    }

    @Test
    public void text_largeDouble_hasNoExponent() {
        Assertions.assertEquals("10000000000", AuditValues.text(1.0E10));
        Assertions.assertEquals("0.1", AuditValues.text(0.1f));
        Assertions.assertEquals("NaN", AuditValues.text(Double.NaN));
        Assertions.assertEquals("Infinity", AuditValues.text(Float.POSITIVE_INFINITY));
    }

    @Test
    public void text_boolean_isLowerCase() {
        Assertions.assertEquals("true", AuditValues.text(true));
        Assertions.assertEquals("false", AuditValues.text(Boolean.FALSE));
    }

    @Test
    public void propertyName_capitalisesFirstLetterOnly() {
        Assertions.assertEquals("ClientSecret", AuditValues.propertyName("clientSecret"));
        Assertions.assertEquals("Title", AuditValues.propertyName("title"));
        Assertions.assertEquals("URL", AuditValues.propertyName("uRL"));
    }
}
