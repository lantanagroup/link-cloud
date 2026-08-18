package com.lantanagroup.link.shared.kafka.records;

import lombok.AllArgsConstructor;
import lombok.Builder;
import lombok.Data;
import lombok.NoArgsConstructor;

@Data
@Builder
@NoArgsConstructor
@AllArgsConstructor
public class ResourceKey {
    private String facilityId;
    private String patientId;
}
