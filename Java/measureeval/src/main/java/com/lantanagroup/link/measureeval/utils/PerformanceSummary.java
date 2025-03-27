package com.lantanagroup.link.measureeval.utils;

import java.util.List;
import java.util.Map;

public class PerformanceSummary {

    private final int totalRequests;
    private final int failedRequests;
    private final double errorRate;

    private final long averageLatencyMs;
    private final long minLatencyMs;
    private final String minLatencyPatient;
    private final long maxLatencyMs;
    private final String maxLatencyPatient;

    private final double averageCpuUsage;
    private final double minCpuUsage;
    private final String minCpuPatient;
    private final double maxCpuUsage;
    private final String maxCpuPatient;

    private final double averageMemoryUsageMB;
    private final double minMemoryUsageMB;
    private final String minMemoryPatient;
    private final double maxMemoryUsageMB;
    private final String maxMemoryPatient;

    private final Map<String, Double> groupPopulationPercentages;
    private final List<String> evaluationsExceedingLatencyBenchmark;

    public PerformanceSummary(int totalRequests, int failedRequests, double errorRate,
                              long averageLatencyMs, long minLatencyMs, String minLatencyPatient,
                              long maxLatencyMs, String maxLatencyPatient,
                              double averageCpuUsage, double minCpuUsage, String minCpuPatient,
                              double maxCpuUsage, String maxCpuPatient,
                              double averageMemoryUsageMB, double minMemoryUsageMB, String minMemoryPatient,
                              double maxMemoryUsageMB, String maxMemoryPatient,
                              Map<String, Double> groupPopulationPercentages,
                              List<String> evaluationsExceedingLatencyBenchmark) {
        this.totalRequests = totalRequests;
        this.failedRequests = failedRequests;
        this.errorRate = errorRate;
        this.averageLatencyMs = averageLatencyMs;
        this.minLatencyMs = minLatencyMs;
        this.minLatencyPatient = minLatencyPatient;
        this.maxLatencyMs = maxLatencyMs;
        this.maxLatencyPatient = maxLatencyPatient;
        this.averageCpuUsage = averageCpuUsage;
        this.minCpuUsage = minCpuUsage;
        this.minCpuPatient = minCpuPatient;
        this.maxCpuUsage = maxCpuUsage;
        this.maxCpuPatient = maxCpuPatient;
        this.averageMemoryUsageMB = averageMemoryUsageMB;
        this.minMemoryUsageMB = minMemoryUsageMB;
        this.minMemoryPatient = minMemoryPatient;
        this.maxMemoryUsageMB = maxMemoryUsageMB;
        this.maxMemoryPatient = maxMemoryPatient;
        this.groupPopulationPercentages = groupPopulationPercentages;
        this.evaluationsExceedingLatencyBenchmark = evaluationsExceedingLatencyBenchmark;
    }

    public int getTotalRequests() {
        return totalRequests;
    }

    public int getFailedRequests() {
        return failedRequests;
    }

    public double getErrorRate() {
        return errorRate;
    }

    public long getAverageLatencyMs() {
        return averageLatencyMs;
    }

    public long getMinLatencyMs() {
        return minLatencyMs;
    }

    public String getMinLatencyPatient() {
        return minLatencyPatient;
    }

    public long getMaxLatencyMs() {
        return maxLatencyMs;
    }

    public String getMaxLatencyPatient() {
        return maxLatencyPatient;
    }

    public double getAverageCpuUsage() {
        return averageCpuUsage;
    }

    public double getMinCpuUsage() {
        return minCpuUsage;
    }

    public String getMinCpuPatient() {
        return minCpuPatient;
    }

    public double getMaxCpuUsage() {
        return maxCpuUsage;
    }

    public String getMaxCpuPatient() {
        return maxCpuPatient;
    }

    public double getAverageMemoryUsageMB() {
        return averageMemoryUsageMB;
    }

    public double getMinMemoryUsageMB() {
        return minMemoryUsageMB;
    }

    public String getMinMemoryPatient() {
        return minMemoryPatient;
    }

    public double getMaxMemoryUsageMB() {
        return maxMemoryUsageMB;
    }

    public String getMaxMemoryPatient() {
        return maxMemoryPatient;
    }

    public Map<String, Double> getGroupPopulationPercentages() {
        return groupPopulationPercentages;
    }

    public List<String> getEvaluationsExceedingLatencyBenchmark() {
        return evaluationsExceedingLatencyBenchmark;
    }
}
