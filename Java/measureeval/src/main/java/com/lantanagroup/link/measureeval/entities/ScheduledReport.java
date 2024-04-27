package com.lantanagroup.link.measureeval.entities;

import com.fasterxml.jackson.annotation.JsonSetter;
import com.fasterxml.jackson.annotation.Nulls;
import lombok.Getter;
import lombok.Setter;
import org.springframework.data.annotation.CreatedDate;
import org.springframework.data.annotation.Id;
import org.springframework.data.annotation.LastModifiedDate;

import java.util.ArrayList;
import java.util.Date;
import java.util.List;

@Getter
@Setter
public class ScheduledReport {
    @Id
    private String id;

    private String facilityId;

    @JsonSetter(nulls = Nulls.AS_EMPTY)
    private List<String> reportTypes = new ArrayList<>();

    private Date periodStart;
    private Date periodEnd;

    @CreatedDate
    private Date createdDate;

    @LastModifiedDate
    private Date modifiedDate;
}
