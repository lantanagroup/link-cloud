package com.lantanagroup.link.measureeval.entities;

import lombok.Getter;
import lombok.Setter;
import org.springframework.data.annotation.CreatedDate;
import org.springframework.data.annotation.Id;
import org.springframework.data.annotation.LastModifiedDate;

import java.util.Date;
import java.util.List;

@Getter
@Setter
public class ReportScheduledEntity {
    @Id
    private String id;

    private String facilityId;

    private List<String> reportTypes;

    private Date periodStart;

    private Date periodEnd;

    @CreatedDate
    private Date createdDate;

    @LastModifiedDate
    private Date modifiedDate;
}
