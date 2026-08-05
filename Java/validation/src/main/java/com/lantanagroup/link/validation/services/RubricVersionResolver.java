package com.lantanagroup.link.validation.services;

import com.lantanagroup.link.validation.entities.RubricVersion;
import com.lantanagroup.link.validation.enums.RubricVersionStatus;
import com.lantanagroup.link.validation.exceptions.RubricLifecycleException;
import com.lantanagroup.link.validation.exceptions.RubricVersionNotFoundException;
import com.lantanagroup.link.validation.models.Semver;
import com.lantanagroup.link.validation.repositories.RubricVersionRepository;
import lombok.RequiredArgsConstructor;
import org.springframework.stereotype.Service;

@Service
@RequiredArgsConstructor
public class RubricVersionResolver {

    private final RubricVersionRepository rubricVersionRepository;


    public RubricVersion resolve(String rubricId, String semver, boolean publishedOnly) {
        if (semver != null && !semver.isBlank()) {
            RubricVersion version = rubricVersionRepository.findByRubricIdAndSemver(rubricId, semver)
                    .orElseThrow(() -> new RubricVersionNotFoundException(rubricId, semver));
            if (publishedOnly && version.getStatus() != RubricVersionStatus.PUBLISHED) {
                // fetched-then-checked (rather than a status-scoped query) so the caller gets a clear
                // 409 "not PUBLISHED" instead of a misleading 404.
                throw new RubricLifecycleException(rubricId, semver, version.getStatus(), "evaluate");
            }
            return version;
        }
        // no semver requested → newest PUBLISHED version. Sort semantically (1.10.0 > 1.9.0);
        // the DB stores semver as a string, so lexical ordering would be wrong.
        return rubricVersionRepository
                .findByRubricIdAndStatus(rubricId, RubricVersionStatus.PUBLISHED)
                .stream()
                .max(Semver.versionComparator())
                .orElseThrow(() -> new RubricVersionNotFoundException(rubricId, "latest-published"));
    }
}
