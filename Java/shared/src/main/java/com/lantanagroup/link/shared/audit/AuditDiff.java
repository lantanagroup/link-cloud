package com.lantanagroup.link.shared.audit;

import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.beans.BeanUtils;
import org.springframework.beans.BeanWrapper;
import org.springframework.beans.BeanWrapperImpl;
import org.springframework.core.annotation.AnnotatedElementUtils;
import org.springframework.data.annotation.CreatedDate;
import org.springframework.data.annotation.LastModifiedDate;
import org.springframework.data.annotation.Version;
import org.springframework.util.ClassUtils;
import org.springframework.util.ReflectionUtils;

import java.beans.PropertyDescriptor;
import java.lang.reflect.AnnotatedElement;
import java.lang.reflect.Array;
import java.lang.reflect.Field;
import java.lang.reflect.Method;
import java.lang.reflect.Modifier;
import java.math.BigDecimal;
import java.util.ArrayList;
import java.util.Collection;
import java.util.HashMap;
import java.util.HashSet;
import java.util.List;
import java.util.Locale;
import java.util.Map;
import java.util.Objects;
import java.util.Set;
import java.util.concurrent.ConcurrentHashMap;

/**
 * Turns two objects into the masked property changes of an audit event, the way the .NET interceptor does.
 * Never logs a property value.
 */
final class AuditDiff {
    private static final Logger logger = LoggerFactory.getLogger(AuditDiff.class);
    // How far into an object kept in one column its fields are followed; past this it is not read at all.
    private static final int MAX_DEPTH = 12;

    private final AuditMasking masking;
    private final Map<Class<?>, List<Compared>> compared = new ConcurrentHashMap<>();
    // "class#property#suffix" of each foreign-key or converted property seen, with how it is reported.
    private final Map<String, Compared> described = new ConcurrentHashMap<>();
    private final Map<Class<?>, MaskedFields> maskedFields = new ConcurrentHashMap<>();
    // "class#property" of each getter that has failed once, so a getter that always fails warns only once.
    private final Set<String> failedGetters = ConcurrentHashMap.newKeySet();

    AuditDiff(AuditMasking masking) {
        this.masking = masking;
    }

    /** The simple properties that differ between the two objects; empty if either is null. Never throws. */
    List<PropertyChange> diff(Object before, Object after) {
        if (before == null || after == null) {
            return List.of();
        }
        try {
            Class<?> type = commonClass(before, after);
            if (type == null) {
                return List.of();
            }
            BeanWrapper beforeWrapper = new BeanWrapperImpl(before);
            BeanWrapper afterWrapper = new BeanWrapperImpl(after);
            List<PropertyChange> changes = new ArrayList<>();
            for (Compared property : compared.computeIfAbsent(type, this::compare)) {
                Object oldValue;
                Object newValue;
                try {
                    oldValue = beforeWrapper.getPropertyValue(property.javaName());
                    newValue = afterWrapper.getPropertyValue(property.javaName());
                } catch (RuntimeException e) {
                    String message = "Audit diff skipped property {} of {}: its getter failed ({})";
                    if (failedGetters.add(type.getName() + "#" + property.javaName())) {
                        logger.warn(message, property.javaName(), type.getName(), e.getClass().getName());
                    } else {
                        logger.debug(message, property.javaName(), type.getName(), e.getClass().getName());
                    }
                    continue;
                }
                PropertyChange change = change(property, oldValue, newValue, null, null, false);
                if (change != null) {
                    changes.add(change);
                }
            }
            return changes;
        } catch (RuntimeException e) {
            logger.warn("Audit diff failed and was ignored: {}", e.getClass().getName());
            return List.of();
        }
    }

    /**
     * The key a row's changes are reported under: its id as text, or masked when the id property is one of the
     * values that must not be shown. Never throws.
     *
     * @param type       the entity class
     * @param idProperty the Java name of the id property, or null
     * @param id         the row's id
     */
    String rowKey(Class<?> type, String idProperty, Object id) {
        try {
            if (type != null && idProperty != null) {
                for (Compared property : compared.computeIfAbsent(type, this::compare)) {
                    if (property.javaName().equals(idProperty) && property.masked()) {
                        return AuditMasking.REDACTED;
                    }
                }
            }
            return AuditValues.text(id);
        } catch (RuntimeException e) {
            return null;
        }
    }

    /**
     * The changes one saved row went through, from the values a persistence provider held for it before and
     * after the save. The three arrays are parallel; a property the class does not compare (a collection, a
     * timestamp) is passed over, exactly as {@link #diff} passes over it. Each change names
     * the entity and the row, which a change computed from two loose objects cannot. Never throws.
     *
     * @param type           the class the properties belong to: the entity, or a class embedded in it, whose
     *                       changes then name that class as their entity and the owning row as their row
     * @param idProperty     the Java name of the id property, which is never reported, or null
     * @param rowKey         the key of the row, from {@link #rowKey}
     * @param names          the Java names of the row's properties
     * @param before         their values before the save
     * @param after          their values after it
     * @param skippedIndexes positions in the arrays to pass over, such as the version property's
     * @param referenceIndexes positions that hold a foreign key: the row's link to one related row, whose
     *                       values the caller has already turned into that row's id. Such a property is
     *                       reported under its name with {@code Id} appended ({@code vendor} as
     *                       {@code VendorId}), which is the name the .NET services report a foreign key by.
     * @param storedIndexes  positions of properties written through a converter, whose values the caller has
     *                       already turned into the form the database stores. Such a property is reported as
     *                       one value in that form, whatever its Java type, as .NET reports a converted one.
     * @param masked         whether every value is masked, whatever the property: true for the fields of an
     *                       embedded object that is masked as a whole
     */
    List<PropertyChange> rowChanges(Class<?> type, String idProperty, String rowKey, String[] names,
                                    Object[] before, Object[] after, Set<Integer> skippedIndexes,
                                    Set<Integer> referenceIndexes, Set<Integer> storedIndexes, boolean masked) {
        if (type == null || names == null || before == null || after == null) {
            return List.of();
        }
        try {
            Map<String, Compared> byName = new HashMap<>();
            Set<String> wireNames = new HashSet<>();
            for (Compared property : compared.computeIfAbsent(type, this::compare)) {
                byName.put(property.javaName(), property);
                wireNames.add(property.wireName().toLowerCase(Locale.ROOT));
            }
            String entity = type.getSimpleName();
            List<PropertyChange> changes = new ArrayList<>();
            int length = Math.min(names.length, Math.min(before.length, after.length));
            for (int i = 0; i < length; i++) {
                if (skippedIndexes.contains(i) || names[i] == null) {
                    continue;
                }
                Compared property;
                if (referenceIndexes.contains(i)) {
                    property = reference(type, names[i], wireNames);
                } else {
                    property = byName.get(names[i]);
                    // A converted property of a type that is not simple (a list, an object) is compared here
                    // only because the caller supplied its stored form.
                    if (property == null && storedIndexes.contains(i) && !isPassedOver(type, names[i])) {
                        property = described(type, names[i], "");
                    }
                }
                // The id identifies the row rather than describing it.
                if (property == null || property.javaName().equals(idProperty)) {
                    continue;
                }
                PropertyChange change = change(property, before[i], after[i], entity, rowKey, masked);
                if (change != null) {
                    changes.add(change);
                }
            }
            return changes;
        } catch (RuntimeException e) {
            logger.warn("Audit could not read the changes of a saved {} row and ignored them: {}",
                    type.getName(), e.getClass().getName());
            return List.of();
        }
    }

    /**
     * How a foreign-key property is reported, or null when it is not. It is not when the class also maps the
     * same column as a simple property ({@code vendorId} beside {@code vendor}): that property already reports
     * the change under the same name, and one change must not appear twice. It is masked by {@code @AuditMask}
     * on its field or getter, or by name, under either the property's own name or the reported one.
     */
    private Compared reference(Class<?> type, String javaName, Set<String> simpleWireNames) {
        Compared reference = described(type, javaName, "Id");
        return simpleWireNames.contains(reference.wireName().toLowerCase(Locale.ROOT)) ? null : reference;
    }

    /**
     * How a property the class scan passed over is reported once a persistence provider has supplied a value
     * for it that can be compared: under its own name plus the suffix, masked by {@code @AuditMask} on its
     * field or getter or by name. Worked out once per property.
     */
    private Compared described(Class<?> type, String javaName, String suffix) {
        return described.computeIfAbsent(type.getName() + "#" + javaName + "#" + suffix, key -> {
            String propertyName = AuditValues.propertyName(javaName);
            String wireName = propertyName + suffix;
            Field field = ReflectionUtils.findField(type, javaName);
            PropertyDescriptor descriptor = BeanUtils.getPropertyDescriptor(type, javaName);
            Method getter = descriptor == null ? null : descriptor.getReadMethod();
            boolean masked = isMaskedByAnnotation(field) || isMaskedByAnnotation(getter)
                    || masking.isMasked(wireName) || masking.isMasked(propertyName)
                    || mayShowMaskedField(type, javaName);
            return new Compared(javaName, wireName, masked);
        });
    }

    /**
     * Whether a property is one no change is ever reported for, whatever its type or converter: a timestamp by
     * name, or the version and created and modified dates. The class scan leaves these out, and a converter
     * must not bring them back.
     */
    boolean isPassedOver(Class<?> type, String javaName) {
        if (masking.isTimestamp(AuditValues.propertyName(javaName))) {
            return true;
        }
        PropertyDescriptor descriptor = BeanUtils.getPropertyDescriptor(type, javaName);
        return isBookkeeping(ReflectionUtils.findField(type, javaName))
                || isBookkeeping(descriptor == null ? null : descriptor.getReadMethod());
    }

    /**
     * Whether a property the class scan passed over, such as an embedded object, is masked: by
     * {@code @AuditMask} on its field or getter, or by name.
     */
    boolean isMasked(Class<?> type, String javaName) {
        return described(type, javaName, "").masked();
    }

    /**
     * The change of a property reported as one value, from the two texts the caller made of it; null when they
     * are the same.
     *
     * @param masked whether the value is masked whatever the property says
     */
    PropertyChange valueChange(Class<?> type, String javaName, String before, String after, String rowKey,
                               boolean masked) {
        return change(described(type, javaName, ""), before, after, type.getSimpleName(), rowKey, masked);
    }

    /**
     * The changes inside an object kept in one column, such as a JSON column: each field of it that changed is
     * a change of its own, naming the object's class as its entity and the owning row as its row, which is how
     * the .NET services report an object they own and store as JSON. Fields are read through getters, as
     * everywhere else here, and by the same rules: masks, the timestamp skip, binary as base64, a list of
     * simple values as a JSON array. An object inside is read the same way under its own class. A list of
     * objects is read item by item, each item's row being the owner's with the item's position, counted from
     * one, appended ({@code 12|1}).
     * <p>
     * Null when the value cannot be read this way, and the caller reports the column as one value instead: it
     * is not an object with getters (a map, a tree of JSON nodes, a list of simple values, a record), or
     * something inside it is not, or a list of objects changed length, where reading by position would hide
     * the item added or removed. What comes back also says which properties were read, so the caller can tell
     * whether the read accounts for everything that differs in what is stored. Never throws.
     *
     * @param masked whether every value is masked, because the column as a whole is
     */
    ObjectChanges objectChanges(Object before, Object after, String rowKey, boolean masked) {
        try {
            ObjectWalk walk = new ObjectWalk();
            return walk(before, after, rowKey, masked, walk, 0, "")
                    ? new ObjectChanges(walk.changes, walk.passedOver, Set.copyOf(walk.read)) : null;
        } catch (RuntimeException | StackOverflowError e) {
            return null;
        }
    }

    /**
     * What {@link #objectChanges} found.
     *
     * @param changes    the fields that changed
     * @param passedOver whether a field that is never reported, such as a timestamp, changed too. When nothing
     *                   else did, that is the whole of the difference, and there is nothing to report.
     * @param read       the path of every property that was read through a getter, reported or not: the
     *                   property names leading to it, each after a slash, with positions in a list left
     *                   out, which is how {@link AuditJson#differingPaths} writes a path
     */
    record ObjectChanges(List<PropertyChange> changes, boolean passedOver, Set<String> read) {
        /**
         * Whether the read went through everything at these paths. False when it did not, or cannot be told:
         * something stored there has no getter, and its change would be missing from the changes.
         */
        boolean accountsFor(Set<String> paths) {
            return paths != null && read.containsAll(paths);
        }
    }

    private static final class ObjectWalk {
        private final List<PropertyChange> changes = new ArrayList<>();
        private boolean passedOver;
        private final Set<String> read = new HashSet<>();
    }

    /** Reads one object, or one list of objects, into the walk; false when it cannot be read field by field. */
    private boolean walk(Object before, Object after, String rowKey, boolean masked, ObjectWalk walk, int depth,
                         String path) {
        if (before == null && after == null) {
            return true;
        }
        if (depth > MAX_DEPTH) {
            return false;
        }
        if (AuditValues.isList(before) || AuditValues.isList(after)) {
            if (!AuditValues.isList(before) || !AuditValues.isList(after)) {
                return false;
            }
            List<Object> oldItems = items(before);
            List<Object> newItems = items(after);
            if (oldItems.size() != newItems.size()) {
                return false;
            }
            for (int i = 0; i < oldItems.size(); i++) {
                String itemKey = rowKey == null ? String.valueOf(i + 1) : rowKey + "|" + (i + 1);
                if (!walk(oldItems.get(i), newItems.get(i), itemKey, masked, walk, depth + 1, path)) {
                    return false;
                }
            }
            return true;
        }
        Class<?> type = objectClass(before, after);
        if (type == null) {
            return false;
        }
        Map<String, Compared> simple = new HashMap<>();
        for (Compared property : compared.computeIfAbsent(type, this::compare)) {
            simple.put(property.javaName(), property);
        }
        String entity = type.getSimpleName();
        for (PropertyDescriptor descriptor : BeanUtils.getPropertyDescriptors(type)) {
            Method getter = descriptor.getReadMethod();
            String name = descriptor.getName();
            if (getter == null || "class".equals(name)) {
                continue;
            }
            String propertyPath = path + "/" + AuditJson.pathName(name);
            walk.read.add(propertyPath);
            Object oldValue = before == null ? null : ReflectionUtils.invokeMethod(getter, before);
            Object newValue = after == null ? null : ReflectionUtils.invokeMethod(getter, after);
            Compared property = simple.get(name);
            if (property == null && isPassedOver(type, name)) {
                walk.passedOver |= !AuditValues.text(oldValue).equals(AuditValues.text(newValue));
                continue;
            }
            if (property == null && oldValue == null && newValue == null) {
                continue;
            }
            if (property == null) {
                property = described(type, name, "");
                if (isBinary(oldValue, newValue)) {
                    // Bytes read as base64.
                } else if (isListOfSimpleValues(oldValue) && isListOfSimpleValues(newValue)) {
                    oldValue = AuditValues.jsonArray(oldValue);
                    newValue = AuditValues.jsonArray(newValue);
                } else if (walk(oldValue, newValue, rowKey, masked || property.masked(), walk, depth + 1,
                        propertyPath)) {
                    continue;
                } else {
                    return false;
                }
            }
            PropertyChange change = change(property, oldValue, newValue, entity, rowKey, masked);
            if (change != null) {
                walk.changes.add(change);
            }
        }
        return true;
    }

    /**
     * Whether a value kept in one column holds anything that must not be shown: a property or a field of its
     * class, or of a class anywhere inside it, that is masked by {@code @AuditMask} or by name, or a map key
     * that is masked by name. Such a column must not be shown as one value. Fields are looked at as well as
     * getters, because what is shown as one value is everything stored, with or without a getter. True when it
     * cannot be told. Never throws.
     */
    boolean hidesValues(Object value) {
        try {
            return hidesValues(value, 0);
        } catch (RuntimeException | StackOverflowError e) {
            return true;
        }
    }

    private boolean hidesValues(Object value, int depth) {
        if (value == null || BeanUtils.isSimpleValueType(value.getClass()) || value instanceof byte[]) {
            return false;
        }
        if (depth > MAX_DEPTH) {
            return true;
        }
        if (value instanceof Map<?, ?> map) {
            for (Map.Entry<?, ?> entry : map.entrySet()) {
                if ((entry.getKey() instanceof String key && masking.isMasked(AuditValues.propertyName(key)))
                        || hidesValues(entry.getValue(), depth + 1)) {
                    return true;
                }
            }
            return false;
        }
        if (value instanceof Iterable<?> items) {
            for (Object item : items) {
                if (hidesValues(item, depth + 1)) {
                    return true;
                }
            }
            return false;
        }
        if (value.getClass().isArray()) {
            for (Object item : items(value)) {
                if (hidesValues(item, depth + 1)) {
                    return true;
                }
            }
            return false;
        }
        Class<?> type = ClassUtils.getUserClass(value);
        for (Compared property : compared.computeIfAbsent(type, this::compare)) {
            if (property.masked()) {
                return true;
            }
        }
        for (PropertyDescriptor descriptor : BeanUtils.getPropertyDescriptors(type)) {
            Method getter = descriptor.getReadMethod();
            if (getter == null || "class".equals(descriptor.getName())
                    || BeanUtils.isSimpleValueType(descriptor.getPropertyType())) {
                continue;
            }
            if (described(type, descriptor.getName(), "").masked()
                    || hidesValues(ReflectionUtils.invokeMethod(getter, value), depth + 1)) {
                return true;
            }
        }
        // What has no getter is stored and shown all the same: a record's components, a public field.
        List<Field> fields = new ArrayList<>();
        ReflectionUtils.doWithFields(type, fields::add, field -> !Modifier.isStatic(field.getModifiers()));
        for (Field field : fields) {
            if (isMaskedByAnnotation(field) || masking.isMasked(AuditValues.propertyName(field.getName()))) {
                return true;
            }
            if (!BeanUtils.isSimpleValueType(field.getType())) {
                ReflectionUtils.makeAccessible(field);
                if (hidesValues(ReflectionUtils.getField(field, value), depth + 1)) {
                    return true;
                }
            }
        }
        return false;
    }

    /**
     * The class two values are read as when they are read field by field, or null when they are not: either is
     * something other than an object with getters, or they are of unrelated classes.
     */
    private static Class<?> objectClass(Object before, Object after) {
        Class<?> a = before == null ? null : ClassUtils.getUserClass(before);
        Class<?> b = after == null ? null : ClassUtils.getUserClass(after);
        Class<?> type = a == null ? b : b == null || a.isAssignableFrom(b) ? a : b.isAssignableFrom(a) ? b : null;
        if (type == null || (a != null && !isObjectWithGetters(a)) || (b != null && !isObjectWithGetters(b))) {
            return null;
        }
        return type;
    }

    private static boolean isObjectWithGetters(Class<?> type) {
        String name = type.getName();
        if (BeanUtils.isSimpleValueType(type) || type.isArray() || type.isRecord()
                || Map.class.isAssignableFrom(type) || Iterable.class.isAssignableFrom(type)
                || name.startsWith("java.") || name.startsWith("javax.") || name.startsWith("jakarta.")) {
            return false;
        }
        for (PropertyDescriptor descriptor : BeanUtils.getPropertyDescriptors(type)) {
            if (descriptor.getReadMethod() != null && !"class".equals(descriptor.getName())) {
                return true;
            }
        }
        return false;
    }

    private static boolean isBinary(Object before, Object after) {
        return (before == null || before instanceof byte[]) && (after == null || after instanceof byte[]);
    }

    private static boolean isListOfSimpleValues(Object value) {
        if (value == null) {
            return true;
        }
        if (!AuditValues.isList(value)) {
            return false;
        }
        for (Object item : items(value)) {
            if (item != null && !BeanUtils.isSimpleValueType(item.getClass())) {
                return false;
            }
        }
        return true;
    }

    private static List<Object> items(Object list) {
        if (list instanceof Collection<?> collection) {
            return new ArrayList<>(collection);
        }
        List<Object> items = new ArrayList<>();
        for (int i = 0; i < Array.getLength(list); i++) {
            items.add(Array.get(list, i));
        }
        return items;
    }

    private static boolean isMaskedByAnnotation(AnnotatedElement element) {
        return element != null && AnnotatedElementUtils.hasAnnotation(element, AuditMask.class);
    }

    /** The change of one compared property, masked; null when the two values are the same. */
    private static PropertyChange change(Compared property, Object oldValue, Object newValue, String entity,
                                         String rowKey, boolean masked) {
        if (isEqual(oldValue, newValue)) {
            return null;
        }
        String oldText = AuditValues.text(oldValue);
        String newText = AuditValues.text(newValue);
        if (oldText.equals(newText)) {
            return null;
        }
        if (masked || property.masked()) {
            oldText = AuditMasking.REDACTED;
            newText = AuditMasking.REDACTED;
        }
        return new PropertyChange(property.wireName(), oldText, newText, entity, rowKey);
    }

    /** One row as given: no comparison, no timestamp skip, masked by name. */
    PropertyChange single(String name, Object before, Object after) {
        if (masking.isMasked(name)) {
            return new PropertyChange(name, AuditMasking.REDACTED, AuditMasking.REDACTED);
        }
        return new PropertyChange(name, AuditValues.text(before), AuditValues.text(after));
    }

    private static boolean isEqual(Object a, Object b) {
        if (a instanceof BigDecimal x && b instanceof BigDecimal y) {
            return x.compareTo(y) == 0;
        }
        return Objects.equals(a, b);
    }

    private static Class<?> commonClass(Object before, Object after) {
        Class<?> a = ClassUtils.getUserClass(before);
        Class<?> b = ClassUtils.getUserClass(after);
        if (a.isAssignableFrom(b)) {
            return a;
        }
        if (b.isAssignableFrom(a)) {
            return b;
        }
        logger.warn("Audit diff skipped: {} and {} are unrelated classes", a.getName(), b.getName());
        return null;
    }

    /**
     * The properties of the class that are compared, scanned once per class. {@code @AuditMask} on a field
     * masks the property whose name equals the field's ignoring case (field {@code eMail}, property
     * {@code EMail}). An annotated field that names no property of the class cannot be matched to the getter
     * that shows it, so every property that might be that getter is masked instead, and the class is reported
     * once, here: see {@link #mayShowMaskedField}.
     */
    private List<Compared> compare(Class<?> type) {
        MaskedFields fields = maskedFields.computeIfAbsent(type, AuditDiff::scanMaskedFields);

        List<Compared> result = new ArrayList<>();
        List<String> maskedByAnnotation = new ArrayList<>();
        List<String> maskedForAnUnmatchedField = new ArrayList<>();
        for (PropertyDescriptor descriptor : BeanUtils.getPropertyDescriptors(type)) {
            String javaName = descriptor.getName();
            boolean fieldMasked = fields.masked().contains(javaName.toLowerCase(Locale.ROOT));
            Method getter = descriptor.getReadMethod();
            if (getter == null || "class".equals(javaName)) {
                continue;
            }
            boolean guarded = mayShowMaskedField(type, javaName);
            if (guarded) {
                maskedForAnUnmatchedField.add(AuditValues.propertyName(javaName));
            }
            if (!BeanUtils.isSimpleValueType(descriptor.getPropertyType())) {
                continue;
            }
            String wireName = AuditValues.propertyName(javaName);
            if (masking.isTimestamp(wireName)) {
                continue;
            }
            Field field = ReflectionUtils.findField(type, javaName);
            if (isBookkeeping(field) || isBookkeeping(getter)) {
                continue;
            }
            boolean annotated = fieldMasked || AnnotatedElementUtils.hasAnnotation(getter, AuditMask.class);
            if (annotated) {
                maskedByAnnotation.add(wireName);
            }
            boolean masked = annotated || guarded || masking.isMasked(wireName);
            result.add(new Compared(javaName, wireName, masked));
        }
        if (!maskedByAnnotation.isEmpty()) {
            logger.info("Audit masks properties of {} through @AuditMask: {}", type.getName(), maskedByAnnotation);
        }
        if (!maskedForAnUnmatchedField.isEmpty()) {
            logger.warn("@AuditMask on field(s) {} of {} matches no property of that class, so it cannot be told "
                            + "which getter shows the value. Every property with no field of its own name is "
                            + "masked until @AuditMask is put on the getter instead: {}",
                    fields.unmatched(), type.getName(), maskedForAnUnmatchedField);
        }
        return List.copyOf(result);
    }

    /**
     * Whether a property might be showing a field that is marked {@code @AuditMask} under another name. A field
     * that is marked and names no property of its class (field {@code socialSecurityNumber}, getter
     * {@code getSsn}) cannot be matched to its getter, so the mark cannot be put where it belongs. Rather than
     * let the value through, every property that could be that getter is masked: one with no field of its own
     * name. A property that has such a field shows that field and is left as it is, which is what keeps a
     * marked field with no getter at all from masking the rest of its class.
     */
    private boolean mayShowMaskedField(Class<?> type, String javaName) {
        MaskedFields fields = maskedFields.computeIfAbsent(type, AuditDiff::scanMaskedFields);
        return !fields.unmatched().isEmpty() && !fields.all().contains(javaName.toLowerCase(Locale.ROOT));
    }

    /** The fields of a class, read once: all of them, and those marked {@code @AuditMask}. */
    private static MaskedFields scanMaskedFields(Class<?> type) {
        Set<String> all = new HashSet<>();
        Set<String> masked = new HashSet<>();
        List<Field> marked = new ArrayList<>();
        ReflectionUtils.doWithFields(type, field -> {
            all.add(field.getName().toLowerCase(Locale.ROOT));
            if (AnnotatedElementUtils.hasAnnotation(field, AuditMask.class)) {
                masked.add(field.getName().toLowerCase(Locale.ROOT));
                marked.add(field);
            }
        }, field -> !Modifier.isStatic(field.getModifiers()));
        Set<String> properties = new HashSet<>();
        for (PropertyDescriptor descriptor : BeanUtils.getPropertyDescriptors(type)) {
            properties.add(descriptor.getName().toLowerCase(Locale.ROOT));
        }
        List<String> unmatched = marked.stream().map(Field::getName)
                .filter(name -> !properties.contains(name.toLowerCase(Locale.ROOT))).toList();
        return new MaskedFields(Set.copyOf(all), Set.copyOf(masked), unmatched);
    }

    /**
     * @param all       the names of the class's fields, lower-cased
     * @param masked    those marked {@code @AuditMask}, lower-cased
     * @param unmatched the marked ones that name no property of the class, as written
     */
    private record MaskedFields(Set<String> all, Set<String> masked, List<String> unmatched) {
    }

    private static boolean isBookkeeping(AnnotatedElement element) {
        return element != null
                && (AnnotatedElementUtils.hasAnnotation(element, Version.class)
                || AnnotatedElementUtils.hasAnnotation(element, CreatedDate.class)
                || AnnotatedElementUtils.hasAnnotation(element, LastModifiedDate.class));
    }

    private record Compared(String javaName, String wireName, boolean masked) {
    }
}
