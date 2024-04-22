package com.lantanagroup.link.validation.repositories;

import com.lantanagroup.link.validation.entities.Artifact;
import org.springframework.data.jpa.repository.JpaRepository;
import org.springframework.stereotype.Repository;

import java.util.List;

@Repository
public interface ArtifactRepository extends JpaRepository<Artifact, Long> {
    List<Artifact> findByTypeAndName(Artifact.Types type, String name);
}
