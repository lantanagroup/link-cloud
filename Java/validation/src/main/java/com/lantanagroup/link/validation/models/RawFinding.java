package com.lantanagroup.link.validation.models;

import com.lantanagroup.link.validation.enums.PiqiDimension;
import com.lantanagroup.link.validation.enums.Severity;
import lombok.AllArgsConstructor;
import lombok.Builder;
import lombok.Getter;
import lombok.NoArgsConstructor;
import lombok.Setter;

@Getter
@Setter
@NoArgsConstructor
@AllArgsConstructor
@Builder
public class RawFinding {

    private Long checkId;
    private String checkLocalId;
    private PiqiDimension dimension;
    private Severity severity;
    private String code;
    private String message;
    private String location;
    private String expression;
    private boolean notEvaluated;
}
