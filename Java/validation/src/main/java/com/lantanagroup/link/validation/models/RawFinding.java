package com.lantanagroup.link.validation.models;

import com.lantanagroup.link.validation.enums.PiqiDimension;
import com.lantanagroup.link.validation.enums.Severity;
import lombok.*;

@Getter
@Setter
@NoArgsConstructor
@AllArgsConstructor
@Builder
public class RawFinding {
    private String checkLocalId;
    private PiqiDimension dimension;
    private Severity severity;
    private String code;
    private String message;
    private String location;
    private String expression;
}
