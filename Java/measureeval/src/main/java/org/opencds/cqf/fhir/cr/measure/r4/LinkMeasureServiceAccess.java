package org.opencds.cqf.fhir.cr.measure.r4;

import ca.uhn.fhir.repository.IRepository;
import java.time.ZonedDateTime;
import org.hl7.fhir.r4.model.Parameters;
import org.opencds.cqf.fhir.cr.measure.common.MeasureReference;

/**
 * Exposes {@link R4MultiMeasureService#evaluateSingleMeasureCaptureDef}, which cqf-fhir 4.15 and later make
 * package-private ({@code @VisibleForTesting}). MeasureEvaluator needs the captured MeasureDef and CQL
 * results for its debug output, and no public cqf-fhir API returns them.
 *
 * <p>This class lives in cqf-fhir's own package to reach that method (a split package). It works on the
 * classpath and in the Spring Boot jar because the cqf-fhir-cr jar is neither sealed nor a named module.
 * Upstream can change a package-private method in any release, so check this class on every cqf-fhir
 * upgrade, and delete it once cqf-fhir offers a public way to capture the MeasureDef.
 */
public final class LinkMeasureServiceAccess {
    private LinkMeasureServiceAccess() {}

    public static MeasureDefAndR4MeasureReport evaluateSingleMeasureCaptureDef(
            R4MultiMeasureService service, MeasureReference measure, ZonedDateTime start, ZonedDateTime end,
            String reportType, String subjectId, String lastReceivedOn, IRepository repo,
            Parameters parameters, String productLine, String practitioner) {
        return service.evaluateSingleMeasureCaptureDef(
                measure, start, end, reportType, subjectId, lastReceivedOn, repo, parameters, productLine, practitioner);
    }
}
