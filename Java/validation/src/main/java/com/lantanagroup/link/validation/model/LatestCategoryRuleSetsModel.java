package com.lantanagroup.link.validation.model;

import lombok.Getter;
import lombok.Setter;

import java.sql.Timestamp;

/**
 * Model for a set of category rule sets used by the API to return
 * all the latest rules related to a category.
 */
@Getter
@Setter
public class LatestCategoryRuleSetsModel extends CategoryRuleSetsModel {
    private int version;
    private Timestamp timestamp;
}
