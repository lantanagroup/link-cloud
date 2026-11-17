package com.lantanagroup.link.validation.records;

import com.lantanagroup.link.validation.entities.Result;
import com.lantanagroup.link.validation.enums.RubricResultStatus;

import java.util.List;

public record BridgeOutcome(List<Result> results, RubricResultStatus status) {
}
