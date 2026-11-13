import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, ParamMap, provideRouter } from '@angular/router';
import { BehaviorSubject, of, Subject, throwError } from 'rxjs';

import { IRubricFinding, IRubricRunResult, IRubricVersionDetail } from '../../../interfaces/validation/rubric.interface';
import { RubricApiError, RubricService } from '../../../services/gateway/validation/rubric.service';
import { FINDINGS_PAGE, readSample, RubricDryRunComponent } from './rubric-dry-run.component';

const version: IRubricVersionDetail = {
  rubricVersionId: 3,
  rubricId: 'piqi.core',
  semver: '1.2.0',
  status: 'DRAFT',
  checksum: 'abc',
  definition: {
    id: 'piqi.core', semver: '1.2.0', title: 'PIQI Core', dimensions: ['CONFORMANCE', 'TERMINOLOGY'],
    scoringPolicy: { type: 'piqi-dimension-scorecard', rollup: 'worst-of' },
    checks: [
      { id: 'name', type: 'FHIRPATH', dimension: 'CONFORMANCE', ordinal: 1, parameters: { expression: 'Patient.name.exists()' } },
      { id: 'gender', type: 'VALUESET', dimension: 'TERMINOLOGY', ordinal: 2, parameters: { path: 'Patient.gender', valueSet: 'http://vs' } },
      { id: 'off', type: 'FHIRPATH', dimension: 'CONFORMANCE', ordinal: 3, enabled: false, parameters: { expression: 'true' } }
    ]
  }
};

const findings: IRubricFinding[] = [
  { checkId: 'name', dimension: 'CONFORMANCE', severity: 'ERROR', message: 'No name', location: 'Bundle.entry[0].resource' },
  { checkId: 'name', dimension: 'CONFORMANCE', severity: 'ERROR', message: 'No name', location: 'Bundle.entry[4].resource' },
  { checkId: 'gender', dimension: 'TERMINOLOGY', severity: 'WARNING', message: 'Not in the value set', location: 'Bundle.entry[0].resource',
    originalSeverity: 'ERROR', overriddenSeverity: 'WARNING' }
];

function runResult(overrides: Partial<IRubricRunResult> = {}): IRubricRunResult {
  return {
    rubricId: 'piqi.core',
    rubricVersion: '1.2.0',
    status: 'UNACCEPTABLE',
    score: { byDimension: { CONFORMANCE: 'UNACCEPTABLE', TERMINOLOGY: 'ACCEPTABLE_WITH_WARNINGS' } },
    summary: { errorCount: 2, warningCount: 1, informationCount: 0 },
    findings,
    trace: { durationMs: 840, checkDurationsMs: { name: 12.5, gender: 800 } },
    ...overrides
  };
}

const PATIENT = '{"resourceType":"Patient","id":"p1"}';

describe('readSample', () => {
  it('accepts one resource and says what it is', () => {
    expect(readSample(PATIENT)).toEqual({ sample: { resourceType: 'Patient', id: 'p1' }, description: 'Patient resource' });
  });

  it('says how many entries a Bundle has', () => {
    expect(readSample('{"resourceType":"Bundle","entry":[{},{}]}').description).toBe('Bundle with 2 entries');
    expect(readSample('{"resourceType":"Bundle","entry":[{}]}').description).toBe('Bundle with 1 entry');
    expect(readSample('{"resourceType":"Bundle"}').description).toBe('Bundle with 0 entries');
  });

  it('refuses nothing, XML, text that is not JSON, and JSON that is not a resource', () => {
    expect(readSample('   ').error).toBe('Provide a sample to run against.');
    expect(readSample('<Patient xmlns="http://hl7.org/fhir"/>').error).toContain('looks like XML');
    expect(readSample('{"resourceType":').error).toContain('The sample is not valid JSON');
    expect(readSample('[{"resourceType":"Patient"}]').error).toContain('a JSON object with a resourceType');
    expect(readSample('{"id":"p1"}').error).toContain('a JSON object with a resourceType');
  });
});

describe('RubricDryRunComponent', () => {
  let fixture: ComponentFixture<RubricDryRunComponent>;
  let component: RubricDryRunComponent;
  let rubricService: jasmine.SpyObj<RubricService>;
  let params: BehaviorSubject<ParamMap>;

  beforeEach(async () => {
    rubricService = jasmine.createSpyObj<RubricService>('RubricService', ['getVersion', 'dryRun']);
    rubricService.getVersion.and.returnValue(of(version));
    rubricService.dryRun.and.returnValue(of(runResult()));
    params = new BehaviorSubject(convertToParamMap({ rubricId: 'piqi.core', semver: '1.2.0' }));

    await TestBed.configureTestingModule({
      imports: [RubricDryRunComponent],
      providers: [
        provideRouter([]),
        { provide: RubricService, useValue: rubricService },
        { provide: ActivatedRoute, useValue: { paramMap: params } }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(RubricDryRunComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  const text = (): string => (fixture.nativeElement as HTMLElement).textContent ?? '';

  function run(sample = PATIENT): void {
    component.text = sample;
    component.runDryRun();
    fixture.detectChanges();
  }

  it('loads the version it is about and runs nothing by itself', () => {
    expect(rubricService.getVersion).toHaveBeenCalledOnceWith('piqi.core', '1.2.0');
    expect(text()).toContain('Dry run · PIQI Core · 1.2.0');
    expect(rubricService.dryRun).not.toHaveBeenCalled();
    expect(component.hasSample).toBeFalse();
  });

  describe('the sample', () => {
    it('sends a pasted resource as it was written', () => {
      run();

      expect(rubricService.dryRun).toHaveBeenCalledOnceWith('piqi.core', '1.2.0', { resourceType: 'Patient', id: 'p1' });
      expect(component.sampleDescription).toBe('Patient resource');
    });

    it('does not send a sample that is not FHIR JSON, and says why', () => {
      run('<Patient/>');

      expect(rubricService.dryRun).not.toHaveBeenCalled();
      expect(text()).toContain('The sample looks like XML.');
    });

    it('holds an uploaded file aside instead of putting it in the box, and sends it', async () => {
      component.text = 'something pasted earlier';
      const file = new File(['{"resourceType":"Bundle","entry":[{}]}'], 'patient.json');

      await component.onFileChosen({ target: { files: [file], value: 'x' } } as unknown as Event);
      component.runDryRun();

      expect(component.text).toBe('');
      expect(component.file?.name).toBe('patient.json');
      expect(rubricService.dryRun.calls.mostRecent().args[2]).toEqual({ resourceType: 'Bundle', entry: [{}] });
      expect(component.sampleDescription).toBe('patient.json: Bundle with 1 entry');
    });

    it('refuses a file that is not .json', async () => {
      await component.onFileChosen({ target: { files: [new File(['<x/>'], 'patient.xml')], value: 'x' } } as unknown as Event);

      expect(component.file).toBeNull();
      expect(component.sampleError).toContain('"patient.xml" is not a .json file');
    });

    it('goes back to pasting when the file is removed', async () => {
      await component.onFileChosen({ target: { files: [new File([PATIENT], 'p.json')], value: 'x' } } as unknown as Event);

      component.removeFile();

      expect(component.file).toBeNull();
      expect(component.hasSample).toBeFalse();
    });
  });

  describe('the result', () => {
    it('shows the outcome, the counts, the time and the outcome of each dimension', () => {
      run();

      expect(text()).toContain('Unacceptable');
      expect(text()).toContain('2 errors');
      expect(text()).toContain('1 warning');
      expect(text()).toContain('840 ms');
      expect(component.dimensionOutcomes).toEqual([
        { dimension: 'CONFORMANCE', outcome: 'UNACCEPTABLE' },
        { dimension: 'TERMINOLOGY', outcome: 'ACCEPTABLE_WITH_WARNINGS' }
      ]);
    });

    it('says the run is recorded, and that findings are about the sample', () => {
      run();

      expect(text()).toContain('The dry run completed and is recorded on version 1.2.0.');
      expect(text()).toContain('can be published');
    });

    it('offers to publish a draft, and not a version that is already published', () => {
      run();
      const links = (): string[] => Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('a.rubric-button'))
        .map(link => link.textContent?.trim() ?? '');
      expect(links()).toContain('Publish');

      rubricService.getVersion.and.returnValue(of({ ...version, status: 'PUBLISHED' }));
      params.next(convertToParamMap({ rubricId: 'piqi.core', semver: '1.1.0' }));
      fixture.detectChanges();
      run();
      expect(links()).not.toContain('Publish');
    });

    it('lists every check that ran with its findings and time, then the disabled ones as not run', () => {
      run();

      expect(component.checkRuns).toEqual([
        { id: 'name', ran: true, findings: 2, outcome: undefined, durationMs: 12.5 },
        { id: 'gender', ran: true, findings: 1, outcome: undefined, durationMs: 800 },
        { id: 'off', ran: false, findings: 0 }
      ]);
      expect(text()).toContain('Disabled, not run');
    });

    it('shows each check\'s own outcome when the scoring gives one', () => {
      rubricService.dryRun.and.returnValue(of(runResult({ score: { byCheck: { name: 'UNACCEPTABLE', gender: 'ACCEPTABLE_WITH_WARNINGS' } } })));

      run();

      expect(component.checkRuns[0].outcome).toBe('UNACCEPTABLE');
      expect(component.dimensionOutcomes).toEqual([]);
    });

    it('shows a severity a category changed, and what it was', () => {
      run();

      expect(text()).toContain('was Error');
    });

    it('says so when nothing was found', () => {
      rubricService.dryRun.and.returnValue(of(runResult({
        status: 'ACCEPTABLE', findings: [], summary: { errorCount: 0, warningCount: 0, informationCount: 0 }
      })));

      run();

      expect(text()).toContain('No check found anything in this sample.');
      expect(text()).toContain('Acceptable');
    });

    it('is replaced by the next run, and cleared while that run is going', () => {
      run();
      const pending = new Subject<IRubricRunResult>();
      rubricService.dryRun.and.returnValue(pending);

      component.runDryRun();

      expect(component.result).toBeNull();
      expect(component.running).toBeTrue();
      component.runDryRun();
      expect(rubricService.dryRun).toHaveBeenCalledTimes(2);
    });
  });

  describe('filtering the findings', () => {
    beforeEach(() => run());

    it('offers the checks and dimensions that have findings', () => {
      expect(component.findingChecks).toEqual(['gender', 'name']);
      expect(component.findingDimensions).toEqual(['CONFORMANCE', 'TERMINOLOGY']);
    });

    it('narrows by check, severity and dimension together', () => {
      component.severityFilter = 'ERROR';
      component.applyFilters();
      expect(component.shownFindings.map(finding => finding.checkId)).toEqual(['name', 'name']);

      component.dimensionFilter = 'TERMINOLOGY';
      component.applyFilters();
      fixture.detectChanges();
      expect(component.matchingCount).toBe(0);
      expect(text()).toContain('No finding matches these filters.');

      component.clearFilters();
      expect(component.matchingCount).toBe(3);
    });

    it('goes from a check to its findings', () => {
      component.severityFilter = 'ERROR';

      component.showFindingsOf('gender');

      expect(component.checkFilter).toBe('gender');
      expect(component.severityFilter).toBe('');
      expect(component.shownFindings.length).toBe(1);
    });
  });

  it('shows a page of findings at a time when there are thousands', () => {
    const many: IRubricFinding[] = Array.from({ length: FINDINGS_PAGE * 2 + 5 },
      (_, index) => ({ checkId: 'name', severity: 'ERROR', message: `m${index}` }));
    rubricService.dryRun.and.returnValue(of(runResult({ findings: many })));

    run();
    expect(component.shownFindings.length).toBe(FINDINGS_PAGE);

    component.showMore();
    expect(component.shownFindings.length).toBe(FINDINGS_PAGE * 2);
    component.showMore();
    expect(component.shownFindings.length).toBe(many.length);

    component.applyFilters();
    expect(component.shownFindings.length).toBe(FINDINGS_PAGE);
  });

  describe('a run that could not be done', () => {
    it('says why, with the service\'s details, and can be run again', () => {
      rubricService.dryRun.and.returnValue(throwError(() =>
        new RubricApiError('Failed to parse FHIR payload: Unknown resource name "Patien"', 400)));

      run('{"resourceType":"Patien"}');

      expect(text()).toContain('The dry run could not be completed.');
      expect(text()).toContain('Unknown resource name "Patien"');
      expect(component.running).toBeFalse();

      rubricService.dryRun.and.returnValue(of(runResult()));
      run();
      expect(text()).not.toContain('The dry run could not be completed.');
    });

    it('lists the problems when the service gave a list', () => {
      rubricService.dryRun.and.returnValue(throwError(() =>
        new RubricApiError('Request validation failed', 400, ['payload: must not be null'])));

      run();

      expect(component.failure).toBe('Request validation failed: payload: must not be null');
    });
  });

  it('says the version does not exist on a 404', () => {
    rubricService.getVersion.and.returnValue(throwError(() => new RubricApiError('Rubric version not found', 404)));

    params.next(convertToParamMap({ rubricId: 'piqi.core', semver: '9.9.9' }));
    fixture.detectChanges();

    expect(text()).toContain('The rubric "piqi.core" has no version 9.9.9.');
  });
});
