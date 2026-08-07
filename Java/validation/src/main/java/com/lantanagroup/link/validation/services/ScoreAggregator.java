package com.lantanagroup.link.validation.services;

import com.lantanagroup.link.validation.enums.PiqiDimension;
import com.lantanagroup.link.validation.enums.RubricResultStatus;
import com.lantanagroup.link.validation.enums.Severity;
import com.lantanagroup.link.validation.models.ScoreCardDto;
import com.lantanagroup.link.validation.models.RawFinding;
import org.springframework.stereotype.Component;

import java.util.EnumMap;
import java.util.List;
import java.util.Map;

@Component
public class ScoreAggregator {

    public ScoreCardDto aggregate(List<RawFinding> findings) {
        Map<PiqiDimension, RubricResultStatus> byDim = new EnumMap<>(PiqiDimension.class);
        for (PiqiDimension d : PiqiDimension.values()) {
            byDim.put(d, RubricResultStatus.ACCEPTABLE);
        }
        for (RawFinding f : findings) {
            if (f.getDimension() == null) {
                continue;
            }
            RubricResultStatus current = byDim.get(f.getDimension());
            byDim.put(f.getDimension(), upgrade(current, f.getSeverity()));
        }
        RubricResultStatus overall = rollUp(byDim);
        return ScoreCardDto.builder()
                .interpretation(overall)
                .byDimension(byDim)
                .build();
    }

    private RubricResultStatus upgrade(RubricResultStatus current, Severity severity) {
        return switch (severity) {
            case ERROR -> RubricResultStatus.UNACCEPTABLE;
            case WARNING -> current == RubricResultStatus.UNACCEPTABLE
                    ? RubricResultStatus.UNACCEPTABLE
                    : RubricResultStatus.ACCEPTABLE_WITH_WARNINGS;
            case INFORMATION -> current;
        };
    }

    private RubricResultStatus rollUp(Map<PiqiDimension, RubricResultStatus> byDim) {
        if (byDim.containsValue(RubricResultStatus.UNACCEPTABLE)) return RubricResultStatus.UNACCEPTABLE;
        if (byDim.containsValue(RubricResultStatus.ACCEPTABLE_WITH_WARNINGS)) return RubricResultStatus.ACCEPTABLE_WITH_WARNINGS;
        return RubricResultStatus.ACCEPTABLE;
    }
}
