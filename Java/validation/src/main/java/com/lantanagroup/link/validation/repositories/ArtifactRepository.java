package com.lantanagroup.link.validation.repositories;

import com.lantanagroup.link.validation.entities.Artifact;
import com.lantanagroup.link.validation.entities.ArtifactType;
import org.springframework.data.jpa.repository.JpaRepository;
import org.springframework.stereotype.Repository;

import java.util.Optional;

@Repository
public interface ArtifactRepository extends JpaRepository<Artifact, Long> {
    Optional<Artifact> findByTypeAndName(ArtifactType type, String name);

    boolean deleteByTypeAndName(ArtifactType type, String name);
}
