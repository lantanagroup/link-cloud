package com.lantanagroup.link.shared.audit;

import jakarta.persistence.AttributeConverter;
import jakarta.persistence.Column;
import jakarta.persistence.Convert;
import jakarta.persistence.Entity;
import jakarta.persistence.EntityManagerFactory;
import jakarta.persistence.EnumType;
import jakarta.persistence.Enumerated;
import jakarta.persistence.FetchType;
import jakarta.persistence.Id;
import jakarta.persistence.JoinColumn;
import jakarta.persistence.ManyToOne;
import jakarta.persistence.OneToOne;
import jakarta.persistence.Version;
import org.hibernate.Hibernate;
import org.hibernate.Session;
import org.hibernate.SessionFactory;
import org.hibernate.Transaction;
import org.hibernate.annotations.JdbcTypeCode;
import org.hibernate.cfg.Configuration;
import org.hibernate.event.spi.PostUpdateEvent;
import org.hibernate.type.SqlTypes;
import org.junit.jupiter.api.AfterAll;
import org.junit.jupiter.api.AfterEach;
import org.junit.jupiter.api.Assertions;
import org.junit.jupiter.api.BeforeAll;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.Test;
import org.mockito.Mockito;
import org.springframework.beans.factory.support.DefaultListableBeanFactory;
import org.springframework.mock.web.MockHttpServletRequest;
import org.springframework.web.context.request.RequestContextHolder;
import org.springframework.web.context.request.ServletRequestAttributes;

import java.nio.charset.StandardCharsets;
import java.time.Instant;
import java.util.ArrayList;
import java.util.Arrays;
import java.util.Base64;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;
import java.util.function.Consumer;
import java.util.stream.Collectors;

/**
 * Runs {@link HibernateAuditChangeListener} against a real Hibernate session, attached the way a service
 * attaches it, so what is asserted is what Hibernate actually hands the listener and when.
 */
public class HibernateAuditChangeListenerTest {
    private static SessionFactory sessionFactory;

    private MockHttpServletRequest request;
    private ServletRequestAttributes attributes;

    public enum Status { DRAFT, ACTIVE }

    /** Keeps a list as one comma-separated column, the way a JSON converter keeps one as a document. */
    public static class TagsConverter implements AttributeConverter<List<String>, String> {
        @Override
        public String convertToDatabaseColumn(List<String> tags) {
            return tags == null ? null : String.join(",", tags);
        }

        @Override
        public List<String> convertToEntityAttribute(String column) {
            return column == null ? null : new ArrayList<>(Arrays.asList(column.split(",")));
        }
    }

    /** Keeps a simple value in a different stored form. */
    public static class YesNoConverter implements AttributeConverter<Boolean, String> {
        @Override
        public String convertToDatabaseColumn(Boolean flag) {
            return flag == null ? null : flag ? "Y" : "N";
        }

        @Override
        public Boolean convertToEntityAttribute(String column) {
            return column == null ? null : "Y".equals(column);
        }
    }

    /** Keeps a moment as a number. */
    public static class EpochConverter implements AttributeConverter<Instant, Long> {
        @Override
        public Long convertToDatabaseColumn(Instant moment) {
            return moment == null ? null : moment.toEpochMilli();
        }

        @Override
        public Instant convertToEntityAttribute(Long column) {
            return column == null ? null : Instant.ofEpochMilli(column);
        }
    }

    /** Keeps text as binary. */
    public static class BytesConverter implements AttributeConverter<String, byte[]> {
        @Override
        public byte[] convertToDatabaseColumn(String text) {
            return text == null ? null : text.getBytes(StandardCharsets.UTF_8);
        }

        @Override
        public String convertToEntityAttribute(byte[] column) {
            return column == null ? null : new String(column, StandardCharsets.UTF_8);
        }
    }

    @Entity(name = "Widget")
    public static class Widget {
        @Id
        private Long id;
        private String name;
        private int quantity;
        @Enumerated(EnumType.STRING)
        private Status status;
        private String apiKey;
        @AuditMask
        private String email;
        private Instant updatedAt;
        @Version
        private long revision;
        @ManyToOne(fetch = FetchType.LAZY)
        private Owner owner;
        @ManyToOne(fetch = FetchType.LAZY)
        @AuditMask
        private Owner sponsor;
        @Convert(converter = TagsConverter.class)
        private List<String> tags;
        @Convert(converter = TagsConverter.class)
        @AuditMask
        private List<String> recipients;
        @Convert(converter = YesNoConverter.class)
        private Boolean enabled;
        @Convert(converter = BytesConverter.class)
        private String signature;
        @Convert(converter = EpochConverter.class)
        private Instant lastUpdated;
        // Fetched with the widget, as a to-one link is unless it says otherwise.
        @ManyToOne
        private Owner reviewer;

        public Widget() {
        }

        Widget(Long id, String name) {
            this.id = id;
            this.name = name;
            this.quantity = 1;
            this.status = Status.DRAFT;
            this.apiKey = "key-1";
            this.email = "ada@example.com";
            this.updatedAt = Instant.parse("2026-01-01T00:00:00Z");
        }

        public Long getId() {
            return id;
        }

        public void setId(Long id) {
            this.id = id;
        }

        public String getName() {
            return name;
        }

        public void setName(String name) {
            this.name = name;
        }

        public int getQuantity() {
            return quantity;
        }

        public void setQuantity(int quantity) {
            this.quantity = quantity;
        }

        public Status getStatus() {
            return status;
        }

        public void setStatus(Status status) {
            this.status = status;
        }

        public String getApiKey() {
            return apiKey;
        }

        public void setApiKey(String apiKey) {
            this.apiKey = apiKey;
        }

        public String getEmail() {
            return email;
        }

        public void setEmail(String email) {
            this.email = email;
        }

        public Instant getUpdatedAt() {
            return updatedAt;
        }

        public void setUpdatedAt(Instant updatedAt) {
            this.updatedAt = updatedAt;
        }

        public long getRevision() {
            return revision;
        }

        public void setRevision(long revision) {
            this.revision = revision;
        }

        public Owner getOwner() {
            return owner;
        }

        public void setOwner(Owner owner) {
            this.owner = owner;
        }

        public Owner getSponsor() {
            return sponsor;
        }

        public void setSponsor(Owner sponsor) {
            this.sponsor = sponsor;
        }

        public List<String> getTags() {
            return tags;
        }

        public void setTags(List<String> tags) {
            this.tags = tags;
        }

        public List<String> getRecipients() {
            return recipients;
        }

        public void setRecipients(List<String> recipients) {
            this.recipients = recipients;
        }

        public Boolean getEnabled() {
            return enabled;
        }

        public void setEnabled(Boolean enabled) {
            this.enabled = enabled;
        }

        public String getSignature() {
            return signature;
        }

        public void setSignature(String signature) {
            this.signature = signature;
        }

        public Instant getLastUpdated() {
            return lastUpdated;
        }

        public void setLastUpdated(Instant lastUpdated) {
            this.lastUpdated = lastUpdated;
        }

        public Owner getReviewer() {
            return reviewer;
        }

        public void setReviewer(Owner reviewer) {
            this.reviewer = reviewer;
        }
    }

    /** Holds values the database stores in a form of its own, with no converter: a JSON column and binary ones. */
    @Entity(name = "Panel")
    public static class Panel {
        @Id
        private Long id;
        private String title;
        @JdbcTypeCode(SqlTypes.JSON)
        private Map<String, String> settings;
        private byte[] icon;
        @AuditMask
        private byte[] seal;

        public Panel() {
        }

        Panel(Long id) {
            this.id = id;
            this.title = "Panel";
            this.settings = new LinkedHashMap<>(Map.of("theme", "dark"));
            this.icon = new byte[] {1, 2, 3};
            this.seal = new byte[] {9, 9};
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

        public Map<String, String> getSettings() {
            return settings;
        }

        public void setSettings(Map<String, String> settings) {
            this.settings = settings;
        }

        public byte[] getIcon() {
            return icon;
        }

        public void setIcon(byte[] icon) {
            this.icon = icon;
        }

        public byte[] getSeal() {
            return seal;
        }

        public void setSeal(byte[] seal) {
            this.seal = seal;
        }
    }

    /** Holds the foreign key of a one-to-one link. */
    @Entity(name = "Badge")
    public static class Badge {
        @Id
        private Long id;
        @OneToOne(fetch = FetchType.LAZY)
        @JoinColumn(name = "holder_id")
        private Owner holder;

        public Badge() {
        }

        Badge(Long id, Owner holder) {
            this.id = id;
            this.holder = holder;
        }

        public Long getId() {
            return id;
        }

        public void setId(Long id) {
            this.id = id;
        }

        public Owner getHolder() {
            return holder;
        }

        public void setHolder(Owner holder) {
            this.holder = holder;
        }
    }

    /** The row a foreign key points at. */
    @Entity(name = "Owner")
    public static class Owner {
        @Id
        private Long id;
        private String name;

        public Owner() {
        }

        Owner(Long id, String name) {
            this.id = id;
            this.name = name;
        }

        public Long getId() {
            return id;
        }

        public void setId(Long id) {
            this.id = id;
        }

        public String getName() {
            return name;
        }

        public void setName(String name) {
            this.name = name;
        }
    }

    /**
     * Maps its foreign key twice, as the rubric entities of the Validation service do: a simple property that
     * writes the column and a read-only link to the related row.
     */
    @Entity(name = "Gadget")
    public static class Gadget {
        @Id
        private Long id;
        @Column(name = "maker_id")
        private Long makerId;
        @ManyToOne(fetch = FetchType.LAZY)
        @JoinColumn(name = "maker_id", insertable = false, updatable = false)
        private Owner maker;

        public Gadget() {
        }

        Gadget(Long id, Long makerId) {
            this.id = id;
            this.makerId = makerId;
        }

        public Long getId() {
            return id;
        }

        public void setId(Long id) {
            this.id = id;
        }

        public Long getMakerId() {
            return makerId;
        }

        public void setMakerId(Long makerId) {
            this.makerId = makerId;
        }

        public Owner getMaker() {
            return maker;
        }

        public void setMaker(Owner maker) {
            this.maker = maker;
        }
    }

    /** An entity whose id is itself a secret, so the row key must not show it. */
    @Entity(name = "Credential")
    public static class Credential {
        @Id
        private String token;
        private String label;

        public Credential() {
        }

        Credential(String token, String label) {
            this.token = token;
            this.label = label;
        }

        public String getToken() {
            return token;
        }

        public void setToken(String token) {
            this.token = token;
        }

        public String getLabel() {
            return label;
        }

        public void setLabel(String label) {
            this.label = label;
        }
    }

    @BeforeAll
    public static void startHibernate() {
        sessionFactory = new Configuration()
                .addAnnotatedClass(Widget.class)
                .addAnnotatedClass(Credential.class)
                .addAnnotatedClass(Owner.class)
                .addAnnotatedClass(Gadget.class)
                .addAnnotatedClass(Badge.class)
                .addAnnotatedClass(Panel.class)
                .setProperty("hibernate.connection.driver_class", "org.h2.Driver")
                .setProperty("hibernate.connection.url", "jdbc:h2:mem:audit-listener;DB_CLOSE_DELAY=-1")
                .setProperty("hibernate.hbm2ddl.auto", "create-drop")
                .buildSessionFactory();
        // Attached as a service attaches it: by the registrar, to every persistence unit among the beans.
        DefaultListableBeanFactory beans = new DefaultListableBeanFactory();
        beans.registerSingleton("entityManagerFactory", sessionFactory);
        new HibernateAuditRegistrar(beans.getBeanProvider(EntityManagerFactory.class), listener())
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
            session.createMutationQuery("delete from Widget").executeUpdate();
            session.createMutationQuery("delete from Gadget").executeUpdate();
            session.createMutationQuery("delete from Badge").executeUpdate();
            session.createMutationQuery("delete from Panel").executeUpdate();
            session.persist(new Panel(9L));
            session.createMutationQuery("delete from Credential").executeUpdate();
            session.createMutationQuery("delete from Owner").executeUpdate();
            Owner ten = new Owner(10L, "Ten");
            session.persist(ten);
            session.persist(new Owner(20L, "Twenty"));
            // Only ever a reviewer, so loading it with a widget leaves the rows the other links point at unloaded.
            Owner thirty = new Owner(30L, "Thirty");
            session.persist(thirty);
            session.persist(new Widget(1L, "First"));
            session.persist(new Widget(2L, "Second"));
            Widget owned = new Widget(5L, "Owned");
            owned.setOwner(ten);
            owned.setSponsor(ten);
            owned.setReviewer(thirty);
            session.persist(owned);
            session.persist(new Badge(8L, ten));
            Widget tagged = new Widget(6L, "Tagged");
            tagged.setTags(new ArrayList<>(List.of("a", "b")));
            tagged.setRecipients(new ArrayList<>(List.of("ada@example.com")));
            tagged.setEnabled(false);
            tagged.setSignature("sig-1");
            tagged.setLastUpdated(Instant.parse("2026-01-01T00:00:00Z"));
            session.persist(tagged);
            session.persist(new Gadget(7L, 10L));
            session.persist(new Credential("tok-1", "Primary"));
        });
    }

    @AfterEach
    public void unbind() {
        RequestContextHolder.resetRequestAttributes();
    }

    private static HibernateAuditChangeListener listener() {
        return new HibernateAuditChangeListener(new AuditDiff(new AuditMasking(List.of())));
    }

    /** Binds a request to the thread, as the servlet does, marked the way {@code preHandle} marks it or not. */
    private void serveRequest(boolean audited) {
        request = new MockHttpServletRequest();
        attributes = new ServletRequestAttributes(request);
        RequestContextHolder.setRequestAttributes(attributes);
        if (audited) {
            AuditState.stored(request).captureChanges();
        }
    }

    private List<PropertyChange> captured() {
        return AuditState.from(request).changes();
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

    private static Map<String, PropertyChange> byName(List<PropertyChange> changes) {
        return changes.stream().collect(Collectors.toMap(PropertyChange::propertyName, change -> change));
    }

    @Test
    public void committedUpdate_isCapturedWithTheEntityAndTheRow() {
        serveRequest(true);

        inTransaction(session -> {
            Widget widget = session.find(Widget.class, 1L);
            widget.setName("Renamed");
            widget.setQuantity(5);
            widget.setStatus(Status.ACTIVE);
        });

        Map<String, PropertyChange> changes = byName(captured());
        Assertions.assertEquals(List.of("Name", "Quantity", "Status"), changes.keySet().stream().sorted().toList());
        Assertions.assertEquals(new PropertyChange("Name", "First", "Renamed", "Widget", "1"), changes.get("Name"));
        Assertions.assertEquals(new PropertyChange("Quantity", "1", "5", "Widget", "1"), changes.get("Quantity"));
        Assertions.assertEquals(new PropertyChange("Status", "DRAFT", "ACTIVE", "Widget", "1"),
                changes.get("Status"));
    }

    @Test
    public void maskedProperties_areReportedAsChangedButNotShown() {
        serveRequest(true);

        inTransaction(session -> {
            Widget widget = session.find(Widget.class, 1L);
            widget.setApiKey("key-2");
            widget.setEmail("grace@example.com");
        });

        Map<String, PropertyChange> changes = byName(captured());
        Assertions.assertEquals(
                new PropertyChange("ApiKey", AuditMasking.REDACTED, AuditMasking.REDACTED, "Widget", "1"),
                changes.get("ApiKey"));
        Assertions.assertEquals(
                new PropertyChange("Email", AuditMasking.REDACTED, AuditMasking.REDACTED, "Widget", "1"),
                changes.get("Email"));
        Assertions.assertEquals(2, changes.size());
    }

    @Test
    public void versionAndTimestamp_areNotReported() {
        serveRequest(true);

        inTransaction(session -> {
            Widget widget = session.find(Widget.class, 1L);
            widget.setName("Renamed");
            widget.setUpdatedAt(Instant.parse("2026-02-02T00:00:00Z"));
        });

        // The update also moved the version on, and neither it nor the timestamp describes the change.
        Assertions.assertEquals(List.of("Name"), captured().stream().map(PropertyChange::propertyName).toList());
    }

    @Test
    public void mergeOfADetachedCopy_isCaptured() {
        serveRequest(true);
        Widget incoming = new Widget(1L, "Sent by the client");
        incoming.setRevision(0);

        inTransaction(session -> session.merge(incoming));

        Assertions.assertEquals(
                List.of(new PropertyChange("Name", "First", "Sent by the client", "Widget", "1")), captured());
    }

    @Test
    public void twoRows_areEachReportedUnderTheirOwnKey() {
        serveRequest(true);

        inTransaction(session -> {
            session.find(Widget.class, 1L).setName("One");
            session.find(Widget.class, 2L).setName("Two");
        });

        Assertions.assertEquals(
                List.of("1:One", "2:Two"),
                captured().stream().map(change -> change.rowKey() + ":" + change.newPropertyValue()).sorted().toList());
    }

    @Test
    public void maskedId_isNotShownAsTheRowKey() {
        serveRequest(true);

        inTransaction(session -> session.find(Credential.class, "tok-1").setLabel("Rotated"));

        Assertions.assertEquals(
                List.of(new PropertyChange("Label", "Primary", "Rotated", "Credential", AuditMasking.REDACTED)),
                captured());
    }

    @Test
    public void foreignKeyChange_isReportedAsTheRelatedRowsIdWithoutLoadingIt() {
        serveRequest(true);
        List<Owner> related = new ArrayList<>();

        inTransaction(session -> {
            Widget widget = session.find(Widget.class, 5L);
            related.add(widget.getOwner());
            related.add(session.getReference(Owner.class, 20L));
            widget.setOwner(related.get(1));
        });

        Assertions.assertEquals(List.of(new PropertyChange("OwnerId", "10", "20", "Widget", "5")), captured());
        // Neither related row was read from the database to learn its id.
        Assertions.assertFalse(Hibernate.isInitialized(related.get(0)));
        Assertions.assertFalse(Hibernate.isInitialized(related.get(1)));
    }

    @Test
    public void foreignKeyToLoadedRows_isReportedAsTheRelatedRowsId() {
        serveRequest(true);
        List<Owner> related = new ArrayList<>();

        inTransaction(session -> {
            Widget widget = session.find(Widget.class, 5L);
            related.add(widget.getReviewer());
            related.add(session.find(Owner.class, 20L));
            widget.setReviewer(related.get(1));
        });

        // Both sides are real, loaded entities here, not the unloaded references of the test above.
        Assertions.assertTrue(Hibernate.isInitialized(related.get(0)));
        Assertions.assertTrue(Hibernate.isInitialized(related.get(1)));
        Assertions.assertEquals(List.of(new PropertyChange("ReviewerId", "30", "20", "Widget", "5")), captured());
    }

    @Test
    public void foreignKeyOfAOneToOneLink_isReported() {
        serveRequest(true);

        inTransaction(session -> session.find(Badge.class, 8L).setHolder(session.getReference(Owner.class, 20L)));

        Assertions.assertEquals(List.of(new PropertyChange("HolderId", "10", "20", "Badge", "8")), captured());
    }

    @Test
    public void foreignKeyCleared_isReportedAsEmpty() {
        serveRequest(true);

        inTransaction(session -> session.find(Widget.class, 5L).setOwner(null));

        Assertions.assertEquals(List.of(new PropertyChange("OwnerId", "10", "", "Widget", "5")), captured());
    }

    @Test
    public void foreignKeySetForTheFirstTime_isReportedFromEmpty() {
        serveRequest(true);

        inTransaction(session -> session.find(Widget.class, 1L).setOwner(session.getReference(Owner.class, 20L)));

        Assertions.assertEquals(List.of(new PropertyChange("OwnerId", "", "20", "Widget", "1")), captured());
    }

    @Test
    public void unchangedForeignKey_isNotReported() {
        serveRequest(true);

        inTransaction(session -> session.find(Widget.class, 5L).setName("Still owned"));

        Assertions.assertEquals(List.of("Name"), captured().stream().map(PropertyChange::propertyName).toList());
    }

    @Test
    public void maskedForeignKey_isReportedAsChangedButNotShown() {
        serveRequest(true);

        inTransaction(session ->
                session.find(Widget.class, 5L).setSponsor(session.getReference(Owner.class, 20L)));

        Assertions.assertEquals(
                List.of(new PropertyChange("SponsorId", AuditMasking.REDACTED, AuditMasking.REDACTED, "Widget", "5")),
                captured());
    }

    @Test
    public void foreignKeyMappedAsASimplePropertyToo_isReportedOnce() {
        serveRequest(true);

        inTransaction(session -> session.find(Gadget.class, 7L).setMakerId(20L));

        Assertions.assertEquals(List.of(new PropertyChange("MakerId", "10", "20", "Gadget", "7")), captured());
    }

    @Test
    public void convertedColumn_isReportedAsOneValueInItsStoredForm() {
        serveRequest(true);

        inTransaction(session -> session.find(Widget.class, 6L).setTags(new ArrayList<>(List.of("a", "b", "c"))));

        Assertions.assertEquals(List.of(new PropertyChange("Tags", "a,b", "a,b,c", "Widget", "6")), captured());
    }

    @Test
    public void convertedColumnChangedInPlace_isReported() {
        serveRequest(true);

        inTransaction(session -> session.find(Widget.class, 6L).getTags().add("c"));

        Assertions.assertEquals(List.of(new PropertyChange("Tags", "a,b", "a,b,c", "Widget", "6")), captured());
    }

    @Test
    public void convertedColumnReplacedWithAnEqualValue_isNotReported() {
        serveRequest(true);

        inTransaction(session -> {
            Widget widget = session.find(Widget.class, 6L);
            widget.setTags(new ArrayList<>(List.of("a", "b")));
            widget.setName("Renamed");
        });

        Assertions.assertEquals(List.of("Name"), captured().stream().map(PropertyChange::propertyName).toList());
    }

    @Test
    public void convertedColumnSetAndCleared_readsAsEmptyOnTheNullSide() {
        serveRequest(true);

        inTransaction(session -> {
            session.find(Widget.class, 6L).setTags(null);
            session.find(Widget.class, 1L).setTags(new ArrayList<>(List.of("x")));
        });

        Assertions.assertEquals(
                List.of("1:Tags::x", "6:Tags:a,b:"),
                captured().stream()
                        .map(change -> change.rowKey() + ":" + change.propertyName() + ":"
                                + change.initialPropertyValue() + ":" + change.newPropertyValue())
                        .sorted().toList());
    }

    @Test
    public void convertedTimestamp_isStillNotReported() {
        serveRequest(true);

        inTransaction(session -> {
            Widget widget = session.find(Widget.class, 6L);
            widget.setLastUpdated(Instant.parse("2026-02-02T00:00:00Z"));
            widget.setName("Renamed");
        });

        // A timestamp is passed over by name; being written through a converter does not bring it back.
        Assertions.assertEquals(List.of("Name"), captured().stream().map(PropertyChange::propertyName).toList());
    }

    @Test
    public void convertedSimpleValue_isReportedInItsStoredForm() {
        serveRequest(true);

        inTransaction(session -> session.find(Widget.class, 6L).setEnabled(true));

        Assertions.assertEquals(List.of(new PropertyChange("Enabled", "N", "Y", "Widget", "6")), captured());
    }

    @Test
    public void convertedColumnStoredAsBinary_isReportedAsBase64() {
        serveRequest(true);

        inTransaction(session -> session.find(Widget.class, 6L).setSignature("sig-2"));

        Assertions.assertEquals(
                List.of(new PropertyChange("Signature", base64("sig-1"), base64("sig-2"), "Widget", "6")),
                captured());
    }

    @Test
    public void maskedConvertedColumn_isReportedAsChangedButNotShown() {
        serveRequest(true);

        inTransaction(session ->
                session.find(Widget.class, 6L).setRecipients(new ArrayList<>(List.of("grace@example.com"))));

        Assertions.assertEquals(
                List.of(new PropertyChange("Recipients", AuditMasking.REDACTED, AuditMasking.REDACTED, "Widget", "6")),
                captured());
    }

    @Test
    public void jsonColumn_isReportedAsOneValueInTheJsonItIsStoredAs() {
        serveRequest(true);

        inTransaction(session -> {
            Map<String, String> settings = new LinkedHashMap<>();
            settings.put("theme", "light");
            settings.put("density", "compact");
            session.find(Panel.class, 9L).setSettings(settings);
        });

        Assertions.assertEquals(
                List.of(new PropertyChange("Settings", "{\"theme\":\"dark\"}",
                        "{\"theme\":\"light\",\"density\":\"compact\"}", "Panel", "9")),
                captured());
    }

    @Test
    public void jsonColumnChangedInPlace_isReported() {
        serveRequest(true);

        inTransaction(session -> session.find(Panel.class, 9L).getSettings().put("theme", "light"));

        Assertions.assertEquals(
                List.of(new PropertyChange("Settings", "{\"theme\":\"dark\"}", "{\"theme\":\"light\"}", "Panel", "9")),
                captured());
    }

    @Test
    public void jsonColumnReplacedWithAnEqualValue_isNotReported() {
        serveRequest(true);

        inTransaction(session -> {
            Panel panel = session.find(Panel.class, 9L);
            panel.setSettings(new LinkedHashMap<>(Map.of("theme", "dark")));
            panel.setTitle("Renamed");
        });

        Assertions.assertEquals(List.of("Title"), captured().stream().map(PropertyChange::propertyName).toList());
    }

    @Test
    public void jsonColumnCleared_readsAsEmpty() {
        serveRequest(true);

        inTransaction(session -> session.find(Panel.class, 9L).setSettings(null));

        Assertions.assertEquals(
                List.of(new PropertyChange("Settings", "{\"theme\":\"dark\"}", "", "Panel", "9")), captured());
    }

    @Test
    public void binaryColumn_isReportedAsBase64() {
        serveRequest(true);

        inTransaction(session -> session.find(Panel.class, 9L).setIcon(new byte[] {4, 5, 6}));

        Assertions.assertEquals(List.of(new PropertyChange("Icon", "AQID", "BAUG", "Panel", "9")), captured());
    }

    @Test
    public void binaryColumnReplacedWithTheSameBytes_isNotReported() {
        serveRequest(true);

        inTransaction(session -> {
            Panel panel = session.find(Panel.class, 9L);
            panel.setIcon(new byte[] {1, 2, 3});
            panel.setTitle("Renamed");
        });

        Assertions.assertEquals(List.of("Title"), captured().stream().map(PropertyChange::propertyName).toList());
    }

    @Test
    public void maskedBinaryColumn_isReportedAsChangedButNotShown() {
        serveRequest(true);

        inTransaction(session -> session.find(Panel.class, 9L).setSeal(new byte[] {7}));

        Assertions.assertEquals(
                List.of(new PropertyChange("Seal", AuditMasking.REDACTED, AuditMasking.REDACTED, "Panel", "9")),
                captured());
    }

    private static String base64(String text) {
        return Base64.getEncoder().encodeToString(text.getBytes(StandardCharsets.UTF_8));
    }

    @Test
    public void rolledBackUpdate_isNotCaptured() {
        serveRequest(true);

        try (Session session = sessionFactory.openSession()) {
            Transaction transaction = session.beginTransaction();
            session.find(Widget.class, 1L).setName("Never kept");
            session.flush();
            transaction.rollback();
        }

        Assertions.assertTrue(captured().isEmpty());
    }

    @Test
    public void updateOutsideAnAuditedRequest_isNotCaptured() {
        serveRequest(false);

        inTransaction(session -> session.find(Widget.class, 1L).setName("Unaudited"));

        Assertions.assertTrue(captured().isEmpty());
        Assertions.assertNull(request.getAttribute(AuditState.ATTRIBUTE));
    }

    @Test
    public void updateWithNoRequestBound_isSavedAndNotCaptured() {
        inTransaction(session -> session.find(Widget.class, 1L).setName("From a listener"));

        inTransaction(session ->
                Assertions.assertEquals("From a listener", session.find(Widget.class, 1L).getName()));
    }

    @Test
    public void insertAndDelete_areNotCaptured() {
        serveRequest(true);

        inTransaction(session -> {
            session.persist(new Widget(3L, "Third"));
            session.remove(session.find(Widget.class, 2L));
        });

        Assertions.assertTrue(captured().isEmpty());
    }

    @Test
    public void updateMadeByAQuery_isNotCaptured() {
        serveRequest(true);

        inTransaction(session ->
                session.createMutationQuery("update Widget set name = 'Bulk' where id = 1").executeUpdate());

        // Hibernate never loaded the row, so it has no earlier values to report.
        Assertions.assertTrue(captured().isEmpty());
    }

    @Test
    public void requiresPostCommitHandling_onlyWhileAnAuditedRequestIsServed() {
        HibernateAuditChangeListener listener = listener();

        Assertions.assertFalse(listener.requiresPostCommitHandling(null));

        serveRequest(false);
        Assertions.assertFalse(listener.requiresPostCommitHandling(null));

        serveRequest(true);
        Assertions.assertTrue(listener.requiresPostCommitHandling(null));

        // A completed request can no longer be read; that must read as "no", not as an exception.
        attributes.requestCompleted();
        Assertions.assertFalse(listener.requiresPostCommitHandling(null));
    }

    @Test
    public void failureWhileReadingTheUpdate_isSwallowed() {
        serveRequest(true);
        PostUpdateEvent event = Mockito.mock(PostUpdateEvent.class);
        Mockito.when(event.getOldState()).thenThrow(new IllegalStateException("broken"));

        Assertions.assertDoesNotThrow(() -> listener().onPostUpdate(event));
        Assertions.assertTrue(captured().isEmpty());
    }

    @Test
    public void registrarWithNoPersistenceUnit_attachesNothing() {
        DefaultListableBeanFactory beans = new DefaultListableBeanFactory();

        Assertions.assertDoesNotThrow(() ->
                new HibernateAuditRegistrar(beans.getBeanProvider(EntityManagerFactory.class), listener())
                        .afterSingletonsInstantiated());
    }
}
