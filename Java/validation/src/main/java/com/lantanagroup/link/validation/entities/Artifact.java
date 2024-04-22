package com.lantanagroup.link.validation.entities;

import com.fasterxml.jackson.annotation.JsonInclude;
import jakarta.persistence.*;
import lombok.Getter;
import lombok.Setter;

@Entity
@Getter @Setter
@JsonInclude(JsonInclude.Include.NON_NULL)
public class Artifact {
    @Id
    @GeneratedValue
    private Long id;

    @Enumerated(EnumType.STRING)
    private Types type;

    private String name;

    @Lob
    @Column(columnDefinition = "VARBINARY(MAX)")
    private byte[] content;

    public enum Types {
        PACKAGE,
        RESOURCE
    }
}
