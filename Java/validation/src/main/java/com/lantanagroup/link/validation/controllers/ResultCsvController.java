package com.lantanagroup.link.validation.controllers;

import com.lantanagroup.link.validation.services.ResultCsvExportService;
import io.swagger.v3.oas.annotations.Operation;
import io.swagger.v3.oas.annotations.responses.ApiResponse;
import io.swagger.v3.oas.annotations.security.SecurityRequirement;
import jakarta.servlet.http.HttpServletResponse;
import org.springframework.web.bind.annotation.*;
import java.io.IOException;
import java.io.BufferedWriter;
import java.io.OutputStreamWriter;
import java.nio.charset.StandardCharsets;
import java.util.UUID;

@RestController
@RequestMapping("/api/validation/facilities/{facilityId}/reports")
@SecurityRequirement(name = "bearer-key")
public class ResultCsvController {
    private final ResultCsvExportService export;

    public ResultCsvController(ResultCsvExportService export) {
        this.export = export;
    }

    @Operation(summary = "Downloads saved report findings and their categories as CSV",
            description = "Streams bounded batches in finding-id order, one row per finding/category pair. "
                    + "Findings without categories have blank category columns. Empty reports produce a header-only CSV. "
                    + "Only saved findings are included. Concurrent updates/deletes may affect the export; "
                    + "new ids above the initial upper bound are excluded. A failure after streaming starts "
                    + "terminates the download; it cannot be replaced with a Problem Details response.")
    @ApiResponse(responseCode = "200", description = "UTF-8 CSV attachment")
    @ApiResponse(responseCode = "400", description = "Invalid report UUID; Problem Details")
    @ApiResponse(responseCode = "500", description = "Failure before streaming starts; Problem Details")
    @GetMapping(value = "/{reportId}/results.csv", produces = "text/csv")
    public void download(@PathVariable String facilityId, @PathVariable UUID reportId, HttpServletResponse response) throws IOException {
        String id = reportId.toString();
        Long upperBound = export.upperBound(facilityId, id);
        response.setContentType("text/csv");
        response.setCharacterEncoding(StandardCharsets.UTF_8.name());
        response.setHeader("Content-Disposition", "attachment; filename=\"validation-results-" + id + ".csv\"");
        response.setHeader("Cache-Control", "no-store");
        // Synchronous streaming avoids MVC async timeouts and unbounded async worker queues.
        // Avoid PrintWriter, which swallows client-disconnect IOExceptions.
        var writer = new BufferedWriter(new OutputStreamWriter(response.getOutputStream(), StandardCharsets.UTF_8), 65536);
        export.write(facilityId, id, upperBound, writer);
        writer.flush();
    }
}
