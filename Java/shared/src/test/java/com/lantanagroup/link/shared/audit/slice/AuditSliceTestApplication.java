package com.lantanagroup.link.shared.audit.slice;

import org.springframework.boot.autoconfigure.SpringBootApplication;

/**
 * The configuration {@code AuditSliceTest} finds. It sits in its own package so that no other shared test
 * finds it, and its scan sees only {@link SliceController}.
 */
@SpringBootApplication
public class AuditSliceTestApplication {
}
