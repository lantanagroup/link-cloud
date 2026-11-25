package com.lantanagroup.link.shared.audit;

import ch.qos.logback.classic.Level;
import ch.qos.logback.classic.Logger;
import ch.qos.logback.classic.spi.ILoggingEvent;
import ch.qos.logback.core.read.ListAppender;
import org.junit.jupiter.api.AfterEach;
import org.junit.jupiter.api.Assertions;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.Test;
import org.slf4j.LoggerFactory;
import org.springframework.data.annotation.CreatedDate;
import org.springframework.data.annotation.LastModifiedDate;
import org.springframework.data.annotation.Version;

import java.math.BigDecimal;
import java.sql.Timestamp;
import java.util.Date;
import java.util.List;
import java.util.Set;

public class AuditDiffTest {
    private final AuditDiff diff = new AuditDiff(new AuditMasking(List.of()));

    public static class Category {
        private String title;
        private boolean enabled;

        public Category(String title, boolean enabled) {
            this.title = title;
            this.enabled = enabled;
        }

        public String getTitle() {
            return title;
        }

        public void setTitle(String title) {
            this.title = title;
        }

        public boolean isEnabled() {
            return enabled;
        }

        public void setEnabled(boolean enabled) {
            this.enabled = enabled;
        }
    }

    public static class SpecialCategory extends Category {
        private String extra;

        public SpecialCategory(String title, String extra) {
            super(title, true);
            this.extra = extra;
        }

        public String getExtra() {
            return extra;
        }
    }

    public static class Inner {
        public String getName() {
            return "inner";
        }
    }

    public static class Complex {
        private final List<String> rules;
        private final byte[] content;
        private final Inner inner;

        public Complex(List<String> rules, byte[] content, Inner inner) {
            this.rules = rules;
            this.content = content;
            this.inner = inner;
        }

        public List<String> getRules() {
            return rules;
        }

        public byte[] getContent() {
            return content;
        }

        public Inner getInner() {
            return inner;
        }
    }

    public static class Bookkeeping {
        private final Date lastUpdated;
        private final long version;
        private final Date created;
        private final Date modified;

        public Bookkeeping(Date lastUpdated, long version, Date created, Date modified) {
            this.lastUpdated = lastUpdated;
            this.version = version;
            this.created = created;
            this.modified = modified;
        }

        public Date getLastUpdated() {
            return lastUpdated;
        }

        @Version
        public long getVersion() {
            return version;
        }

        @CreatedDate
        public Date getCreated() {
            return created;
        }

        public Date getModified() {
            return modified;
        }
    }

    public static class FieldBookkeeping {
        @Version
        private final long version;
        @LastModifiedDate
        private final Date modified;

        public FieldBookkeeping(long version, Date modified) {
            this.version = version;
            this.modified = modified;
        }

        public long getVersion() {
            return version;
        }

        public Date getModified() {
            return modified;
        }
    }

    public static class Secrets {
        private final String password;
        private final String signingKey;
        @AuditMask
        private final String email;
        private final String title;

        public Secrets(String password, String signingKey, String email, String title) {
            this.password = password;
            this.signingKey = signingKey;
            this.email = email;
            this.title = title;
        }

        public String getPassword() {
            return password;
        }

        public String getSigningKey() {
            return signingKey;
        }

        public String getEmail() {
            return email;
        }

        public String getTitle() {
            return title;
        }
    }

    public static class Dated {
        private final Date when;

        public Dated(Date when) {
            this.when = when;
        }

        public Date getWhen() {
            return when;
        }
    }

    public static class Unrelated {
        public String getTitle() {
            return "x";
        }
    }

    public static class Fragile {
        private final String name;
        private final boolean fail;

        public Fragile(String name, boolean fail) {
            this.name = name;
            this.fail = fail;
        }

        public String getName() {
            return name;
        }

        public String getBroken() {
            if (fail) {
                throw new IllegalStateException("boom");
            }
            return "ok";
        }
    }

    public static class Priced {
        private final BigDecimal price;
        private final Integer count;

        public Priced(BigDecimal price, Integer count) {
            this.price = price;
            this.count = count;
        }

        public BigDecimal getPrice() {
            return price;
        }

        public Integer getCount() {
            return count;
        }
    }

    public static class GetterMasked {
        private final String pin;

        public GetterMasked(String pin) {
            this.pin = pin;
        }

        @AuditMask
        public String getPin() {
            return pin;
        }
    }

    // A Lombok-style field: the bean property of getEMail() is "EMail", the field is "eMail".
    public static class CaseDiffering {
        @AuditMask
        private final String eMail;

        public CaseDiffering(String eMail) {
            this.eMail = eMail;
        }

        public String getEMail() {
            return eMail;
        }
    }

    public static class Renamed {
        @AuditMask
        private final String socialSecurityNumber;

        public Renamed(String socialSecurityNumber) {
            this.socialSecurityNumber = socialSecurityNumber;
        }

        public String getSsn() {
            return socialSecurityNumber;
        }
    }

    /** A marked field whose getter has another name, beside a property with its own field and a computed one. */
    public static class Mixed {
        @AuditMask
        private final String socialSecurityNumber;
        private final String name;

        public Mixed(String socialSecurityNumber, String name) {
            this.socialSecurityNumber = socialSecurityNumber;
            this.name = name;
        }

        public String getSsn() {
            return socialSecurityNumber;
        }

        public String getName() {
            return name;
        }

        public String getInitial() {
            return name.substring(0, 1);
        }
    }

    /** A marked field that no getter can be showing: the only getter has a field of its own name. */
    public static class Sealed {
        @AuditMask
        private final String pin;
        private final String label;

        public Sealed(String pin, String label) {
            this.pin = pin;
            this.label = label;
        }

        public String getLabel() {
            return label;
        }
    }

    public static class HeldDetail {
        private final String value;

        public HeldDetail(String value) {
            this.value = value;
        }

        public String getValue() {
            return value;
        }
    }

    /** A marked field holding an object, shown by a getter of another name. */
    public static class Holder {
        @AuditMask
        private final HeldDetail secretInner;

        public Holder(HeldDetail secretInner) {
            this.secretInner = secretInner;
        }

        public HeldDetail getPayload() {
            return secretInner;
        }
    }

    private final Logger diffLogger = (Logger) LoggerFactory.getLogger(AuditDiff.class);
    private final ListAppender<ILoggingEvent> logs = new ListAppender<>();
    private Level previousLevel;

    @BeforeEach
    public void captureLogs() {
        logs.start();
        previousLevel = diffLogger.getLevel();
        diffLogger.setLevel(Level.DEBUG);
        diffLogger.addAppender(logs);
        diffLogger.setAdditive(false);
    }

    @AfterEach
    public void releaseLogs() {
        diffLogger.detachAppender(logs);
        diffLogger.setAdditive(true);
        diffLogger.setLevel(previousLevel);
    }

    private List<ILoggingEvent> logged(Level level) {
        return logs.list.stream().filter(e -> e.getLevel() == level).toList();
    }

    @Test
    public void auditMaskOnGetter_masksTheProperty() {
        Assertions.assertEquals(List.of(new PropertyChange("Pin", AuditMasking.REDACTED, AuditMasking.REDACTED)),
                diff.diff(new GetterMasked("1111"), new GetterMasked("2222")));
    }

    @Test
    public void auditMaskOnFieldDifferingOnlyInCase_masksTheProperty() {
        Assertions.assertEquals(List.of(new PropertyChange("EMail", AuditMasking.REDACTED, AuditMasking.REDACTED)),
                diff.diff(new CaseDiffering("a@x"), new CaseDiffering("b@x")));
        Assertions.assertTrue(logged(Level.WARN).isEmpty());
    }

    /**
     * The mark cannot be matched to the getter, so the getter that may be showing the value is masked: the
     * value must not get out because of how a getter is named.
     */
    @Test
    public void auditMaskOnFieldWithNoMatchingProperty_masksTheGetterThatMayShowIt_andWarnsOncePerClass() {
        List<PropertyChange> first = diff.diff(new Renamed("111-11-1111"), new Renamed("222-22-2222"));
        List<PropertyChange> second = diff.diff(new Renamed("333-33-3333"), new Renamed("444-44-4444"));

        List<PropertyChange> masked =
                List.of(new PropertyChange("Ssn", AuditMasking.REDACTED, AuditMasking.REDACTED));
        Assertions.assertEquals(masked, first);
        Assertions.assertEquals(masked, second);
        List<ILoggingEvent> warnings = logged(Level.WARN);
        Assertions.assertEquals(1, warnings.size());
        String message = warnings.get(0).getFormattedMessage();
        Assertions.assertTrue(message.contains(Renamed.class.getName()), message);
        Assertions.assertTrue(message.contains("socialSecurityNumber"), message);
        Assertions.assertTrue(message.contains("getter"), message);
        Assertions.assertTrue(message.contains("[Ssn]"), message);
        Assertions.assertFalse(message.contains("masks nothing"), message);
        for (String value : List.of("111-11-1111", "222-22-2222", "333-33-3333", "444-44-4444")) {
            Assertions.assertFalse(message.contains(value), message);
        }
    }

    /** Only a property that could be the unmatched field's getter is masked: one with no field of its own name. */
    @Test
    public void auditMaskOnFieldWithNoMatchingProperty_leavesAPropertyWithItsOwnFieldAsItIs() {
        List<PropertyChange> changes =
                diff.diff(new Mixed("111-11-1111", "Ada"), new Mixed("222-22-2222", "Grace"));

        Assertions.assertEquals(Set.of(
                new PropertyChange("Name", "Ada", "Grace"),
                new PropertyChange("Ssn", AuditMasking.REDACTED, AuditMasking.REDACTED),
                new PropertyChange("Initial", AuditMasking.REDACTED, AuditMasking.REDACTED)), Set.copyOf(changes));
        Assertions.assertEquals(3, changes.size());
    }

    /** A marked field that no getter can be showing masks nothing else, and there is nothing to warn about. */
    @Test
    public void auditMaskOnFieldWithNoGetterThatCouldShowIt_masksNothingElse_andDoesNotWarn() {
        Assertions.assertEquals(List.of(new PropertyChange("Label", "Main", "Spare")),
                diff.diff(new Sealed("1111", "Main"), new Sealed("2222", "Spare")));
        Assertions.assertTrue(logged(Level.WARN).isEmpty());
    }

    /** The same holds for a getter that returns an object: everything read inside it is masked. */
    @Test
    public void auditMaskOnFieldHoldingAnObject_shownByAGetterOfAnotherName_masksWhatIsInside() {
        AuditDiff.ObjectChanges changes = diff.objectChanges(
                new Holder(new HeldDetail("111-11-1111")), new Holder(new HeldDetail("222-22-2222")), "7", false);

        Assertions.assertNotNull(changes);
        Assertions.assertEquals(
                List.of(new PropertyChange("Value", AuditMasking.REDACTED, AuditMasking.REDACTED, "HeldDetail", "7")),
                changes.changes());
    }

    @Test
    public void getterThatThrows_warnsOnce_thenStaysQuiet() {
        diff.diff(new Fragile("a", true), new Fragile("b", true));
        diff.diff(new Fragile("c", true), new Fragile("d", true));

        Assertions.assertEquals(1, logged(Level.WARN).size());
        Assertions.assertTrue(logged(Level.WARN).get(0).getFormattedMessage().contains("broken"));
        Assertions.assertEquals(1, logged(Level.DEBUG).stream()
                .filter(e -> e.getFormattedMessage().contains("getter failed")).count());
    }

    @Test
    public void diff_reportsOnlyChangedSimpleProperties() {
        List<PropertyChange> changes = diff.diff(new Category("Old", true), new Category("New", true));

        Assertions.assertEquals(List.of(new PropertyChange("Title", "Old", "New", null, null)), changes);
    }

    @Test
    public void diff_skipsCollectionsArraysAndAssociations() {
        Complex before = new Complex(List.of("a"), new byte[]{1}, new Inner());
        Complex after = new Complex(List.of("b"), new byte[]{2}, new Inner());

        Assertions.assertTrue(diff.diff(before, after).isEmpty());
    }

    @Test
    public void diff_skipsTimestampNamesAndSpringDataBookkeeping() {
        Date a = new Date(1_000_000L);
        Date b = new Date(2_000_000L);

        // lastUpdated is a timestamp name, version and created are annotated on the getter; "modified" is
        // neither, so it is the one property reported
        Assertions.assertEquals(List.of(new PropertyChange("Modified", "1970-01-01T00:16:40Z", "1970-01-01T00:33:20Z")),
                diff.diff(new Bookkeeping(a, 1, a, a), new Bookkeeping(b, 2, b, b)));
        Assertions.assertTrue(diff.diff(new FieldBookkeeping(1, a), new FieldBookkeeping(2, b)).isEmpty());
    }

    @Test
    public void diff_masksByNameByPatternAndByAnnotation() {
        List<PropertyChange> changes = diff.diff(new Secrets("p1", "k1", "a@x", "t"), new Secrets("p2", "k2", "b@x", "t"));

        Assertions.assertEquals(3, changes.size());
        for (PropertyChange change : changes) {
            Assertions.assertEquals(AuditMasking.REDACTED, change.initialPropertyValue(), change.propertyName());
            Assertions.assertEquals(AuditMasking.REDACTED, change.newPropertyValue(), change.propertyName());
        }
        Assertions.assertEquals(List.of("Email", "Password", "SigningKey"),
                changes.stream().map(PropertyChange::propertyName).sorted().toList());
    }

    @Test
    public void diff_maskedUnchangedProperty_isNotReported() {
        Assertions.assertTrue(diff.diff(new Secrets("p", "k", "a@x", "t"), new Secrets("p", "k", "a@x", "t")).isEmpty());
    }

    @Test
    public void diff_eitherNull_isEmpty() {
        Assertions.assertTrue(diff.diff(null, new Category("a", true)).isEmpty());
        Assertions.assertTrue(diff.diff(new Category("a", true), null).isEmpty());
        Assertions.assertTrue(diff.diff(null, null).isEmpty());
    }

    @Test
    public void diff_sqlTimestampAgainstEqualDate_isNotAChange() {
        Timestamp loaded = new Timestamp(1_700_000_000_000L);
        Date parsed = new Date(1_700_000_000_000L);

        Assertions.assertTrue(diff.diff(new Dated(loaded), new Dated(parsed)).isEmpty());
        Assertions.assertTrue(diff.diff(new Dated(parsed), new Dated(loaded)).isEmpty());
    }

    @Test
    public void diff_unrelatedClasses_isEmptyAndDoesNotThrow() {
        Assertions.assertDoesNotThrow(() -> Assertions.assertTrue(diff.diff(new Category("a", true), new Unrelated()).isEmpty()));
    }

    @Test
    public void diff_subclassAgainstParent_comparesParentProperties() {
        List<PropertyChange> changes = diff.diff(new SpecialCategory("Old", "x"), new Category("New", true));

        Assertions.assertEquals(List.of(new PropertyChange("Title", "Old", "New")), changes);
    }

    @Test
    public void diff_getterThatThrows_isSkippedAndOthersReported() {
        List<PropertyChange> changes = diff.diff(new Fragile("a", true), new Fragile("b", true));

        Assertions.assertEquals(List.of(new PropertyChange("Name", "a", "b")), changes);
    }

    @Test
    public void diff_nullToValue_reportsEmptyStringAsOld() {
        List<PropertyChange> changes = diff.diff(new Priced(null, null), new Priced(null, 5));

        Assertions.assertEquals(List.of(new PropertyChange("Count", "", "5")), changes);
    }

    @Test
    public void diff_bigDecimalDifferingOnlyInScale_isNotAChange() {
        Assertions.assertTrue(diff.diff(new Priced(new BigDecimal("1.0"), 1), new Priced(new BigDecimal("1.00"), 1)).isEmpty());
        Assertions.assertEquals(1, diff.diff(new Priced(new BigDecimal("1.0"), 1), new Priced(new BigDecimal("1.5"), 1)).size());
    }

    @Test
    public void single_equalValues_isStillRecorded() {
        Assertions.assertEquals(new PropertyChange("Count", "1", "1"), diff.single("Count", 1, 1));
    }

    @Test
    public void single_nameIsRecordedAsGiven() {
        Assertions.assertEquals("status", diff.single("status", "a", "b").propertyName());
    }

    @Test
    public void single_timestampName_isRecorded() {
        Assertions.assertEquals(new PropertyChange("LastUpdated", "a", "b"), diff.single("LastUpdated", "a", "b"));
    }

    @Test
    public void single_masksByName() {
        Assertions.assertEquals(new PropertyChange("password", "[redacted]", "[redacted]"),
                diff.single("password", "a", "b"));
        Assertions.assertEquals(new PropertyChange("signingKey", "[redacted]", "[redacted]"),
                diff.single("signingKey", "a", "b"));
    }
}
