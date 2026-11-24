package org.opencds.cqf.fhir.cr.measure.r4;

import ca.uhn.fhir.repository.IRepository;
import org.opencds.cqf.fhir.cr.measure.common.MeasureReference;

import java.time.ZonedDateTime;

/**
 * Gives MeasureEval's debug sections the MeasureDef and per-subject CQL results behind a single-measure report.
 */
// Lives in clinical-reasoning's package because the capture method is package-private.
// See dev-docs/measureeval-cqf-fhir.md
public final class LinkMeasureDefCapture {

    private LinkMeasureDefCapture() {
    }

    /**
     * Evaluates one measure for one subject, returning the report with the MeasureDef and CQL results behind it.
     */
    public static MeasureDefAndR4MeasureReport evaluateSingleMeasure(R4MultiMeasureService service,
                                                                     MeasureReference measure,
                                                                     ZonedDateTime periodStart,
                                                                     ZonedDateTime periodEnd,
                                                                     String subjectId,
                                                                     IRepository repository) {
        return service.evaluateSingleMeasureCaptureDef(measure,
                                                       periodStart,
                                                       periodEnd,
                                                       null,
                                                       subjectId,
                                                       null,
                                                       repository,
                                                       null,
                                                       null,
                                                       null);
    }
}
