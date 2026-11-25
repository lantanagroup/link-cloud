package com.lantanagroup.link.shared.audit;

import jakarta.persistence.Embeddable;
import jakarta.persistence.Embedded;
import jakarta.persistence.Entity;
import jakarta.persistence.EntityManagerFactory;
import jakarta.persistence.Id;
import org.hibernate.Session;
import org.hibernate.SessionFactory;
import org.hibernate.Transaction;
import org.hibernate.annotations.JdbcTypeCode;
import org.hibernate.cfg.Configuration;
import org.hibernate.type.SqlTypes;
import org.junit.jupiter.api.AfterAll;
import org.junit.jupiter.api.AfterEach;
import org.junit.jupiter.api.Assertions;
import org.junit.jupiter.api.BeforeAll;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.support.DefaultListableBeanFactory;
import org.springframework.mock.web.MockHttpServletRequest;
import org.springframework.web.context.request.RequestContextHolder;
import org.springframework.web.context.request.ServletRequestAttributes;

import java.util.ArrayList;
import java.util.Date;
import java.util.List;
import java.util.function.Consumer;

/**
 * Runs {@link HibernateAuditChangeListener} against a real Hibernate session for objects kept in a JSON column,
 * which are reported field by field the way the .NET services report an object they own and store as JSON.
 */
public class HibernateAuditJsonColumnTest {
    private static SessionFactory sessionFactory;

    private MockHttpServletRequest request;

    /**
     * An object kept as JSON, the shape Tenant's {@code ScheduledReports} has. It does not say when two of it
     * are equal, as such classes rarely do.
     */
    public static class Schedule {
        private String name;
        private String[] daily;
        @AuditMask
        private String token;
        private Date updatedAt;
        private Window window;

        public Schedule() {
        }

        Schedule(String name, String... daily) {
            this.name = name;
            this.daily = daily;
        }

        public String getName() {
            return name;
        }

        public void setName(String name) {
            this.name = name;
        }

        public String[] getDaily() {
            return daily;
        }

        public void setDaily(String[] daily) {
            this.daily = daily;
        }

        public String getToken() {
            return token;
        }

        public void setToken(String token) {
            this.token = token;
        }

        public Date getUpdatedAt() {
            return updatedAt;
        }

        public void setUpdatedAt(Date updatedAt) {
            this.updatedAt = updatedAt;
        }

        public Window getWindow() {
            return window;
        }

        public void setWindow(Window window) {
            this.window = window;
        }
    }

    /** An object inside an object kept as JSON, and one kept as JSON on its own. */
    public static class Window {
        private Integer startHour;
        private Integer endHour;

        public Window() {
        }

        Window(Integer startHour, Integer endHour) {
            this.startHour = startHour;
            this.endHour = endHour;
        }

        public Integer getStartHour() {
            return startHour;
        }

        public void setStartHour(Integer startHour) {
            this.startHour = startHour;
        }

        public Integer getEndHour() {
            return endHour;
        }

        public void setEndHour(Integer endHour) {
            this.endHour = endHour;
        }
    }

    /** One of a list of objects kept as JSON, the shape Notification's {@code Channels} has. */
    public static class Channel {
        private String name;
        private Boolean enabled;

        public Channel() {
        }

        Channel(String name, Boolean enabled) {
            this.name = name;
            this.enabled = enabled;
        }

        public String getName() {
            return name;
        }

        public void setName(String name) {
            this.name = name;
        }

        public Boolean getEnabled() {
            return enabled;
        }

        public void setEnabled(Boolean enabled) {
            this.enabled = enabled;
        }
    }

    /** One of a list of objects kept as JSON, with a field that must not be shown. */
    public static class Recipient {
        private String label;
        @AuditMask
        private String email;

        public Recipient() {
        }

        Recipient(String label, String email) {
            this.label = label;
            this.email = email;
        }

        public String getLabel() {
            return label;
        }

        public void setLabel(String label) {
            this.label = label;
        }

        public String getEmail() {
            return email;
        }

        public void setEmail(String email) {
            this.email = email;
        }
    }

    /** A record has no getters to read fields through. */
    public record Limits(int low, int high) {
    }

    /** A record with a component that must not be shown. */
    public record Access(String user, @AuditMask String secret) {
    }

    /** A field written to the JSON without a getter, which must not be shown. */
    public static class Vault {
        private String label;
        @AuditMask
        @com.fasterxml.jackson.annotation.JsonProperty
        private String pin;

        public Vault() {
        }

        Vault(String label, String pin) {
            this.label = label;
            this.pin = pin;
        }

        public String getLabel() {
            return label;
        }

        public void setLabel(String label) {
            this.label = label;
        }

        void changePin(String pin) {
            this.pin = pin;
        }
    }

    /** Neither has a class whose fields are public. */
    public static class Flags {
        public String mode;
        public boolean strict;
    }

    /** An object with one getter and one field that is written to the JSON without one. */
    public static class Half {
        private String seen;
        @com.fasterxml.jackson.annotation.JsonProperty
        private String unseen;

        public Half() {
        }

        Half(String seen, String unseen) {
            this.seen = seen;
            this.unseen = unseen;
        }

        public String getSeen() {
            return seen;
        }

        public void setSeen(String seen) {
            this.seen = seen;
        }

        void changeUnseen(String unseen) {
            this.unseen = unseen;
        }
    }

    @Embeddable
    public static class Extras {
        private String note;
        @JdbcTypeCode(SqlTypes.JSON)
        private Window quietHours;

        public Extras() {
        }

        Extras(String note, Window quietHours) {
            this.note = note;
            this.quietHours = quietHours;
        }

        public String getNote() {
            return note;
        }

        public void setNote(String note) {
            this.note = note;
        }

        public Window getQuietHours() {
            return quietHours;
        }

        public void setQuietHours(Window quietHours) {
            this.quietHours = quietHours;
        }
    }

    @Entity(name = "Plan")
    public static class Plan {
        @Id
        private Long id;
        private String title;
        @JdbcTypeCode(SqlTypes.JSON)
        private Schedule schedule;
        @JdbcTypeCode(SqlTypes.JSON)
        @AuditMask
        private Window sealed;
        @JdbcTypeCode(SqlTypes.JSON)
        private List<Channel> channels;
        @JdbcTypeCode(SqlTypes.JSON)
        private List<Recipient> recipients;
        @JdbcTypeCode(SqlTypes.JSON)
        private Limits limits;
        @JdbcTypeCode(SqlTypes.JSON)
        private Flags flags;
        @JdbcTypeCode(SqlTypes.JSON)
        private Half half;
        @JdbcTypeCode(SqlTypes.JSON)
        private Access access;
        @JdbcTypeCode(SqlTypes.JSON)
        private Vault vault;
        @JdbcTypeCode(SqlTypes.JSON)
        private String raw;
        @Embedded
        private Extras extras;

        public Plan() {
        }

        Plan(Long id, String title) {
            this.id = id;
            this.title = title;
        }

        public Long getId() {
            return id;
        }

        public void setId(Long id) {
            this.id = id;
        }

        public String getTitle() {
            return title;
        }

        public void setTitle(String title) {
            this.title = title;
        }

        public Schedule getSchedule() {
            return schedule;
        }

        public void setSchedule(Schedule schedule) {
            this.schedule = schedule;
        }

        public Window getSealed() {
            return sealed;
        }

        public void setSealed(Window sealed) {
            this.sealed = sealed;
        }

        public List<Channel> getChannels() {
            return channels;
        }

        public void setChannels(List<Channel> channels) {
            this.channels = channels;
        }

        public List<Recipient> getRecipients() {
            return recipients;
        }

        public void setRecipients(List<Recipient> recipients) {
            this.recipients = recipients;
        }

        public Limits getLimits() {
            return limits;
        }

        public void setLimits(Limits limits) {
            this.limits = limits;
        }

        public Flags getFlags() {
            return flags;
        }

        public void setFlags(Flags flags) {
            this.flags = flags;
        }

        public Half getHalf() {
            return half;
        }

        public void setHalf(Half half) {
            this.half = half;
        }

        public Access getAccess() {
            return access;
        }

        public void setAccess(Access access) {
            this.access = access;
        }

        public Vault getVault() {
            return vault;
        }

        public void setVault(Vault vault) {
            this.vault = vault;
        }

        public String getRaw() {
            return raw;
        }

        public void setRaw(String raw) {
            this.raw = raw;
        }

        public Extras getExtras() {
            return extras;
        }

        public void setExtras(Extras extras) {
            this.extras = extras;
        }
    }

    @BeforeAll
    public static void startHibernate() {
        sessionFactory = new Configuration()
                .addAnnotatedClass(Plan.class)
                .setProperty("hibernate.connection.driver_class", "org.h2.Driver")
                .setProperty("hibernate.connection.url", "jdbc:h2:mem:audit-json;DB_CLOSE_DELAY=-1")
                .setProperty("hibernate.hbm2ddl.auto", "create-drop")
                .buildSessionFactory();
        DefaultListableBeanFactory beans = new DefaultListableBeanFactory();
        beans.registerSingleton("entityManagerFactory", sessionFactory);
        new HibernateAuditRegistrar(beans.getBeanProvider(EntityManagerFactory.class),
                new HibernateAuditChangeListener(new AuditDiff(new AuditMasking(List.of()))))
                .afterSingletonsInstantiated();
    }

    @AfterAll
    public static void stopHibernate() {
        if (sessionFactory != null) {
            sessionFactory.close();
        }
    }

    @BeforeEach
    public void seed() {
        RequestContextHolder.resetRequestAttributes();
        inTransaction(session -> {
            session.createMutationQuery("delete from Plan").executeUpdate();

            Plan full = new Plan(1L, "Full");
            Schedule schedule = new Schedule("Daily", "AcuteCare");
            schedule.setToken("t-1");
            schedule.setUpdatedAt(new Date(1_000_000L));
            schedule.setWindow(new Window(1, 5));
            full.setSchedule(schedule);
            full.setSealed(new Window(2, 4));
            full.setChannels(new ArrayList<>(List.of(new Channel("Email", false), new Channel("Sms", false))));
            full.setRecipients(new ArrayList<>(List.of(new Recipient("Ada", "ada@example.com"))));
            full.setLimits(new Limits(1, 5));
            Flags flags = new Flags();
            flags.mode = "strict";
            full.setFlags(flags);
            full.setHalf(new Half("s-1", "u-1"));
            full.setAccess(new Access("ada", "s3cret"));
            full.setVault(new Vault("Main", "1234"));
            full.setRaw("{\"a\":1}");
            full.setExtras(new Extras("note", new Window(22, 6)));
            session.persist(full);

            session.persist(new Plan(2L, "Bare"));
        });
        request = new MockHttpServletRequest();
        RequestContextHolder.setRequestAttributes(new ServletRequestAttributes(request));
        AuditState.stored(request).captureChanges();
    }

    @AfterEach
    public void unbind() {
        RequestContextHolder.resetRequestAttributes();
    }

    private List<PropertyChange> captured() {
        return AuditState.from(request).changes();
    }

    /** Each change as {@code Entity.Property@row:old>new}, sorted, so a set of them reads at a glance. */
    private List<String> summary() {
        return captured().stream()
                .map(change -> change.entity() + "." + change.propertyName() + "@" + change.rowKey() + ":"
                        + change.initialPropertyValue() + ">" + change.newPropertyValue())
                .sorted().toList();
    }

    private PropertyChange single() {
        Assertions.assertEquals(1, captured().size(), () -> "expected one change but was " + summary());
        return captured().get(0);
    }

    private static void inTransaction(Consumer<Session> work) {
        try (Session session = sessionFactory.openSession()) {
            Transaction transaction = session.beginTransaction();
            try {
                work.accept(session);
                transaction.commit();
            } catch (RuntimeException e) {
                if (transaction.isActive()) {
                    transaction.rollback();
                }
                throw e;
            }
        }
    }

    @Test
    public void fieldOfAnObjectKeptAsJson_isReportedUnderTheObjectsClassAndTheOwningRow() {
        inTransaction(session -> session.find(Plan.class, 1L).getSchedule().setName("Nightly"));

        Assertions.assertEquals(List.of(new PropertyChange("Name", "Daily", "Nightly", "Schedule", "1")), captured());
    }

    @Test
    public void ownerAndObjectChangedInOneSave_areToldApartByTheEntity() {
        inTransaction(session -> {
            Plan plan = session.find(Plan.class, 1L);
            plan.setTitle("Renamed");
            plan.getSchedule().setName("Nightly");
        });

        Assertions.assertEquals(List.of("Plan.Title@1:Full>Renamed", "Schedule.Name@1:Daily>Nightly"), summary());
    }

    /** A row whose JSON columns were only loaded reports none of them, though no class here defines equals. */
    @Test
    public void objectsThatWereOnlyLoaded_areNotReported() {
        inTransaction(session -> session.find(Plan.class, 1L).setTitle("Renamed"));

        Assertions.assertEquals(List.of("Plan.Title@1:Full>Renamed"), summary());
    }

    @Test
    public void objectReplacedWithAnEqualOne_isNotReported() {
        inTransaction(session -> {
            Plan plan = session.find(Plan.class, 1L);
            Schedule same = new Schedule("Daily", "AcuteCare");
            same.setToken("t-1");
            same.setUpdatedAt(new Date(1_000_000L));
            same.setWindow(new Window(1, 5));
            plan.setSchedule(same);
            plan.setTitle("Renamed");
        });

        Assertions.assertEquals(List.of("Plan.Title@1:Full>Renamed"), summary());
    }

    @Test
    public void objectSetForTheFirstTime_readsEachFieldFromEmpty() {
        inTransaction(session -> session.find(Plan.class, 2L).setSchedule(new Schedule("New", "A", "B")));

        Assertions.assertEquals(List.of("Schedule.Daily@2:>[\"A\",\"B\"]", "Schedule.Name@2:>New"), summary());
    }

    @Test
    public void objectCleared_readsEachFieldAsEmpty() {
        inTransaction(session -> session.find(Plan.class, 1L).setSchedule(null));

        Assertions.assertEquals(
                List.of("Schedule.Daily@1:[\"AcuteCare\"]>",
                        "Schedule.Name@1:Daily>",
                        "Schedule.Token@1:" + AuditMasking.REDACTED + ">" + AuditMasking.REDACTED,
                        "Window.EndHour@1:5>",
                        "Window.StartHour@1:1>"),
                summary());
    }

    @Test
    public void maskedFieldOfAnObject_isReportedAsChangedButNotShown() {
        inTransaction(session -> session.find(Plan.class, 1L).getSchedule().setToken("t-2"));

        Assertions.assertEquals(
                List.of(new PropertyChange("Token", AuditMasking.REDACTED, AuditMasking.REDACTED, "Schedule", "1")),
                captured());
    }

    @Test
    public void columnMaskedAsAWhole_masksEveryFieldInside() {
        inTransaction(session -> session.find(Plan.class, 1L).getSealed().setEndHour(9));

        Assertions.assertEquals(
                List.of(new PropertyChange("EndHour", AuditMasking.REDACTED, AuditMasking.REDACTED, "Window", "1")),
                captured());
    }

    @Test
    public void timestampOfAnObject_isNotReported() {
        inTransaction(session -> {
            Schedule schedule = session.find(Plan.class, 1L).getSchedule();
            schedule.setUpdatedAt(new Date(2_000_000L));
            schedule.setName("Nightly");
        });

        Assertions.assertEquals(List.of("Schedule.Name@1:Daily>Nightly"), summary());
    }

    /** The JSON differs, but only by a value that is never reported, so the column is not reported whole either. */
    @Test
    public void timestampAloneChanging_reportsNothing() {
        inTransaction(session -> session.find(Plan.class, 1L).getSchedule().setUpdatedAt(new Date(2_000_000L)));

        Assertions.assertEquals(List.of(), summary());
    }

    @Test
    public void objectInsideAnObject_isReportedUnderItsOwnClass() {
        inTransaction(session -> session.find(Plan.class, 1L).getSchedule().getWindow().setEndHour(7));

        Assertions.assertEquals(List.of(new PropertyChange("EndHour", "5", "7", "Window", "1")), captured());
    }

    /** Tenant's shape in .NET: a schedule owned by a facility, holding lists of report names. */
    @Test
    public void listOfSimpleValuesInsideAnObject_isReportedAsAJsonArray() {
        inTransaction(session ->
                session.find(Plan.class, 1L).getSchedule().setDaily(new String[] {"AcuteCare", "HSLOC"}));

        Assertions.assertEquals(
                List.of(new PropertyChange("Daily", "[\"AcuteCare\"]", "[\"AcuteCare\",\"HSLOC\"]", "Schedule", "1")),
                captured());
    }

    /** Notification's shape in .NET: the row key is the owner's with the item's position, counted from one. */
    @Test
    public void fieldOfAnItemInAListOfObjects_isReportedUnderTheItemsClassAndPosition() {
        inTransaction(session -> session.find(Plan.class, 1L).getChannels().get(1).setEnabled(true));

        Assertions.assertEquals(List.of(new PropertyChange("Enabled", "false", "true", "Channel", "1|2")), captured());
    }

    @Test
    public void maskedFieldOfAnItem_isReportedAsChangedButNotShown() {
        inTransaction(session -> session.find(Plan.class, 1L).getRecipients().get(0).setEmail("grace@example.com"));

        Assertions.assertEquals(
                List.of(new PropertyChange("Email", AuditMasking.REDACTED, AuditMasking.REDACTED, "Recipient", "1|1")),
                captured());
    }

    /** Read by position, an added item would not show at all, so the list is reported as one value. */
    @Test
    public void itemAddedToAListOfObjects_isReportedAsTheWholeList() {
        inTransaction(session -> session.find(Plan.class, 1L).getChannels().add(new Channel("Pager", true)));

        PropertyChange change = single();
        Assertions.assertEquals("Channels", change.propertyName());
        Assertions.assertEquals("Plan", change.entity());
        Assertions.assertEquals("1", change.rowKey());
        Assertions.assertFalse(change.initialPropertyValue().contains("Pager"), change.initialPropertyValue());
        Assertions.assertTrue(change.newPropertyValue().contains("Pager"), change.newPropertyValue());
        Assertions.assertTrue(change.newPropertyValue().startsWith("[{"), change.newPropertyValue());
    }

    @Test
    public void itemRemovedFromAListOfObjects_isReportedAsTheWholeList() {
        inTransaction(session -> session.find(Plan.class, 1L).getChannels().remove(0));

        PropertyChange change = single();
        Assertions.assertEquals("Channels", change.propertyName());
        Assertions.assertTrue(change.initialPropertyValue().contains("Email"), change.initialPropertyValue());
        Assertions.assertFalse(change.newPropertyValue().contains("Email"), change.newPropertyValue());
    }

    /** One value would show the address that is masked on every other save, so the whole list is masked. */
    @Test
    public void itemAddedToAListWhoseItemsHaveAMaskedField_isMaskedWhole() {
        inTransaction(session ->
                session.find(Plan.class, 1L).getRecipients().add(new Recipient("Grace", "grace@example.com")));

        Assertions.assertEquals(
                List.of(new PropertyChange("Recipients", AuditMasking.REDACTED, AuditMasking.REDACTED, "Plan", "1")),
                captured());
    }

    @Test
    public void record_isReportedAsOneValue() {
        inTransaction(session -> session.find(Plan.class, 1L).setLimits(new Limits(1, 9)));

        PropertyChange change = single();
        Assertions.assertEquals("Limits", change.propertyName());
        Assertions.assertEquals("Plan", change.entity());
        Assertions.assertTrue(change.initialPropertyValue().contains("5"), change.initialPropertyValue());
        Assertions.assertTrue(change.newPropertyValue().contains("9"), change.newPropertyValue());
    }

    @Test
    public void classWithPublicFieldsAndNoGetters_isReportedAsOneValue() {
        inTransaction(session -> session.find(Plan.class, 1L).getFlags().mode = "lax");

        PropertyChange change = single();
        Assertions.assertEquals("Flags", change.propertyName());
        Assertions.assertEquals("Plan", change.entity());
        Assertions.assertTrue(change.newPropertyValue().contains("lax"), change.newPropertyValue());
    }

    /** A change no getter shows is still a change: the JSON differs, so the column is reported as one value. */
    @Test
    public void fieldWithNoGetterChangingAlone_isReportedAsOneValue() {
        inTransaction(session -> session.find(Plan.class, 1L).getHalf().changeUnseen("u-2"));

        PropertyChange change = single();
        Assertions.assertEquals("Half", change.propertyName());
        Assertions.assertEquals("Plan", change.entity());
        Assertions.assertTrue(change.newPropertyValue().contains("u-2"), change.newPropertyValue());
    }

    /** A record is shown as one value, which would show the component that is masked. */
    @Test
    public void recordWithAMaskedComponent_isMaskedWhole() {
        inTransaction(session -> session.find(Plan.class, 1L).setAccess(new Access("grace", "s3cret")));

        Assertions.assertEquals(
                List.of(new PropertyChange("Access", AuditMasking.REDACTED, AuditMasking.REDACTED, "Plan", "1")),
                captured());
    }

    /** The one value an unseen change falls back to would show the masked field, getter or no getter. */
    @Test
    public void maskedFieldWithNoGetterChangingAlone_isMaskedWhole() {
        inTransaction(session -> session.find(Plan.class, 1L).getVault().changePin("9999"));

        Assertions.assertEquals(
                List.of(new PropertyChange("Vault", AuditMasking.REDACTED, AuditMasking.REDACTED, "Plan", "1")),
                captured());
    }

    @Test
    public void textKeptInAJsonColumn_isReportedOnceAsItIs() {
        inTransaction(session -> session.find(Plan.class, 1L).setRaw("{\"a\":2}"));

        Assertions.assertEquals(
                List.of(new PropertyChange("Raw", "{\"a\":1}", "{\"a\":2}", "Plan", "1")), captured());
    }

    @Test
    public void objectKeptAsJsonInsideAnEmbeddedObject_isReportedFieldByField() {
        inTransaction(session -> session.find(Plan.class, 1L).getExtras().getQuietHours().setStartHour(23));

        Assertions.assertEquals(List.of(new PropertyChange("StartHour", "22", "23", "Window", "1")), captured());
    }
}
