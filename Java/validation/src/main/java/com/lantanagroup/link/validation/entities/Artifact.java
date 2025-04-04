package com.lantanagroup.link.validation.entities;

import com.fasterxml.jackson.annotation.JsonInclude;
import com.fasterxml.jackson.annotation.JsonView;
import com.lantanagroup.link.shared.serdes.Views;
import jakarta.persistence.*;
import lombok.Getter;
import lombok.Setter;

@Getter
@Setter
@Entity
@Table(uniqueConstraints = @UniqueConstraint(name = "uq_artifact_type_name", columnNames = {"type", "name"}))
@JsonInclude(JsonInclude.Include.NON_NULL)
public class Artifact {
    @Id
    @GeneratedValue(strategy = GenerationType.IDENTITY)
    @JsonView(Views.Summary.class)
    private Long id;

    @Enumerated(EnumType.STRING)
    @Column(nullable = false)
    @JsonView(Views.Summary.class)
    private ArtifactType type;

    @Column(nullable = false)
    @JsonView(Views.Summary.class)
    private String name;

    @Lob
    @Column(columnDefinition = "varbinary(max)", nullable = false)
    @JsonView(Views.Detail.class)
    private byte[] content;
}
