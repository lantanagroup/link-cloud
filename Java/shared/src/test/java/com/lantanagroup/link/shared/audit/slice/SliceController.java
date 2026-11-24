package com.lantanagroup.link.shared.audit.slice;

import com.lantanagroup.link.shared.audit.AuditAction;
import com.lantanagroup.link.shared.audit.AuditContext;
import com.lantanagroup.link.shared.audit.Auditable;
import org.springframework.web.bind.annotation.PathVariable;
import org.springframework.web.bind.annotation.PutMapping;
import org.springframework.web.bind.annotation.RestController;

/** A controller that injects {@link AuditContext}, as the services' controllers will. */
@RestController
public class SliceController {
    private final AuditContext audit;

    public SliceController(AuditContext audit) {
        this.audit = audit;
    }

    @PutMapping("/slice/{id}")
    @Auditable(action = AuditAction.UPDATE, resource = "Slice")
    public String update(@PathVariable String id) {
        audit.setPatientId("patient-1");
        return "ok";
    }
}
