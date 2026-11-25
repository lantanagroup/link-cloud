package com.lantanagroup.link.shared.audit;

import com.lantanagroup.link.shared.audit.HibernateAuditChangeListenerTest.TagsConverter;
import jakarta.persistence.Convert;
import jakarta.persistence.Embeddable;
import jakarta.persistence.Embedded;
import jakarta.persistence.Entity;
import jakarta.persistence.EntityManagerFactory;
import jakarta.persistence.FetchType;
import jakarta.persistence.Id;
import jakarta.persistence.ManyToOne;
import org.hibernate.Hibernate;
import org.hibernate.Session;
import org.hibernate.SessionFactory;
import org.hibernate.Transaction;
import org.hibernate.annotations.JdbcTypeCode;
import org.hibernate.cfg.Configuration;
import org.hibernate.engine.spi.SessionFactoryImplementor;
import org.hibernate.type.BasicType;
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
import java.util.List;
import java.util.function.Consumer;

/**
 * Runs {@link HibernateAuditChangeListener} against a real Hibernate session for the two shapes that are not one
 * simple value in one column: an object embedded in a row, and an array or list of simple values.
 */
public class HibernateAuditEmbeddedAndArrayTest {
    private static SessionFactory sessionFactory;

    private MockHttpServletRequest request;

    @Entity(name = "Keeper")
    public static class Keeper {
        @Id
        private Long id;
        private String name;

        public Keeper() {
        }

        Keeper(Long id, String name) {
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

    /** An object embedded in a station's row, holding one of every kind of property a row can. */
    @Embeddable
    public static class Address {
        private String street;
        private String city;
        // Written when the row is created and never by an update.
        @jakarta.persistence.Column(updatable = false)
        private String region;
        @AuditMask
        private String phone;
        @Convert(converter = TagsConverter.class)
        private List<String> zones;
        @ManyToOne(fetch = FetchType.LAZY)
        private Keeper keeper;
        @Embedded
        private GeoPoint point;
        private String[] gates;

        public Address() {
        }

        Address(String street, String city) {
            this.street = street;
            this.city = city;
        }

        public String getRegion() {
            return region;
        }

        public void setRegion(String region) {
            this.region = region;
        }

        public String getStreet() {
            return street;
        }

        public void setStreet(String street) {
            this.street = street;
        }

        public String getCity() {
            return city;
        }

        public void setCity(String city) {
            this.city = city;
        }

        public String getPhone() {
            return phone;
        }

        public void setPhone(String phone) {
            this.phone = phone;
        }

        public List<String> getZones() {
            return zones;
        }

        public void setZones(List<String> zones) {
            this.zones = zones;
        }

        public Keeper getKeeper() {
            return keeper;
        }

        public void setKeeper(Keeper keeper) {
            this.keeper = keeper;
        }

        public GeoPoint getPoint() {
            return point;
        }

        public void setPoint(GeoPoint point) {
            this.point = point;
        }

        public String[] getGates() {
            return gates;
        }

        public void setGates(String[] gates) {
            this.gates = gates;
        }
    }

    /** An object embedded in an embedded object. */
    @Embeddable
    public static class GeoPoint {
        private Double latitude;
        private Double longitude;

        public GeoPoint() {
        }

        GeoPoint(Double latitude, Double longitude) {
            this.latitude = latitude;
            this.longitude = longitude;
        }

        public Double getLatitude() {
            return latitude;
        }

        public void setLatitude(Double latitude) {
            this.latitude = latitude;
        }

        public Double getLongitude() {
            return longitude;
        }

        public void setLongitude(Double longitude) {
            this.longitude = longitude;
        }
    }

    /** An embedded object none of whose own fields is masked; the station masks it as a whole. */
    @Embeddable
    public static class Contact {
        private String person;
        private String email;

        public Contact() {
        }

        Contact(String person, String email) {
            this.person = person;
            this.email = email;
        }

        public String getPerson() {
            return person;
        }

        public void setPerson(String person) {
            this.person = person;
        }

        public String getEmail() {
            return email;
        }

        public void setEmail(String email) {
            this.email = email;
        }
    }

    @Entity(name = "Station")
    public static class Station {
        @Id
        private Long id;
        private String name;
        private String[] codes;
        private Integer[] limits;
        @AuditMask
        private String[] secrets;
        @JdbcTypeCode(SqlTypes.ARRAY)
        private List<String> labels;
        @Embedded
        private Address address;
        @Embedded
        @AuditMask
        private Contact contact;

        public Station() {
        }

        Station(Long id, String name) {
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

        public String[] getCodes() {
            return codes;
        }

        public void setCodes(String[] codes) {
            this.codes = codes;
        }

        public Integer[] getLimits() {
            return limits;
        }

        public void setLimits(Integer[] limits) {
            this.limits = limits;
        }

        public String[] getSecrets() {
            return secrets;
        }

        public void setSecrets(String[] secrets) {
            this.secrets = secrets;
        }

        public List<String> getLabels() {
            return labels;
        }

        public void setLabels(List<String> labels) {
            this.labels = labels;
        }

        public Address getAddress() {
            return address;
        }

        public void setAddress(Address address) {
            this.address = address;
        }

        public Contact getContact() {
            return contact;
        }

        public void setContact(Contact contact) {
            this.contact = contact;
        }
    }

    /** A row with an array, for the database that has no array type of its own. */
    @Entity(name = "Crate")
    public static class Crate {
        @Id
        private Long id;
        private String[] codes;

        public Crate() {
        }

        Crate(Long id, String[] codes) {
            this.id = id;
            this.codes = codes;
        }

        public Long getId() {
            return id;
        }

        public void setId(Long id) {
            this.id = id;
        }

        public String[] getCodes() {
            return codes;
        }

        public void setCodes(String[] codes) {
            this.codes = codes;
        }
    }

    @BeforeAll
    public static void startHibernate() {
        sessionFactory = start(new Configuration()
                .addAnnotatedClass(Keeper.class)
                .addAnnotatedClass(Station.class)
                .setProperty("hibernate.connection.url", "jdbc:h2:mem:audit-embedded;DB_CLOSE_DELAY=-1"));
    }

    @AfterAll
    public static void stopHibernate() {
        if (sessionFactory != null) {
            sessionFactory.close();
        }
    }

    /** Starts a persistence unit with the listener attached the way a service attaches it. */
    private static SessionFactory start(Configuration configuration) {
        SessionFactory factory = configuration
                .setProperty("hibernate.connection.driver_class", "org.h2.Driver")
                .setProperty("hibernate.hbm2ddl.auto", "create-drop")
                .buildSessionFactory();
        DefaultListableBeanFactory beans = new DefaultListableBeanFactory();
        beans.registerSingleton("entityManagerFactory", factory);
        new HibernateAuditRegistrar(beans.getBeanProvider(EntityManagerFactory.class),
                new HibernateAuditChangeListener(new AuditDiff(new AuditMasking(List.of()))))
                .afterSingletonsInstantiated();
        return factory;
    }

    @BeforeEach
    public void seed() {
        RequestContextHolder.resetRequestAttributes();
        inTransaction(sessionFactory, session -> {
            session.createMutationQuery("delete from Station").executeUpdate();
            session.createMutationQuery("delete from Keeper").executeUpdate();
            Keeper ten = new Keeper(10L, "Ten");
            session.persist(ten);
            session.persist(new Keeper(20L, "Twenty"));

            Station full = new Station(1L, "Full");
            full.setCodes(new String[] {"A", "B"});
            full.setLimits(new Integer[] {1, 2});
            full.setSecrets(new String[] {"s1"});
            full.setLabels(new ArrayList<>(List.of("north", "east")));
            Address address = new Address("1 High St", "York");
            address.setPhone("555-0100");
            address.setZones(new ArrayList<>(List.of("a", "b")));
            address.setKeeper(ten);
            address.setPoint(new GeoPoint(1.5, 2.5));
            address.setGates(new String[] {"G1"});
            full.setAddress(address);
            full.setContact(new Contact("Ada", "ada@example.com"));
            session.persist(full);

            session.persist(new Station(2L, "Bare"));
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

    private static void inTransaction(SessionFactory factory, Consumer<Session> work) {
        try (Session session = factory.openSession()) {
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

    private static void inTransaction(Consumer<Session> work) {
        inTransaction(sessionFactory, work);
    }

    /** The JDBC type Hibernate keeps a property in. */
    private static int storedAs(SessionFactory factory, Class<?> entity, String property) {
        BasicType<?> type = (BasicType<?>) factory.unwrap(SessionFactoryImplementor.class).getMappingMetamodel()
                .getEntityDescriptor(entity).getPropertyType(property);
        return type.getJdbcType().getDefaultSqlTypeCode();
    }

    @Test
    public void embeddedField_isReportedUnderTheEmbeddedClassAndTheOwningRow() {
        inTransaction(session -> session.find(Station.class, 1L).getAddress().setCity("Leeds"));

        Assertions.assertEquals(List.of(new PropertyChange("City", "York", "Leeds", "Address", "1")), captured());
    }

    /** A column of an embedded object that the row does not write is not a change, whatever the object holds. */
    @Test
    public void embeddedColumnTheUpdateDoesNotWrite_isNotReported_besideOneThatIs() {
        inTransaction(session -> {
            Address address = session.find(Station.class, 1L).getAddress();
            address.setRegion("North");
            address.setCity("Leeds");
        });

        Assertions.assertEquals(List.of(new PropertyChange("City", "York", "Leeds", "Address", "1")), captured());
        inTransaction(session -> Assertions.assertNull(
                session.find(Station.class, 1L).getAddress().getRegion(), "the database kept its value"));
    }

    @Test
    public void ownerAndEmbeddedObjectChangedInOneSave_areToldApartByTheEntity() {
        inTransaction(session -> {
            Station station = session.find(Station.class, 1L);
            station.setName("Renamed");
            station.getAddress().setStreet("2 Low Rd");
        });

        Assertions.assertEquals(
                List.of("Address.Street@1:1 High St>2 Low Rd", "Station.Name@1:Full>Renamed"), summary());
    }

    @Test
    public void embeddedObjectReplacedWithAnEqualOne_isNotReported() {
        inTransaction(session -> {
            Station station = session.find(Station.class, 1L);
            Address old = station.getAddress();
            Address same = new Address(old.getStreet(), old.getCity());
            same.setPhone(old.getPhone());
            same.setZones(new ArrayList<>(old.getZones()));
            same.setKeeper(old.getKeeper());
            same.setPoint(new GeoPoint(old.getPoint().getLatitude(), old.getPoint().getLongitude()));
            same.setGates(old.getGates().clone());
            station.setAddress(same);
            station.setName("Renamed");
        });

        Assertions.assertEquals(List.of("Station.Name@1:Full>Renamed"), summary());
    }

    @Test
    public void embeddedObjectSetForTheFirstTime_readsEachFieldFromEmpty() {
        inTransaction(session -> session.find(Station.class, 2L).setAddress(new Address("9 New St", "Hull")));

        Assertions.assertEquals(List.of("Address.City@2:>Hull", "Address.Street@2:>9 New St"), summary());
    }

    @Test
    public void embeddedObjectCleared_readsEachFieldAsEmpty() {
        inTransaction(session -> session.find(Station.class, 1L).setAddress(null));

        Assertions.assertEquals(
                List.of("Address.City@1:York>",
                        "Address.Gates@1:[\"G1\"]>",
                        "Address.KeeperId@1:10>",
                        "Address.Phone@1:" + AuditMasking.REDACTED + ">" + AuditMasking.REDACTED,
                        "Address.Street@1:1 High St>",
                        "Address.Zones@1:a,b>",
                        "GeoPoint.Latitude@1:1.5>",
                        "GeoPoint.Longitude@1:2.5>"),
                summary());
    }

    @Test
    public void maskedFieldOfAnEmbeddedObject_isReportedAsChangedButNotShown() {
        inTransaction(session -> session.find(Station.class, 1L).getAddress().setPhone("555-0199"));

        Assertions.assertEquals(
                List.of(new PropertyChange("Phone", AuditMasking.REDACTED, AuditMasking.REDACTED, "Address", "1")),
                captured());
    }

    @Test
    public void embeddedObjectMaskedAsAWhole_masksEveryFieldInside() {
        inTransaction(session -> {
            Contact contact = session.find(Station.class, 1L).getContact();
            contact.setPerson("Grace");
            contact.setEmail("grace@example.com");
        });

        Assertions.assertEquals(
                List.of("Contact.Email@1:" + AuditMasking.REDACTED + ">" + AuditMasking.REDACTED,
                        "Contact.Person@1:" + AuditMasking.REDACTED + ">" + AuditMasking.REDACTED),
                summary());
    }

    @Test
    public void converterColumnAndForeignKeyOfAnEmbeddedObject_areReportedAsTheyAreOnARow() {
        List<Keeper> related = new ArrayList<>();

        inTransaction(session -> {
            Address address = session.find(Station.class, 1L).getAddress();
            address.getZones().add("c");
            related.add(session.getReference(Keeper.class, 20L));
            address.setKeeper(related.get(0));
        });

        Assertions.assertFalse(Hibernate.isInitialized(related.get(0)));
        Assertions.assertEquals(List.of("Address.KeeperId@1:10>20", "Address.Zones@1:a,b>a,b,c"), summary());
    }

    @Test
    public void objectEmbeddedInAnEmbeddedObject_isReportedUnderItsOwnClass() {
        inTransaction(session -> session.find(Station.class, 1L).getAddress().getPoint().setLatitude(3.25));

        Assertions.assertEquals(List.of(new PropertyChange("Latitude", "1.5", "3.25", "GeoPoint", "1")), captured());
    }

    @Test
    public void array_isReportedAsAJsonArrayOfItsElements() {
        Assertions.assertEquals(SqlTypes.ARRAY, storedAs(sessionFactory, Station.class, "codes"));

        inTransaction(session -> session.find(Station.class, 1L).setCodes(new String[] {"A", "C", "D"}));

        Assertions.assertEquals(
                List.of(new PropertyChange("Codes", "[\"A\",\"B\"]", "[\"A\",\"C\",\"D\"]", "Station", "1")),
                captured());
    }

    /** The shape Tenant has in .NET: a schedule owned by a facility, holding lists of report names. */
    @Test
    public void arrayOfAnEmbeddedObject_isReportedAsAJsonArrayUnderTheEmbeddedClass() {
        inTransaction(session -> session.find(Station.class, 1L).getAddress().setGates(new String[] {"G1", "G2"}));

        Assertions.assertEquals(
                List.of(new PropertyChange("Gates", "[\"G1\"]", "[\"G1\",\"G2\"]", "Address", "1")), captured());
    }

    @Test
    public void arrayChangedInPlace_isReported() {
        inTransaction(session -> session.find(Station.class, 1L).getCodes()[1] = "Z");

        Assertions.assertEquals(
                List.of(new PropertyChange("Codes", "[\"A\",\"B\"]", "[\"A\",\"Z\"]", "Station", "1")), captured());
    }

    @Test
    public void arrayReplacedWithAnEqualOne_isNotReported() {
        inTransaction(session -> {
            Station station = session.find(Station.class, 1L);
            station.setCodes(new String[] {"A", "B"});
            station.setName("Renamed");
        });

        Assertions.assertEquals(List.of("Station.Name@1:Full>Renamed"), summary());
    }

    @Test
    public void arrayOfNumbers_readsThemBare() {
        inTransaction(session -> session.find(Station.class, 1L).setLimits(new Integer[] {1, 2, 30}));

        Assertions.assertEquals(List.of(new PropertyChange("Limits", "[1,2]", "[1,2,30]", "Station", "1")), captured());
    }

    @Test
    public void arraySetAndCleared_readsAsEmptyOnTheAbsentSide() {
        inTransaction(session -> {
            session.find(Station.class, 1L).setCodes(null);
            session.find(Station.class, 2L).setCodes(new String[] {"X"});
        });

        Assertions.assertEquals(
                List.of("Station.Codes@1:[\"A\",\"B\"]>", "Station.Codes@2:>[\"X\"]"), summary());
    }

    @Test
    public void maskedArray_isReportedAsChangedButNotShown() {
        inTransaction(session -> session.find(Station.class, 1L).setSecrets(new String[] {"s2"}));

        Assertions.assertEquals(
                List.of(new PropertyChange("Secrets", AuditMasking.REDACTED, AuditMasking.REDACTED, "Station", "1")),
                captured());
    }

    @Test
    public void listKeptInOneColumn_isReportedAsAJsonArrayOfItsElements() {
        inTransaction(session -> session.find(Station.class, 1L).getLabels().add("so\"uth"));

        Assertions.assertEquals(
                List.of(new PropertyChange("Labels", "[\"north\",\"east\"]", "[\"north\",\"east\",\"so\\\"uth\"]",
                        "Station", "1")),
                captured());
    }

    /**
     * SQL Server has no array type, so there Hibernate keeps an array as binary. H2 has one, and is told here
     * to do as SQL Server does, so what is asserted is the shape the services actually run with.
     */
    @Test
    public void arrayKeptAsBinary_isStillReportedAsAJsonArrayOfItsElements() {
        SessionFactory binary = start(new Configuration()
                .addAnnotatedClass(Crate.class)
                .setProperty("hibernate.type.preferred_array_jdbc_type", "VARBINARY")
                .setProperty("hibernate.connection.url", "jdbc:h2:mem:audit-array-binary;DB_CLOSE_DELAY=-1"));
        try {
            Assertions.assertEquals(SqlTypes.VARBINARY, storedAs(binary, Crate.class, "codes"));
            RequestContextHolder.resetRequestAttributes();
            inTransaction(binary, session -> session.persist(new Crate(1L, new String[] {"A", "B"})));
            RequestContextHolder.setRequestAttributes(new ServletRequestAttributes(request));

            inTransaction(binary, session -> session.find(Crate.class, 1L).getCodes()[0] = "Z");

            Assertions.assertEquals(
                    List.of(new PropertyChange("Codes", "[\"A\",\"B\"]", "[\"Z\",\"B\"]", "Crate", "1")), captured());
        } finally {
            binary.close();
        }
    }

    @Test
    public void jsonArray_writesStringsEscapedAndEverythingElseAsItsText() {
        Assertions.assertEquals("[\"a\\\\b\",\"line\\nbreak\",null,true,1.5,\"DRAFT\"]",
                AuditValues.jsonArray(new Object[] {"a\\b", "line\nbreak", null, true, 1.5,
                        HibernateAuditChangeListenerTest.Status.DRAFT}));
        Assertions.assertEquals("[[1,2],[3]]", AuditValues.jsonArray(List.of(new int[] {1, 2}, List.of(3))));
        Assertions.assertNull(AuditValues.jsonArray(null));
    }
}
