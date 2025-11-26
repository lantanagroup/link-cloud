package com.lantanagroup.link.measureeval.controllers;

import com.lantanagroup.link.measureeval.services.ReportResourceCache;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.http.ResponseEntity;
import org.springframework.security.access.prepost.PreAuthorize;
import org.springframework.web.bind.annotation.*;

import java.util.List;

@RestController
@RequestMapping("/api/test")
@PreAuthorize("hasRole('LinkUser')")
public class TestController {
    private static final Logger logger = LoggerFactory.getLogger(TestController.class);
    private final ReportResourceCache reportResourceCache;

    private String getMemoryInfo() {
        Runtime runtime = Runtime.getRuntime();
        long totalMemory = runtime.totalMemory();
        long freeMemory = runtime.freeMemory();
        long usedMemory = totalMemory - freeMemory;
        return String.format("Memory usage: %d MB / %d MB", usedMemory / (1024 * 1024), totalMemory / (1024 * 1024));
    }

    public TestController(ReportResourceCache reportResourceCache) {
        this.reportResourceCache = reportResourceCache;
    }

    @GetMapping("/get-list")
    public ResponseEntity<List<String>> getList(@RequestParam String correlationId) {
        long startTime = System.currentTimeMillis();
        String initialMemoryInfo = getMemoryInfo();
        logger.info("Starting get value operation for correlationId {}. {}", correlationId, initialMemoryInfo);
        try {
            List<String> value = reportResourceCache.getReportResources(correlationId);
            long endTime = System.currentTimeMillis();
            String finalMemoryInfo = getMemoryInfo();
            logger.info("Get value operation for correlationId {} completed in {} ms", correlationId, endTime - startTime);
            logger.info("Memory usage after operation: {}", finalMemoryInfo);
            return ResponseEntity.ok(value);
        } catch (Exception e) {
            long endTime = System.currentTimeMillis();
            logger.error("Error getting value. Operation took {} ms. Error: {}", endTime - startTime, e.getMessage());
            return ResponseEntity.badRequest().body(List.of("Error getting value: " + e.getMessage()));
        }
    }

    @PostMapping("/resource-persisted")
    public void resourcePersisted(@RequestParam String correlationId) {
        this.reportResourceCache.resourcePersisted(correlationId);
    }
}
