package com.lantanagroup.link.shared.audit;

import jakarta.persistence.PersistenceUnitUtil;
import org.hibernate.bytecode.enhance.spi.LazyPropertyInitializer;
import org.hibernate.event.spi.PostCommitUpdateEventListener;
import org.hibernate.event.spi.PostUpdateEvent;
import org.hibernate.metamodel.mapping.AttributeMapping;
import org.hibernate.metamodel.mapping.EmbeddableMappingType;
import org.hibernate.persister.entity.EntityPersister;
import org.hibernate.type.BasicType;
import org.hibernate.type.ComponentType;
import org.hibernate.type.ConvertedBasicType;
import org.hibernate.type.ManyToOneType;
import org.hibernate.type.SqlTypes;
import org.hibernate.type.Type;
import org.hibernate.type.descriptor.WrapperOptions;
import org.hibernate.type.descriptor.converter.spi.BasicValueConverter;
import org.hibernate.type.descriptor.converter.spi.JpaAttributeConverter;
import org.hibernate.type.descriptor.java.JavaType;
import org.hibernate.type.format.FormatMapper;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;

import java.util.ArrayList;
import java.util.HashSet;
import java.util.List;
import java.util.Objects;
import java.util.Set;

/**
 * Captures what an update actually changed, as old and new value pairs, by listening to Hibernate rather than
 * asking the handler. It is the counterpart of the .NET {@code AuditPropertyChangeInterceptor}: Hibernate already
 * knows which values differ, so this reads them there, where a handler would describe what it meant to change.
 * <p>
 * Hibernate calls it for an updated row only once the row's transaction has committed, so a change that was
 * rolled back is never reported. It collects only while the thread is serving a request whose handler is marked
 * {@link Auditable} with property changes on; a save made by a Kafka listener or a job is not watched at all.
 * It alters nothing and blocks nothing: both entry points catch everything, because one runs inside the flush
 * and the other after the commit, and a throw from either would fail a write the user made.
 * <p>
 * Some properties are reported as they are in .NET rather than passed over:
 * <ul>
 * <li>A foreign key: when a row is pointed at a different related row, the change is the related row's id,
 * under the property's name with {@code Id} appended.</li>
 * <li>A column whose stored form differs from its Java value: the change is the value in the form the database
 * stores, as one property. That covers a column written through an {@code AttributeConverter}, and a binary
 * column, which reads as base64.</li>
 * <li>A JSON column: an object kept in one reads field by field, each changed field a change of its own that
 * names the object's class as its entity, the way .NET reports an object it owns and stores as JSON. Anything
 * else kept in one reads as one value, the JSON Hibernate writes.</li>
 * <li>An array or a list of simple values kept in one column: the change is a JSON array of the elements,
 * whatever form the database keeps it in.</li>
 * <li>An embedded object: each field of it that changed is a change of its own, naming the embedded class as
 * its entity and the owning row as its row, the way .NET reports an owned type.</li>
 * </ul>
 * What it does not see: an update made by a query (JPQL or native) rather than through a loaded entity, a row
 * whose earlier values Hibernate never loaded, and collections kept in a table of their own. A column the
 * update does not write ({@code updatable = false}), on the row or in an embedded object, is passed over: the
 * database kept its value, whatever the object was given.
 */
final class HibernateAuditChangeListener implements PostCommitUpdateEventListener {
    private static final Logger logger = LoggerFactory.getLogger(HibernateAuditChangeListener.class);

    private final AuditDiff diff;

    HibernateAuditChangeListener(AuditDiff diff) {
        this.diff = diff;
    }

    /**
     * Asked during the flush, for every update on every thread. Saying yes makes Hibernate keep the row's
     * values until the transaction ends, so it says yes only for the saves of an audited request.
     */
    @Override
    public boolean requiresPostCommitHandling(EntityPersister persister) {
        return capturing() != null;
    }

    /** Called once the transaction that updated the row has committed. */
    @Override
    public void onPostUpdate(PostUpdateEvent event) {
        try {
            AuditState state = capturing();
            if (state == null) {
                return;
            }
            if (event.getOldState() == null || event.getState() == null) {
                // The row was updated without its earlier values being loaded, so there is nothing to compare.
                return;
            }
            // Copies: some values are rewritten below, and the arrays Hibernate holds must stay as they are.
            Object[] before = event.getOldState().clone();
            Object[] after = event.getState().clone();
            EntityPersister persister = event.getPersister();
            Class<?> type = persister.getMappedClass();
            String idProperty = persister.getIdentifierPropertyName();
            Set<Integer> skipped = unfetched(before, after);
            // The version changes on every update.
            if (persister.isVersioned()) {
                skipped.add(persister.getVersionProperty());
            }
            // What the row does not write was not stored, whatever value the object was given.
            boolean[] updatable = persister.getPropertyUpdateability();
            for (int i = 0; updatable != null && i < updatable.length; i++) {
                if (!updatable[i]) {
                    skipped.add(i);
                }
            }
            state.changes().addAll(changes(event, diff.rowKey(type, idProperty, event.getId()), type, idProperty,
                    persister.getPropertyNames(), persister.getPropertyTypes(), persister.getPropertyUpdateability(),
                    before, after, skipped, false));
        } catch (RuntimeException e) {
            logger.warn("Audit property capture failed; the audit event will carry no changes for this row: {}",
                    e.getClass().getName());
        }
    }

    /** The transaction rolled back: the database kept nothing, so there is nothing to report. */
    @Override
    public void onPostUpdateCommitFailed(PostUpdateEvent event) {
    }

    /** The state of the audited request this thread is serving, or null. Never throws. */
    private static AuditState capturing() {
        try {
            AuditState state = AuditState.current();
            return state != null && state.capturingChanges() ? state : null;
        } catch (RuntimeException e) {
            return null;
        }
    }

    /**
     * The changes of one set of properties: a row's own, or those of an object embedded in it, which is read
     * the same way one level down. Every change carries the row's key; the changes of an embedded object name
     * its class as their entity, so a reader can tell them from the row's own.
     *
     * @param type       the class the properties belong to: the entity, or the embedded class
     * @param idProperty the id property, for the entity; null for an embedded object, which has none
     * @param updatable  which properties the row writes, or null where all of them are
     * @param skipped    positions to pass over
     * @param masked     whether every value is masked, because the embedded object as a whole is
     */
    private List<PropertyChange> changes(PostUpdateEvent event, String rowKey, Class<?> type, String idProperty,
                                         String[] names, Type[] types, boolean[] updatable, Object[] before,
                                         Object[] after, Set<Integer> skipped, boolean masked) {
        Set<Integer> foreignKeys = foreignKeys(types, updatable, event.getFactory().getPersistenceUnitUtil(),
                before, after, skipped);
        List<PropertyChange> changes = jsonColumns(event, rowKey, type, names, types, before, after, skipped, masked);
        Set<Integer> stored = stored(types, before, after, skipped);
        changes.addAll(diff.rowChanges(type, idProperty, rowKey, names, before, after, skipped, foreignKeys, stored,
                masked));
        for (int i = 0; i < types.length && i < names.length && i < before.length && i < after.length; i++) {
            if (skipped.contains(i) || !(types[i] instanceof ComponentType embedded)
                    || before[i] == after[i] || names[i] == null) {
                continue;
            }
            try {
                if (diff.isPassedOver(type, names[i])) {
                    continue;
                }
                Object[] innerBefore = values(embedded, before[i]);
                Object[] innerAfter = values(embedded, after[i]);
                Set<Integer> innerSkipped = unfetched(innerBefore, innerAfter);
                innerSkipped.addAll(notWritten(embedded));
                changes.addAll(changes(event, rowKey, embedded.getReturnedClass(), null, embedded.getPropertyNames(),
                        embedded.getSubtypes(), null, innerBefore, innerAfter, innerSkipped,
                        masked || diff.isMasked(type, names[i])));
            } catch (RuntimeException e) {
                // This embedded object is left out; the rest of the row is still reported.
                logger.debug("Audit property capture skipped {} of {}: {}", names[i], type.getName(),
                        e.getClass().getName());
            }
        }
        return changes;
    }

    /** The values of an embedded object's properties. One that is absent has no value for any of them. */
    private static Object[] values(ComponentType embedded, Object value) {
        return value == null ? new Object[embedded.getSubtypes().length] : embedded.getPropertyValues(value);
    }

    /**
     * The positions of an embedded object's properties that an update does not write, which the row's own
     * updatability does not say: the embedded object counts as written when any column of it is.
     */
    private static Set<Integer> notWritten(ComponentType embedded) {
        Set<Integer> positions = new HashSet<>();
        try {
            EmbeddableMappingType mapping = embedded.getMappingModelPart().getEmbeddableTypeDescriptor();
            String[] names = embedded.getPropertyNames();
            for (int i = 0; i < names.length; i++) {
                AttributeMapping attribute = mapping.findAttributeMapping(names[i]);
                if (attribute != null && !attribute.getAttributeMetadata().isUpdatable()) {
                    positions.add(i);
                }
            }
        } catch (RuntimeException e) {
            // It cannot be told which are written, so each is compared: a change is not left out on a guess.
            positions.clear();
        }
        return positions;
    }

    /** The positions of any lazy property that was never fetched. */
    private static Set<Integer> unfetched(Object[] before, Object[] after) {
        Set<Integer> unfetched = new HashSet<>();
        for (int i = 0; i < before.length && i < after.length; i++) {
            if (before[i] == LazyPropertyInitializer.UNFETCHED_PROPERTY
                    || after[i] == LazyPropertyInitializer.UNFETCHED_PROPERTY) {
                unfetched.add(i);
            }
        }
        return unfetched;
    }

    /**
     * Finds the properties that are a foreign key of this row and replaces their values, which Hibernate holds
     * as the related entity, with that entity's id. Asking for the id does not load a related row that was
     * never fetched. A link the row does not write itself is left out: one that is read-only because a simple
     * property maps the same column, and one that points at something other than the related row's id.
     * A property whose id cannot be read is skipped rather than reported wrongly.
     */
    private static Set<Integer> foreignKeys(Type[] types, boolean[] updatable, PersistenceUnitUtil ids,
                                            Object[] before, Object[] after, Set<Integer> skipped) {
        Set<Integer> foreignKeys = new HashSet<>();
        for (int i = 0; i < types.length && i < before.length && i < after.length; i++) {
            if (skipped.contains(i) || (updatable != null && i < updatable.length && !updatable[i])
                    || !(types[i] instanceof ManyToOneType type) || !type.isReferenceToPrimaryKey()) {
                continue;
            }
            try {
                before[i] = before[i] == null ? null : ids.getIdentifier(before[i]);
                after[i] = after[i] == null ? null : ids.getIdentifier(after[i]);
                foreignKeys.add(i);
            } catch (RuntimeException e) {
                skipped.add(i);
            }
        }
        return foreignKeys;
    }

    /**
     * The changes of the row's JSON columns, each of which is then passed over by everything after.
     * <p>
     * Whether one changed is told from the JSON Hibernate writes for it, not from the values: a class kept in
     * such a column rarely says when two of its objects are equal, and Hibernate rebuilds the earlier one from
     * its JSON, so the values alone would say every save changed it. An object is then read field by field.
     * Where that is not possible, or does not account for everything that differs in the JSON (a field with no
     * getter changed, alone or beside others), the column is reported as one value, the JSON before and after:
     * the fields that were read are not reported beside it, or their change would appear twice. One value
     * shows everything in it, so it is masked whole when anything inside it would have been masked field by
     * field.
     */
    private List<PropertyChange> jsonColumns(PostUpdateEvent event, String rowKey, Class<?> type, String[] names,
                                             Type[] types, Object[] before, Object[] after, Set<Integer> skipped,
                                             boolean masked) {
        List<PropertyChange> changes = new ArrayList<>();
        FormatMapper json = event.getFactory().getSessionFactoryOptions().getJsonFormatMapper();
        for (int i = 0; json != null && i < types.length && i < names.length && i < before.length
                && i < after.length; i++) {
            if (skipped.contains(i) || names[i] == null || !(types[i] instanceof BasicType<?> column)
                    || isConverted(column) || column.getJdbcType().getDefaultSqlTypeCode() != SqlTypes.JSON) {
                continue;
            }
            skipped.add(i);
            try {
                if (Objects.equals(before[i], after[i]) || diff.isPassedOver(type, names[i])) {
                    continue;
                }
                String oldJson = toJson(json, column, before[i], event.getSession());
                String newJson = toJson(json, column, after[i], event.getSession());
                if (Objects.equals(oldJson, newJson)) {
                    continue;
                }
                boolean columnMasked = masked || diff.isMasked(type, names[i]);
                AuditDiff.ObjectChanges fields = diff.objectChanges(before[i], after[i], rowKey, columnMasked);
                if (fields != null && (!fields.changes().isEmpty() || fields.passedOver())
                        && fields.accountsFor(AuditJson.differingPaths(oldJson, newJson))) {
                    changes.addAll(fields.changes());
                    continue;
                }
                PropertyChange whole = diff.valueChange(type, names[i], oldJson, newJson, rowKey,
                        columnMasked || diff.hidesValues(before[i]) || diff.hidesValues(after[i]));
                if (whole != null) {
                    changes.add(whole);
                }
            } catch (RuntimeException e) {
                // This column is left out; the rest of the row is still reported.
                logger.debug("Audit property capture skipped {} of {}: {}", names[i], type.getName(),
                        e.getClass().getName());
            }
        }
        return changes;
    }

    private static boolean isConverted(BasicType<?> type) {
        return type instanceof ConvertedBasicType<?> converted
                && converted.getValueConverter() instanceof JpaAttributeConverter<?, ?>;
    }

    /**
     * Finds the changed properties kept in one column whose value is not simple, and replaces their values with
     * a form a change can be read from:
     * <ul>
     * <li>one written through an {@code AttributeConverter}, by running the converter, which gives the form the
     * database stores. Only those converters are meant: an enum keeps reading as its name.</li>
     * <li>a binary column, whose bytes are left as they are and read as base64.</li>
     * <li>an array or a list of simple values, as a JSON array of its elements. The database may keep it as an
     * array of its own or as binary; neither reads as anything, so the elements are what is reported.</li>
     * </ul>
     * A value that did not change is left alone, so nothing is run for it, and a null stays null. A property
     * whose conversion fails here is skipped rather than reported wrongly. Only single-column properties are
     * looked at: a collection kept in its own table is Hibernate's to load, and is never touched.
     */
    private static Set<Integer> stored(Type[] types, Object[] before, Object[] after, Set<Integer> skipped) {
        Set<Integer> stored = new HashSet<>();
        for (int i = 0; i < types.length && i < before.length && i < after.length; i++) {
            if (skipped.contains(i) || !(types[i] instanceof BasicType<?> type)
                    || Objects.equals(before[i], after[i])) {
                continue;
            }
            try {
                if (type instanceof ConvertedBasicType<?> converted
                        && converted.getValueConverter() instanceof JpaAttributeConverter<?, ?> converter) {
                    before[i] = toStored(converter, before[i]);
                    after[i] = toStored(converter, after[i]);
                } else if (before[i] instanceof byte[] || after[i] instanceof byte[]) {
                    // Bytes already read as base64.
                } else if (AuditValues.isList(before[i]) || AuditValues.isList(after[i])) {
                    before[i] = AuditValues.jsonArray(before[i]);
                    after[i] = AuditValues.jsonArray(after[i]);
                } else {
                    continue;
                }
                stored.add(i);
            } catch (RuntimeException e) {
                skipped.add(i);
            }
        }
        return stored;
    }

    @SuppressWarnings("unchecked")
    private static String toJson(FormatMapper json, BasicType<?> type, Object value, WrapperOptions options) {
        return value == null ? null : json.toString(value, (JavaType<Object>) type.getJavaTypeDescriptor(), options);
    }

    @SuppressWarnings("unchecked")
    private static Object toStored(BasicValueConverter<?, ?> converter, Object value) {
        return value == null ? null : ((BasicValueConverter<Object, Object>) converter).toRelationalValue(value);
    }
}
