package com.lantanagroup.link.validation.records;

import com.lantanagroup.link.validation.services.shadow.ResultDiff;
import lombok.AllArgsConstructor;
import lombok.Builder;
import lombok.Getter;
import lombok.NoArgsConstructor;
import lombok.Setter;

/** JSON-friendly snapshot of a {@link ResultDiff.SeverityChange} pair, for persistence/reporting. */
@Getter
@Setter
@NoArgsConstructor
@AllArgsConstructor
@Builder
public class ShadowSeverityChangeDto {
    private ShadowFindingDto legacy;
    private ShadowFindingDto modern;

    public static ShadowSeverityChangeDto from(ResultDiff.SeverityChange change) {
        return ShadowSeverityChangeDto.builder()
                .legacy(ShadowFindingDto.from(change.legacy()))
                .modern(ShadowFindingDto.from(change.modern()))
                .build();
    }
}
