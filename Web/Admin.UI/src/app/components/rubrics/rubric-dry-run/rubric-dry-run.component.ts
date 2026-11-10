import { DecimalPipe } from '@angular/common';
import { ChangeDetectorRef, Component, OnDestroy, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatTableModule } from '@angular/material/table';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { catchError, of, Subscription, switchMap, tap } from 'rxjs';

import {
  IRubricFinding,
  IRubricRunResult,
  IRubricVersionDetail,
  RubricDimension,
  RubricResultStatus,
  RubricSeverity
} from '../../../interfaces/validation/rubric.interface';
import { RubricApiError, RubricService } from '../../../services/gateway/validation/rubric.service';
import { dimensionLabel, isCheckEnabled, orderedChecks, resultLabel, severityLabel, statusLabel } from '../rubric-display';

/** What a sample turned out to be: something to send, or the reason it cannot be sent. */
export interface SampleReading {
  sample?: Record<string, unknown>;
  /** "Bundle with 42 resources", "Patient resource". */
  description?: string;
  error?: string;
}

/**
 * Reads the text of a sample. A dry run takes FHIR as JSON: one resource, or a Bundle of them. This only
 * rules out what plainly is not that; the service decides whether it is FHIR.
 */
export function readSample(text: string): SampleReading {
  const trimmed = text.trim();
  if (!trimmed) {
    return { error: 'Provide a sample to run against.' };
  }
  if (trimmed.startsWith('<')) {
    return { error: 'The sample looks like XML. A dry run takes FHIR as JSON.' };
  }
  let parsed: unknown;
  try {
    parsed = JSON.parse(trimmed);
  } catch (error) {
    return { error: `The sample is not valid JSON: ${error instanceof Error ? error.message : 'it could not be read'}.` };
  }
  if (!parsed || typeof parsed !== 'object' || Array.isArray(parsed)
    || typeof (parsed as Record<string, unknown>)['resourceType'] !== 'string') {
    return { error: 'The sample must be one FHIR resource or a Bundle: a JSON object with a resourceType.' };
  }
  const sample = parsed as Record<string, unknown>;
  const type = sample['resourceType'] as string;
  if (type === 'Bundle') {
    const count = Array.isArray(sample['entry']) ? sample['entry'].length : 0;
    return { sample, description: `Bundle with ${count} ${count === 1 ? 'entry' : 'entries'}` };
  }
  return { sample, description: `${type} resource` };
}

/** One check's part in a run. */
export interface CheckRun {
  id: string;
  /** False for a check the version has disabled, which was not run. */
  ran: boolean;
  findings: number;
  /** The outcome the scoring gave it; only the check scorecard gives one. */
  outcome?: RubricResultStatus;
  durationMs?: number;
}

/** How many findings are shown before the person asks for more. A sample can produce thousands. */
export const FINDINGS_PAGE = 200;

const SEVERITIES: RubricSeverity[] = ['ERROR', 'WARNING', 'INFORMATION'];

/**
 * Runs a version against a sample before it is published: a FHIR resource or Bundle, uploaded or pasted.
 * The sample is sent to the Validation service and is not kept; only the outcome and the time are recorded
 * on the version. The page never fetches patient data itself.
 */
@Component({
  selector: 'app-rubric-dry-run',
  imports: [DecimalPipe, FormsModule, MatProgressSpinnerModule, MatTableModule, RouterLink],
  templateUrl: './rubric-dry-run.component.html',
  styleUrls: ['./rubric-dry-run.component.scss']
})
export class RubricDryRunComponent implements OnInit, OnDestroy {
  readonly findingColumns = ['check', 'severity', 'dimension', 'message', 'location'];
  readonly checkColumns = ['check', 'outcome', 'findings', 'duration'];
  readonly severities = SEVERITIES;

  readonly statusLabel = statusLabel;
  readonly resultLabel = resultLabel;
  readonly severityLabel = severityLabel;
  readonly dimensionLabel = dimensionLabel;

  rubricId = '';
  semver = '';
  version: IRubricVersionDetail | null = null;
  loading = false;
  notFound = false;
  error: string | null = null;

  /** A pasted sample. */
  text = '';
  /** An uploaded sample. It is held here, not put in the box: a patient's Bundle can be many megabytes. */
  file: { name: string; size: number; text: string } | null = null;
  /** Why the sample cannot be sent. */
  sampleError: string | null = null;
  /** What was sent, said beside the result. */
  sampleDescription = '';

  running = false;
  result: IRubricRunResult | null = null;
  /** Why the run could not be done at all. */
  failure: string | null = null;

  checkFilter = '';
  severityFilter: RubricSeverity | '' = '';
  dimensionFilter: RubricDimension | '' = '';
  /** The findings the filters leave, and the part of them on the page. */
  matchingCount = 0;
  shownFindings: IRubricFinding[] = [];
  checkRuns: CheckRun[] = [];
  /** The dimensions and checks that have findings, for the filters. */
  findingDimensions: RubricDimension[] = [];
  findingChecks: string[] = [];

  private shownLimit = FINDINGS_PAGE;
  private subscription?: Subscription;
  private run?: Subscription;

  constructor(
    private route: ActivatedRoute,
    private rubricService: RubricService,
    private changeDetector: ChangeDetectorRef) {
  }

  ngOnInit(): void {
    this.subscription = this.route.paramMap.pipe(
      tap(params => {
        this.rubricId = params.get('rubricId') ?? '';
        this.semver = params.get('semver') ?? '';
        this.version = null;
        this.notFound = false;
        this.error = null;
        this.loading = true;
        this.run?.unsubscribe();
        this.running = false;
        this.result = null;
        this.failure = null;
        this.changeDetector.markForCheck();
      }),
      switchMap(() => this.rubricService.getVersion(this.rubricId, this.semver).pipe(
        catchError((error: RubricApiError) => {
          this.notFound = error.notFound;
          this.error = error.message;
          return of(null);
        })))
    ).subscribe(version => {
      this.version = version;
      this.loading = false;
      this.changeDetector.markForCheck();
    });
  }

  ngOnDestroy(): void {
    this.subscription?.unsubscribe();
    this.run?.unsubscribe();
  }

  get title(): string {
    return this.version?.definition?.title || this.rubricId;
  }

  get statusChipClass(): string {
    return `rubric-chip rubric-chip--${(this.version?.status ?? '').toLowerCase()}`;
  }

  get hasSample(): boolean {
    return this.file !== null || this.text.trim().length > 0;
  }

  get findings(): IRubricFinding[] {
    return this.result?.findings ?? [];
  }

  get dimensionOutcomes(): { dimension: RubricDimension; outcome: RubricResultStatus }[] {
    return Object.entries(this.result?.score?.byDimension ?? {})
      .map(([dimension, outcome]) => ({ dimension: dimension as RubricDimension, outcome: outcome as RubricResultStatus }));
  }

  get hasFilter(): boolean {
    return !!(this.checkFilter || this.severityFilter || this.dimensionFilter);
  }

  get fileSize(): string {
    const size = this.file?.size ?? 0;
    return size >= 1024 * 1024 ? `${(size / (1024 * 1024)).toFixed(1)} MB` : `${Math.max(1, Math.round(size / 1024))} kB`;
  }

  resultChipClass(status?: RubricResultStatus | null): string {
    switch (status) {
      case 'ACCEPTABLE':
        return 'rubric-chip rubric-chip--published';
      case 'ACCEPTABLE_WITH_WARNINGS':
        return 'rubric-chip rubric-chip--warning';
      case 'UNACCEPTABLE':
        return 'rubric-chip rubric-chip--error';
      default:
        return 'rubric-chip';
    }
  }

  severityChipClass(severity?: RubricSeverity | null): string {
    return `rubric-chip rubric-chip--${(severity ?? '').toLowerCase()}`;
  }

  async onFileChosen(event: Event): Promise<void> {
    const input = event.target as HTMLInputElement;
    const chosen = input.files?.[0];
    // Cleared so choosing the same file again is still a change.
    input.value = '';
    if (!chosen) {
      return;
    }
    this.sampleError = null;
    if (!/\.json$/i.test(chosen.name)) {
      this.sampleError = `"${chosen.name}" is not a .json file. A dry run takes FHIR as JSON.`;
      this.changeDetector.markForCheck();
      return;
    }
    try {
      this.file = { name: chosen.name, size: chosen.size, text: await chosen.text() };
      this.text = '';
    } catch {
      this.sampleError = `"${chosen.name}" could not be read.`;
    }
    this.changeDetector.markForCheck();
  }

  removeFile(): void {
    this.file = null;
    this.sampleError = null;
    this.changeDetector.markForCheck();
  }

  onTextChanged(): void {
    this.sampleError = null;
  }

  /** Sends the sample. The result replaces the one before it. */
  runDryRun(): void {
    if (this.running || !this.version) {
      return;
    }
    const reading = readSample(this.file?.text ?? this.text);
    if (!reading.sample) {
      this.sampleError = reading.error ?? null;
      this.changeDetector.markForCheck();
      return;
    }
    this.sampleError = null;
    this.sampleDescription = this.file ? `${this.file.name}: ${reading.description}` : reading.description ?? '';
    this.running = true;
    this.failure = null;
    this.result = null;
    this.changeDetector.markForCheck();

    this.run = this.rubricService.dryRun(this.rubricId, this.version.semver, reading.sample).subscribe({
      next: result => {
        this.running = false;
        this.showResult(result);
        this.changeDetector.markForCheck();
      },
      error: (error: RubricApiError) => {
        this.running = false;
        this.failure = error.errors.length > 0 ? `${error.message}: ${error.errors.join('; ')}` : error.message;
        this.changeDetector.markForCheck();
      }
    });
  }

  applyFilters(): void {
    this.shownLimit = FINDINGS_PAGE;
    this.refreshFindings();
  }

  clearFilters(): void {
    this.checkFilter = '';
    this.severityFilter = '';
    this.dimensionFilter = '';
    this.applyFilters();
  }

  /** Shows only one check's findings: the way from the list of checks to what a check found. */
  showFindingsOf(checkId: string): void {
    this.checkFilter = checkId;
    this.severityFilter = '';
    this.dimensionFilter = '';
    this.applyFilters();
  }

  showMore(): void {
    this.shownLimit += FINDINGS_PAGE;
    this.refreshFindings();
  }

  private showResult(result: IRubricRunResult): void {
    this.result = result;
    const findings = result.findings ?? [];

    const perCheck = new Map<string, number>();
    for (const finding of findings) {
      perCheck.set(finding.checkId, (perCheck.get(finding.checkId) ?? 0) + 1);
    }
    this.findingChecks = [...perCheck.keys()].sort();
    this.findingDimensions = [...new Set(findings.map(finding => finding.dimension).filter(Boolean))] as RubricDimension[];

    // Every check that ran, in the order it ran, then the ones the version has disabled.
    const durations = result.trace?.checkDurationsMs ?? {};
    const outcomes = result.score?.byCheck ?? {};
    const ranIds = Object.keys(durations).length > 0 ? Object.keys(durations) : this.findingChecks;
    const runs: CheckRun[] = ranIds.map(id => ({
      id, ran: true, findings: perCheck.get(id) ?? 0, outcome: outcomes[id], durationMs: durations[id]
    }));
    for (const check of orderedChecks(this.version?.definition)) {
      if (!isCheckEnabled(check) && !ranIds.includes(check.id)) {
        runs.push({ id: check.id, ran: false, findings: 0 });
      }
    }
    this.checkRuns = runs;

    this.checkFilter = '';
    this.severityFilter = '';
    this.dimensionFilter = '';
    this.applyFilters();
  }

  private refreshFindings(): void {
    const matching = this.findings.filter(finding =>
      (!this.checkFilter || finding.checkId === this.checkFilter)
      && (!this.severityFilter || finding.severity === this.severityFilter)
      && (!this.dimensionFilter || finding.dimension === this.dimensionFilter));
    this.matchingCount = matching.length;
    this.shownFindings = matching.slice(0, this.shownLimit);
    this.changeDetector.markForCheck();
  }
}
