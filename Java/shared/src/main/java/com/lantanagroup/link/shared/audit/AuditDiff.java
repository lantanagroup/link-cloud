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
import java.lang.reflect.Field;
import java.lang.reflect.Method;
import java.math.BigDecimal;
import java.util.ArrayList;
import java.util.LinkedHashSet;
import java.util.List;
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

    private final AuditMasking masking;
    private final Map<Class<?>, List<Compared>> compared = new ConcurrentHashMap<>();
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
                if (isEqual(oldValue, newValue)) {
                    continue;
                }
                String oldText = AuditValues.text(oldValue);
                String newText = AuditValues.text(newValue);
                if (oldText.equals(newText)) {
                    continue;
                }
                if (property.masked()) {
                    oldText = AuditMasking.REDACTED;
                    newText = AuditMasking.REDACTED;
                }
                changes.add(new PropertyChange(property.wireName(), oldText, newText));
            }
            return changes;
        } catch (RuntimeException e) {
            logger.warn("Audit diff failed and was ignored: {}", e.getClass().getName());
            return List.of();
        }
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
     * {@code EMail}); an annotated field that names no property of the class is reported once, here.
     */
    private List<Compared> compare(Class<?> type) {
        List<Field> maskedFields = new ArrayList<>();
        ReflectionUtils.doWithFields(type, maskedFields::add,
                field -> AnnotatedElementUtils.hasAnnotation(field, AuditMask.class));
        Set<Field> unmatchedFields = new LinkedHashSet<>(maskedFields);

        List<Compared> result = new ArrayList<>();
        List<String> maskedByAnnotation = new ArrayList<>();
        for (PropertyDescriptor descriptor : BeanUtils.getPropertyDescriptors(type)) {
            String javaName = descriptor.getName();
            boolean fieldMasked = false;
            for (Field field : maskedFields) {
                if (field.getName().equalsIgnoreCase(javaName)) {
                    fieldMasked = true;
                    unmatchedFields.remove(field);
                }
            }
            Method getter = descriptor.getReadMethod();
            if (getter == null || "class".equals(javaName)
                    || !BeanUtils.isSimpleValueType(descriptor.getPropertyType())) {
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
            boolean masked = annotated || masking.isMasked(wireName);
            result.add(new Compared(javaName, wireName, masked));
        }
        if (!maskedByAnnotation.isEmpty()) {
            logger.info("Audit masks properties of {} through @AuditMask: {}", type.getName(), maskedByAnnotation);
        }
        if (!unmatchedFields.isEmpty()) {
            logger.warn("@AuditMask on field(s) {} of {} matches no property of that class, so it masks nothing; "
                            + "put @AuditMask on the getter instead",
                    unmatchedFields.stream().map(Field::getName).toList(), type.getName());
        }
        return List.copyOf(result);
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
