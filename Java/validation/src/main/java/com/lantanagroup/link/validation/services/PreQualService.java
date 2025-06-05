package com.lantanagroup.link.validation.services;

import com.fasterxml.jackson.databind.ObjectMapper;
import com.lantanagroup.link.validation.entities.Category;
import com.lantanagroup.link.validation.entities.PreQualSummary;
import com.lantanagroup.link.validation.entities.Result;
import lombok.Getter;
import lombok.Setter;
import org.apache.commons.io.IOUtils;
import org.apache.commons.text.StringEscapeUtils;
import org.apache.commons.text.StringSubstitutor;
import org.hl7.fhir.r4.model.OperationOutcome;
import org.springframework.context.annotation.Scope;
import org.springframework.context.annotation.ScopedProxyMode;
import org.springframework.stereotype.Service;

import java.io.IOException;
import java.io.InputStream;
import java.io.InputStreamReader;
import java.io.Reader;
import java.nio.charset.StandardCharsets;
import java.util.HashMap;
import java.util.List;
import java.util.Map;
import java.util.Objects;
import java.util.stream.Collectors;

@Service
@Scope(value = "prototype", proxyMode = ScopedProxyMode.TARGET_CLASS)
public class PreQualService {

    public String generateSimplePreQualReport(PreQualSummary summary) throws IOException {

        final Map<String, Category> categoriesById = summary.getCategories().stream()
                .collect(Collectors.toMap(Category::getId, category -> category));

        final Map<Category, Integer> countsByCategory = summary.getResults().stream()
                .flatMap(result -> result.getCategories().stream())
                .filter(category -> category.getId() != null)
                .collect(Collectors.toMap(
                        category -> categoriesById.get(category.getId()),
                        category -> 1,
                        Integer::sum));

        final Map<Result, Integer> countsByIssue = summary.getResults().stream()
                .collect(Collectors.groupingBy(
                        Result::getMessage,
                        Collectors.toList()
                ))
                .entrySet().stream()
                .collect(Collectors.toMap(
                        entry -> entry.getValue().get(0),
                        entry -> entry.getValue().size()
                ));

        //determine pre-qualification status
        if(summary.getResults() == null || summary.getResults().isEmpty()) {
            summary.setPrequalificationStatus(false);
        }
        else
        {
            summary.setPrequalificationStatus(summary.getResults().stream().allMatch(r -> r.getCategories()
                    .stream().allMatch(Category::isAcceptable
                    )));
        }

        Map<String, Object> substitutions = new HashMap<>();
        substitutions.put("tenant", summary.getFacilityId());
        substitutions.put("report", summary.getReport().getId());
        substitutions.put("measures", String.join(", ", summary.getReport().getMeasures()));
        substitutions.put("periodStart", summary.getReport().getPeriodStart());
        substitutions.put("periodEnd", summary.getReport().getPeriodEnd());
        substitutions.put("preQualified", summary.getPrequalificationStatus() ? "Yes" : "No");
        substitutions.put("categoryCount", countsByCategory.size());
        substitutions.put("issueCount", countsByIssue.values().stream().reduce(0, Integer::sum));
        substitutions.put("categories", getCategoryHtml(countsByCategory));
        substitutions.put("issues", getIssueHtml(countsByIssue));
        StringSubstitutor substitutor = new StringSubstitutor(substitutions, "<!--", "-->");
        String template = IOUtils.resourceToString("/simple-pre-qual.html", StandardCharsets.UTF_8);

        return substitutor.replace(template);
    }

    private String getCategoryHtml(Map<Category, Integer> countsByCategory) {
        StringBuilder builder = new StringBuilder();
        for (Map.Entry<Category, Integer> countByCategory : countsByCategory.entrySet()) {
            Category category = countByCategory.getKey();
            int count = countByCategory.getValue();
            builder.append("<tr>");
            addCell(builder, category.getTitle());
            addCell(builder, category.getSeverity());
            addCell(builder, category.isAcceptable());
            addCell(builder, category.getGuidance());
            addCell(builder, count);
            builder.append("</tr>");
        }
        return builder.toString();
    }

    private String getIssueHtml(Map<Result, Integer> countsByIssue) {
        StringBuilder builder = new StringBuilder();
        for (Map.Entry<Result, Integer> countByIssue : countsByIssue.entrySet()) {
            Result result = countByIssue.getKey();
            int count = countByIssue.getValue();
            builder.append("<tr>");
            addCell(builder, result.getSeverity());
            addCell(builder, result.getCode());
            addCell(builder, result.getMessage());
            addCell(builder, result.getCategories().stream()
                    .map(Category::getTitle)
                    .collect(Collectors.joining(", ")));
            addCell(builder, count);
            builder.append("</tr>");
        }
        return builder.toString();
    }

    private void addCell(StringBuilder builder, Object value) {
        builder.append("<td>");
        String string;
        if (value == null) {
            string = "";
        } else if (value instanceof Boolean) {
            string = ((Boolean) value) ? "Yes" : "No";
        } else {
            string = value.toString();
        }
        builder.append(StringEscapeUtils.escapeHtml4(string));
        builder.append("</td>");
    }

    public String generatePrequalReport(PreQualSummary summary) throws IOException {

        if (summary == null || summary.getResults().isEmpty()) {
            return null;
        }

        ObjectMapper mapper = new ObjectMapper();

        try (InputStream is = this.getClass().getClassLoader().getResourceAsStream("pre-qual.html")) {
            String json = mapper.writeValueAsString(summary);
            String html = readInputStream(is);
            return html.replace("var summary = {};", "var summary = " + json + ";");
        }
    }

    private String readInputStream(InputStream is) throws IOException {
//        Reader inputStreamReader = new InputStreamReader(is);
//        StringBuilder sb = new StringBuilder();
//
//        int data = inputStreamReader.read();
//        while (data != -1) {
//            sb.append((char) data);
//            data = inputStreamReader.read();
//        }
//
//        inputStreamReader.close();
//
//        return sb.toString();
        return IOUtils.toString(is, StandardCharsets.UTF_8);
    }


    @Getter
    public static class Issue {
        private final String severity;
        private final String code;
        private final String details;
        private final String expression;

        @Setter
        private List<Category> categories;

        public Issue(Result result) {
            this.severity = String.valueOf(result.getSeverity());
            this.code = String.valueOf(result.getCode());
            this.details = result.getMessage();
            this.expression = result.getExpression();
        }

        public Issue(OperationOutcome.OperationOutcomeIssueComponent ooIssue) {
            this(toResult(ooIssue));
        }

        @Override
        public int hashCode() {
            return Objects.hash(severity, code, details);
        }

        @Override
        public boolean equals(Object object) {
            if (object == this) {
                return true;
            }
            if (!(object instanceof Issue issue)) {
                return false;
            }
            return Objects.equals(severity, issue.severity)
                    && Objects.equals(code, issue.code)
                    && Objects.equals(details, issue.details);
        }
    }

    public static Result toResult(OperationOutcome.OperationOutcomeIssueComponent model) {
        Result result = new Result();

        result.setCode(model.getCode());
        result.setMessage(model.getDetails() != null ? model.getDetails().getText() : null);
        result.setSeverity(model.getSeverity());
        result.setExpression(model.getExpression() != null ? model.getExpression().toString() : null);
        result.setLocation(model.getLocation()  != null ? model.getLocation().toString() : null);

        return result;
    }
}
