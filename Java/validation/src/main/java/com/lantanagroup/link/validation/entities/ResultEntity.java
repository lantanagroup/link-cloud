package com.lantanagroup.link.validation.entities;

import com.fasterxml.jackson.annotation.JsonInclude;
import com.lantanagroup.link.validation.model.ResultModel;
import jakarta.persistence.Column;
import jakarta.persistence.Entity;
import jakarta.persistence.GeneratedValue;
import jakarta.persistence.Id;
import lombok.Getter;
import lombok.NoArgsConstructor;
import lombok.Setter;
import org.hl7.fhir.r4.model.OperationOutcome;

@Entity(name = "result")
@Getter
@Setter
@NoArgsConstructor
@JsonInclude(JsonInclude.Include.NON_NULL)
public class ResultEntity extends ResultModel {
    @Id
    @GeneratedValue
    private Long id;

    @Column(nullable = false)
    private String tenantId;

    @Column(nullable = false)
    private String reportId;

    @Column(nullable = false, columnDefinition = "varchar(max)")
    private String message;

    @Column(nullable = false, length = 4096)
    private String expression;

    @Column(nullable = false)
    private OperationOutcome.IssueSeverity severity;

    private OperationOutcome.IssueType type;

    private String location;

    public ResultEntity(ResultModel resultModel, String tenantId, String reportId) {
        this.message = resultModel.getMessage();
        this.expression = resultModel.getExpression();
        this.severity = resultModel.getSeverity();
        this.type = resultModel.getType();
        this.location = resultModel.getLocation();
    }

    public static ResultModel toModel(ResultEntity entity) {
        ResultModel model = new ResultModel();
        model.setMessage(entity.getMessage());
        model.setExpression(entity.getExpression());
        model.setSeverity(entity.getSeverity());
        model.setType(entity.getType());
        model.setLocation(entity.getLocation());
        return model;
    }
}
