package com.lantanagroup.link.shared.kafka.records.retention;

import lombok.AllArgsConstructor;
import lombok.Builder;
import lombok.Data;
import lombok.NoArgsConstructor;

@Data
@Builder
@NoArgsConstructor
@AllArgsConstructor
public class RetentionWindow {
    private RetentionWindowUnit unit;
    private int count;
}
