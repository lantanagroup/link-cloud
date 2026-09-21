import { ViewReportComponent } from './view-report.component';
import { SubmissionStatus } from '../../../../interfaces/report/report-entry.interface';

/**
 * Covers the submission-status presentation mapping only.
 *
 * Built without TestBed on purpose. The constructor is pure parameter assignment and these
 * are pure functions, so instantiating directly keeps the spec independent of the nine
 * services the component injects — the DI setup that makes much of this suite fragile.
 */
describe('ViewReportComponent submission status presentation', () => {
  let component: ViewReportComponent;

  beforeEach(() => {
    component = new ViewReportComponent(
      null!, null!, null!, null!, null!, null!, null!, null!, null!);
  });

  describe('NotSubmitted', () => {
    it('uses its own badge class rather than the pending one', () => {
      expect(component.getSubmissionStatusClass(SubmissionStatus.NotSubmitted))
        .toBe('status-not-submitted');
    });

    it('reads as a deliberate outcome, not a failure', () => {
      expect(component.getSubmissionStatusText(SubmissionStatus.NotSubmitted))
        .toBe('Submission Skipped');
    });

    it('is not confused with a patient that was never eligible', () => {
      expect(component.getSubmissionStatusClass(SubmissionStatus.NotSubmitted))
        .not.toBe(component.getSubmissionStatusClass(SubmissionStatus.NotEligable));
      expect(component.getSubmissionStatusText(SubmissionStatus.NotSubmitted))
        .not.toBe(component.getSubmissionStatusText(SubmissionStatus.NotEligable));
    });
  });

  describe('every declared status', () => {
    // Spelled out so a new SubmissionStatus member cannot quietly fall through to the
    // default and render as "Pending" — which is what NotSubmitted did before it was mapped.
    const expected: Array<[SubmissionStatus, string, string]> = [
      [SubmissionStatus.PendingValidation, 'status-pending', 'Pending Validation'],
      [SubmissionStatus.Submitting, 'status-processing', 'Submitting'],
      [SubmissionStatus.Submitted, 'status-submitted', 'Submitted'],
      [SubmissionStatus.FailedSubmission, 'status-failed', 'Failed Submission'],
      [SubmissionStatus.NotEligable, 'status-not-reportable', 'Not Eligible'],
      [SubmissionStatus.NotSubmitted, 'status-not-submitted', 'Submission Skipped'],
    ];

    expected.forEach(([status, cssClass, label]) => {
      it(`maps ${SubmissionStatus[status]} to ${cssClass} / "${label}"`, () => {
        expect(component.getSubmissionStatusClass(status)).toBe(cssClass);
        expect(component.getSubmissionStatusText(status)).toBe(label);
      });
    });

    it('classifies every member of the enum', () => {
      const declared = Object.values(SubmissionStatus).filter(v => typeof v === 'number');
      expect(expected.length).toBe(declared.length);
    });
  });

  describe('donut chart colours', () => {
    // submissionStatusColors is keyed by the *label*, so renaming a label without moving its
    // key silently drops that slice's colour. That nearly shipped when NotSubmitted was
    // relabelled to "Submission Skipped".
    it('has a colour for every label the chart can be given', () => {
      const labels = [
        SubmissionStatus.PendingValidation,
        SubmissionStatus.Submitting,
        SubmissionStatus.Submitted,
        SubmissionStatus.FailedSubmission,
        SubmissionStatus.NotEligable,
        SubmissionStatus.NotSubmitted,
      ].map(s => component.getSubmissionStatusText(s));

      labels.forEach(label => {
        expect(component.submissionStatusColors[label])
          .withContext(`no colour registered for "${label}"`)
          .toBeDefined();
      });
    });
  });
});
