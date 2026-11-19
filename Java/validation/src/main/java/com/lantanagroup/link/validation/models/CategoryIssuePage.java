package com.lantanagroup.link.validation.models;

import java.util.List;

public record CategoryIssuePage(List<CategoryIssueModel> records, Metadata metadata) {
    public record Metadata(int pageSize, int pageNumber, long totalCount, int totalPages) {}
}
