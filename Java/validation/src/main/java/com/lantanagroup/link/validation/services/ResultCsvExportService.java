package com.lantanagroup.link.validation.services;

import org.springframework.jdbc.core.JdbcTemplate;
import org.springframework.jdbc.core.RowCallbackHandler;
import org.springframework.stereotype.Service;

import java.io.IOException;
import java.io.UncheckedIOException;
import java.io.Writer;

@Service
public class ResultCsvExportService {
    private static final int BATCH_SIZE = 1000;
    private final JdbcTemplate jdbc;

    public ResultCsvExportService(JdbcTemplate jdbc) {
        this.jdbc = jdbc;
    }

    public Long upperBound(String facilityId, String reportId) {
        return jdbc.queryForObject("SELECT MAX(id) FROM result WHERE facility_id = ? AND report_id = ?", Long.class, facilityId, reportId);
    }

    public void write(String facilityId, String reportId, Long upperBound, Writer writer) throws IOException {
        writer.write("resultId,facilityId,reportId,patientId,severity,code,message,location,expression,profile,"
                + "categoryId,categoryTitle,categorySeverity,acceptable,submit,guidance\r\n");
        writer.flush();
        if (upperBound == null) return;
        long after = 0;
        while (after < upperBound) {
            if (Thread.currentThread().isInterrupted()) throw new IOException("Export interrupted");
            long[] last = {after};
            try {
                // Limit findings before joining categories. No persistence context, offset growth or full-report list.
                jdbc.query("""
                        SELECT r.id, r.facility_id, r.report_id, r.patient_id, r.severity, r.code,
                               r.message, r.location, r.expression, r.profile,
                               c.id, c.title, c.severity, c.acceptable, c.submit, c.guidance
                        FROM (SELECT * FROM result WHERE facility_id = ? AND report_id = ? AND id > ? AND id <= ?
                              ORDER BY id OFFSET 0 ROWS FETCH NEXT ? ROWS ONLY) r
                        LEFT JOIN result_category rc ON rc.result_id = r.id
                        LEFT JOIN category c ON c.id = rc.category_id
                        ORDER BY r.id, c.id
                        """, (RowCallbackHandler) row -> {
                    if (Thread.currentThread().isInterrupted()) throw new UncheckedIOException(new IOException("Export interrupted"));
                    try {
                        for (int column = 1; column <= 16; column++) {
                            if (column > 1) writer.write(',');
                            writeCell(writer, row.getString(column));
                        }
                        writer.write("\r\n");
                        last[0] = row.getLong(1);
                    } catch (IOException ex) {
                        // Stop querying immediately when the download connection closes.
                        throw new UncheckedIOException(ex);
                    }
                }, facilityId, reportId, after, upperBound, BATCH_SIZE);
            } catch (UncheckedIOException ex) {
                throw ex.getCause();
            }
            if (last[0] == after) break;
            after = last[0];
            writer.flush();
        }
    }

    static void writeCell(Writer writer, String value) throws IOException {
        if (value == null) value = "";
        // CSV quoting alone does not prevent spreadsheet formula execution.
        String stripped = value.stripLeading();
        if ((!stripped.isEmpty() && "=+-@".indexOf(stripped.charAt(0)) >= 0)
                || (!value.isEmpty() && "\t\r\n".indexOf(value.charAt(0)) >= 0)) {
            value = "'" + value;
        }
        writer.write('"');
        writer.write(value.replace("\"", "\"\""));
        writer.write('"');
    }
}
