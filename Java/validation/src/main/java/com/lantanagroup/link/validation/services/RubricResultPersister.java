package com.lantanagroup.link.validation.services;

import com.lantanagroup.link.validation.entities.RubricFinding;
import com.lantanagroup.link.validation.entities.RubricResult;
import com.lantanagroup.link.validation.repositories.RubricFindingRepository;
import com.lantanagroup.link.validation.repositories.RubricResultRepository;
import lombok.RequiredArgsConstructor;
import org.springframework.stereotype.Component;
import org.springframework.transaction.annotation.Transactional;

import java.util.List;


@Component
@RequiredArgsConstructor
public class RubricResultPersister {

    private final RubricResultRepository rubricResultRepository;
    private final RubricFindingRepository rubricFindingRepository;

    @Transactional
    public void persist(RubricResult result, List<RubricFinding> findings) {
        rubricResultRepository.save(result);
        rubricFindingRepository.saveAll(findings);
    }
}
