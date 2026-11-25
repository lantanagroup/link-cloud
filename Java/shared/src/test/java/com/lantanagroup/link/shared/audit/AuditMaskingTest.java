package com.lantanagroup.link.shared.audit;

import org.junit.jupiter.api.Assertions;
import org.junit.jupiter.api.Test;

import java.lang.reflect.Method;
import java.util.ArrayList;
import java.util.List;

public class AuditMaskingTest {
    private static final List<String> SHARED_NAMES = List.of(
            "Password", "Secret", "ClientSecret", "Token", "AccessToken", "RefreshToken", "ApiKey",
            "PrivateKey", "Passphrase", "ConnectionString", "Authentication", "Credentials");
    private static final List<String> TIMESTAMP_NAMES = List.of(
            "ModifyDate", "ModifiedDate", "ModifiedOn", "LastModifiedOn", "UpdatedAt", "LastUpdatedDate",
            "LastUpdated");

    private static AuditMasking shared() {
        return new AuditMasking(List.of());
    }

    @Test
    public void isMasked_everySharedName_ignoringCase() {
        AuditMasking masking = shared();
        Assertions.assertEquals(12, SHARED_NAMES.size());
        for (String name : SHARED_NAMES) {
            Assertions.assertTrue(masking.isMasked(name), name);
            Assertions.assertTrue(masking.isMasked(name.toUpperCase()), name.toUpperCase());
            Assertions.assertTrue(masking.isMasked(name.toLowerCase()), name.toLowerCase());
        }
        Assertions.assertTrue(masking.isMasked("PASSWORD"));
        Assertions.assertTrue(masking.isMasked("apikey"));
    }

    @Test
    public void isMasked_patterns() {
        AuditMasking masking = shared();
        Assertions.assertTrue(masking.isMasked("ClientSecretValue"));
        Assertions.assertTrue(masking.isMasked("oldPasswordHash"));
        Assertions.assertTrue(masking.isMasked("IdTokenHint"));
        Assertions.assertTrue(masking.isMasked("SigningKey"));
        Assertions.assertFalse(masking.isMasked("Keyboard"));
        Assertions.assertFalse(masking.isMasked("Title"));
        Assertions.assertFalse(masking.isMasked(null));
    }

    @Test
    public void isTimestamp_everySharedName() {
        AuditMasking masking = shared();
        for (String name : TIMESTAMP_NAMES) {
            Assertions.assertTrue(masking.isTimestamp(name), name);
            Assertions.assertTrue(masking.isTimestamp(name.toLowerCase()), name.toLowerCase());
        }
        Assertions.assertFalse(masking.isTimestamp("Title"));
    }

    @Test
    public void customizer_addsNamePatternAndTimestamp() {
        AuditMasking masking = new AuditMasking(List.of(
                a -> a.addName("Ssn").addPattern("*Pin*"),
                a -> a.addTimestampName("SyncedAt")));

        Assertions.assertTrue(masking.isMasked("ssn"));
        Assertions.assertTrue(masking.isMasked("PinCode"));
        Assertions.assertTrue(masking.isTimestamp("syncedat"));
        Assertions.assertTrue(masking.names().contains("SSN"));
        Assertions.assertEquals("*Pin*", masking.patterns().get(masking.patterns().size() - 1));
        Assertions.assertTrue(masking.timestampNames().contains("SyncedAt"));
        // the shared entries are still there
        Assertions.assertTrue(masking.isMasked("Password"));
        Assertions.assertTrue(masking.isTimestamp("UpdatedAt"));
    }

    @Test
    public void customizer_cannotRemove() {
        AuditMasking masking = shared();
        Assertions.assertThrows(UnsupportedOperationException.class, () -> masking.names().remove("Password"));
        Assertions.assertThrows(UnsupportedOperationException.class, () -> masking.names().clear());
        Assertions.assertThrows(UnsupportedOperationException.class, () -> masking.patterns().remove("*Key"));
        Assertions.assertThrows(UnsupportedOperationException.class, () -> masking.patterns().clear());
        Assertions.assertThrows(UnsupportedOperationException.class, () -> masking.timestampNames().remove("UpdatedAt"));
        Assertions.assertThrows(UnsupportedOperationException.class, () -> masking.timestampNames().clear());

        for (Method method : AuditMasking.Additions.class.getDeclaredMethods()) {
            String name = method.getName().toLowerCase();
            Assertions.assertFalse(name.contains("remove") || name.contains("clear") || name.contains("delete"), name);
        }
        Assertions.assertTrue(masking.isMasked("Password"));
    }

    @Test
    public void blankAdditions_areIgnored() {
        List<String> before = new ArrayList<>(shared().patterns());
        AuditMasking masking = new AuditMasking(List.of(a -> a
                .addName(null).addName("").addName("   ")
                .addPattern(null).addPattern("").addPattern("  ")
                .addTimestampName(null).addTimestampName("").addTimestampName(" ")));

        Assertions.assertEquals(SHARED_NAMES.size(), masking.names().size());
        Assertions.assertEquals(before, masking.patterns());
        Assertions.assertEquals(TIMESTAMP_NAMES.size(), masking.timestampNames().size());
        Assertions.assertFalse(masking.isMasked("Title"));
    }
}
