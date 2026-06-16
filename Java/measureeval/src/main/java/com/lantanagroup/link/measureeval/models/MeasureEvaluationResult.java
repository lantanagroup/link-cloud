package com.lantanagroup.link.measureeval.models;

import com.fasterxml.jackson.annotation.JsonInclude;
import org.hl7.fhir.r4.model.MeasureReport;

import java.util.List;
import java.util.Map;

/**
 * Response DTO that wraps a standard FHIR MeasureReport along with optional debug/evaluation data.
 * When debug mode is enabled, the {@code debugInfo} field is populated with detailed evaluation information.
 */
public class MeasureEvaluationResult {

    private MeasureReport measureReport;
    private DebugInfo debugInfo;

    public MeasureEvaluationResult() {}

    public MeasureEvaluationResult(MeasureReport measureReport, DebugInfo debugInfo) {
        this.measureReport = measureReport;
        this.debugInfo = debugInfo;
    }

    public MeasureReport getMeasureReport() {
        return measureReport;
    }

    public void setMeasureReport(MeasureReport measureReport) {
        this.measureReport = measureReport;
    }

    public DebugInfo getDebugInfo() {
        return debugInfo;
    }

    public void setDebugInfo(DebugInfo debugInfo) {
        this.debugInfo = debugInfo;
    }

    @JsonInclude(JsonInclude.Include.NON_NULL)
    public static class DebugInfo {
        private List<String> errors;
        private List<GroupInfo> groups;
        private Map<String, String> expressionResults;
        private List<CqlLibraryDebug> libraryDebug;
        private List<String> cqlMessages;
        private List<ExpressionTrace> traces;
        private String debugLog;

        public DebugInfo() {}

        public List<String> getErrors() {
            return errors;
        }

        public void setErrors(List<String> errors) {
            this.errors = errors;
        }

        public List<GroupInfo> getGroups() {
            return groups;
        }

        public void setGroups(List<GroupInfo> groups) {
            this.groups = groups;
        }

        public Map<String, String> getExpressionResults() {
            return expressionResults;
        }

        public void setExpressionResults(Map<String, String> expressionResults) {
            this.expressionResults = expressionResults;
        }

        public List<CqlLibraryDebug> getLibraryDebug() {
            return libraryDebug;
        }

        public void setLibraryDebug(List<CqlLibraryDebug> libraryDebug) {
            this.libraryDebug = libraryDebug;
        }

        public List<String> getCqlMessages() {
            return cqlMessages;
        }

        public void setCqlMessages(List<String> cqlMessages) {
            this.cqlMessages = cqlMessages;
        }

        public List<ExpressionTrace> getTraces() {
            return traces;
        }

        public void setTraces(List<ExpressionTrace> traces) {
            this.traces = traces;
        }

        public String getDebugLog() {
            return debugLog;
        }

        public void setDebugLog(String debugLog) {
            this.debugLog = debugLog;
        }
    }

    public static class GroupInfo {
        private String id;
        private Double score;
        private List<PopulationInfo> populations;

        public GroupInfo() {}

        public String getId() {
            return id;
        }

        public void setId(String id) {
            this.id = id;
        }

        public Double getScore() {
            return score;
        }

        public void setScore(Double score) {
            this.score = score;
        }

        public List<PopulationInfo> getPopulations() {
            return populations;
        }

        public void setPopulations(List<PopulationInfo> populations) {
            this.populations = populations;
        }
    }

    public static class PopulationInfo {
        private String type;
        private int count;
        private List<String> subjects;

        public PopulationInfo() {}

        public String getType() {
            return type;
        }

        public void setType(String type) {
            this.type = type;
        }

        public int getCount() {
            return count;
        }

        public void setCount(int count) {
            this.count = count;
        }

        public List<String> getSubjects() {
            return subjects;
        }

        public void setSubjects(List<String> subjects) {
            this.subjects = subjects;
        }
    }

    /**
     * Per-library CQL debug data showing each expression/node that was evaluated,
     * its source location, and the result values.
     */
    public static class CqlLibraryDebug {
        private String libraryName;
        private String subjectId;
        private List<CqlNodeDebug> nodes;

        public CqlLibraryDebug() {}

        public String getLibraryName() {
            return libraryName;
        }

        public void setLibraryName(String libraryName) {
            this.libraryName = libraryName;
        }

        public String getSubjectId() {
            return subjectId;
        }

        public void setSubjectId(String subjectId) {
            this.subjectId = subjectId;
        }

        public List<CqlNodeDebug> getNodes() {
            return nodes;
        }

        public void setNodes(List<CqlNodeDebug> nodes) {
            this.nodes = nodes;
        }
    }

    /**
     * Debug data for a single CQL node/expression showing the source locator
     * and each result value produced at that node during evaluation.
     */
    public static class CqlNodeDebug {
        private String locator;
        private String locatorType;
        private List<String> results;

        public CqlNodeDebug() {}

        public String getLocator() {
            return locator;
        }

        public void setLocator(String locator) {
            this.locator = locator;
        }

        public String getLocatorType() {
            return locatorType;
        }

        public void setLocatorType(String locatorType) {
            this.locatorType = locatorType;
        }

        public List<String> getResults() {
            return results;
        }

        public void setResults(List<String> results) {
            this.results = results;
        }
    }

    /**
     * Hierarchical trace of a CQL expression evaluation showing the call tree:
     * which expressions were called, what they returned, and what sub-expressions
     * they invoked during evaluation.
     */
    @JsonInclude(JsonInclude.Include.NON_NULL)
    public static class ExpressionTrace {
        private String library;
        private String libraryVersion;
        private String expressionName;
        private String nodeType;
        private String locator;
        private String context;
        private String result;
        private List<VariableInfo> arguments;
        private List<VariableInfo> variables;
        private List<ExpressionTrace> children;

        public ExpressionTrace() {}

        public String getLibrary() {
            return library;
        }

        public void setLibrary(String library) {
            this.library = library;
        }

        public String getLibraryVersion() {
            return libraryVersion;
        }

        public void setLibraryVersion(String libraryVersion) {
            this.libraryVersion = libraryVersion;
        }

        public String getExpressionName() {
            return expressionName;
        }

        public void setExpressionName(String expressionName) {
            this.expressionName = expressionName;
        }

        public String getNodeType() {
            return nodeType;
        }

        public void setNodeType(String nodeType) {
            this.nodeType = nodeType;
        }

        public String getLocator() {
            return locator;
        }

        public void setLocator(String locator) {
            this.locator = locator;
        }

        public String getContext() {
            return context;
        }

        public void setContext(String context) {
            this.context = context;
        }

        public String getResult() {
            return result;
        }

        public void setResult(String result) {
            this.result = result;
        }

        public List<VariableInfo> getArguments() {
            return arguments;
        }

        public void setArguments(List<VariableInfo> arguments) {
            this.arguments = arguments;
        }

        public List<VariableInfo> getVariables() {
            return variables;
        }

        public void setVariables(List<VariableInfo> variables) {
            this.variables = variables;
        }

        public List<ExpressionTrace> getChildren() {
            return children;
        }

        public void setChildren(List<ExpressionTrace> children) {
            this.children = children;
        }
    }

    public static class VariableInfo {
        private String name;
        private String value;

        public VariableInfo() {}

        public VariableInfo(String name, String value) {
            this.name = name;
            this.value = value;
        }

        public String getName() {
            return name;
        }

        public void setName(String name) {
            this.name = name;
        }

        public String getValue() {
            return value;
        }

        public void setValue(String value) {
            this.value = value;
        }
    }
}
