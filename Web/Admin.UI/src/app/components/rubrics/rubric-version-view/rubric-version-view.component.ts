import { DatePipe } from '@angular/common';
import { ChangeDetectorRef, Component, OnDestroy, OnInit } from '@angular/core';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatTableModule } from '@angular/material/table';
import { MatTabsModule } from '@angular/material/tabs';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { catchError, of, Subscription, switchMap, tap } from 'rxjs';

import {
  IRubricCheck,
  IRubricDefinition,
  IRubricVersionDetail,
  RubricDefinitionFormat
} from '../../../interfaces/validation/rubric.interface';
import { RubricApiError, RubricService } from '../../../services/gateway/validation/rubric.service';
import {
  checkSummary,
  checkTypeLabel,
  dimensionLabel,
  dryRunLabel,
  isCheckEnabled,
  isCombinedCheck,
  orderedChecks,
  scoringLabel,
  severityLabel,
  statusLabel
} from '../rubric-display';

/** A setting of a check, as the expanded row lists it. */
interface CheckSetting {
  label: string;
  value: string;
}

const SETTING_LABELS: Record<string, string> = {
  expression: 'Expression',
  failureMessage: 'Failure message',
  code: 'Code',
  profiles: 'Profiles',
  path: 'Path',
  valueSet: 'Value set',
  system: 'Code system',
  validateCodings: 'Validate codings',
  valueSetWhitelistRegex: 'Value sets matching',
  matchMode: 'Passes when',
  customCheckId: 'Plug-in id',
  className: 'Plug-in class',
  min: 'Minimum',
  max: 'Maximum'
};

/** One version of a rubric, read-only: what it is set to, the checks it runs, and its definition. */
@Component({
  selector: 'app-rubric-version-view',
  imports: [
    DatePipe,
    MatProgressSpinnerModule,
    MatTableModule,
    MatTabsModule,
    RouterLink
  ],
  templateUrl: './rubric-version-view.component.html',
  styleUrls: ['./rubric-version-view.component.scss']
})
export class RubricVersionViewComponent implements OnInit, OnDestroy {
  readonly checkColumns = ['order', 'id', 'type', 'dimension', 'severity', 'enabled', 'summary'];

  readonly statusLabel = statusLabel;
  readonly scoringLabel = scoringLabel;
  readonly dryRunLabel = dryRunLabel;
  readonly checkTypeLabel = checkTypeLabel;
  readonly dimensionLabel = dimensionLabel;
  readonly severityLabel = severityLabel;
  readonly checkSummary = checkSummary;
  readonly isCheckEnabled = isCheckEnabled;

  rubricId = '';
  semver = '';
  version: IRubricVersionDetail | null = null;
  checks: IRubricCheck[] = [];
  definitionJson = '';
  expandedCheckId: string | null = null;
  copied = false;
  /** The format being exported, while the file is being fetched. */
  exporting: RubricDefinitionFormat | null = null;
  /** Why the last export failed, when it did. */
  exportError: string | null = null;

  loading = false;
  notFound = false;
  error: string | null = null;

  private subscription?: Subscription;
  private exportRequest?: Subscription;
  private copiedTimer?: ReturnType<typeof setTimeout>;

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
        this.checks = [];
        this.definitionJson = '';
        this.expandedCheckId = null;
        this.exportRequest?.unsubscribe();
        this.exporting = null;
        this.exportError = null;
        this.notFound = false;
        this.error = null;
        this.loading = true;
        this.changeDetector.markForCheck();
      }),
      switchMap(() => this.rubricService.getVersion(this.rubricId, this.semver).pipe(
        // Handled here, inside switchMap, so a failed load does not end the subscription to the route.
        catchError((error: RubricApiError) => {
          this.notFound = error.notFound;
          this.error = error.message;
          return of(null);
        })))
    ).subscribe(version => {
      if (version) {
        this.version = version;
        this.checks = orderedChecks(version.definition);
        this.definitionJson = version.definition ? JSON.stringify(version.definition, null, 2) : '';
      }
      this.loading = false;
      // The answer arrives outside anything Angular watches for; say the page has changed.
      this.changeDetector.markForCheck();
    });
  }

  ngOnDestroy(): void {
    this.subscription?.unsubscribe();
    this.exportRequest?.unsubscribe();
    clearTimeout(this.copiedTimer);
  }

  get definition(): IRubricDefinition | undefined {
    return this.version?.definition;
  }

  get title(): string {
    return this.definition?.title || this.rubricId;
  }

  get enabledCheckCount(): number {
    return this.checks.filter(isCheckEnabled).length;
  }

  get statusChipClass(): string {
    return `rubric-chip rubric-chip--${(this.version?.status ?? '').toLowerCase()}`;
  }

  severityChipClass(check: IRubricCheck): string {
    return `rubric-chip rubric-chip--${(check.severityOverride ?? '').toLowerCase()}`;
  }

  toggleCheck(check: IRubricCheck): void {
    this.expandedCheckId = this.expandedCheckId === check.id ? null : check.id;
  }

  /** A check's settings as label and value, in the order they were written. Child checks are listed apart. */
  settingsOf(check: IRubricCheck): CheckSetting[] {
    return Object.entries(check.parameters ?? {})
      .filter(([key, value]) => key !== 'checks' && value !== null && value !== undefined && value !== '')
      .map(([key, value]) => ({
        label: SETTING_LABELS[key] ?? key,
        value: RubricVersionViewComponent.settingValue(key, value)
      }));
  }

  /** The child checks of a combined value-set check; none for any other check. */
  childrenOf(check: IRubricCheck): IRubricCheck[] {
    return isCombinedCheck(check) ? check.parameters?.checks ?? [] : [];
  }

  /**
   * Downloads the definition as a file that registering a version accepts as it is. The service writes
   * the file, so the YAML is the service's own and not this screen's idea of it.
   */
  export(format: RubricDefinitionFormat): void {
    if (!this.version || this.exporting) {
      return;
    }
    const semver = this.version.semver;
    this.exporting = format;
    this.exportError = null;

    this.exportRequest = this.rubricService.exportDefinition(this.rubricId, semver, format).subscribe({
      next: text => {
        this.download(text, `${this.rubricId}-${semver}.${format}`,
          format === 'yaml' ? 'application/yaml' : 'application/json');
        this.exporting = null;
        this.changeDetector.markForCheck();
      },
      error: (error: RubricApiError) => {
        this.exportError = error.message;
        this.exporting = null;
        this.changeDetector.markForCheck();
      }
    });
  }

  private download(text: string, fileName: string, type: string): void {
    const url = URL.createObjectURL(new Blob([text], { type }));
    const link = document.createElement('a');
    link.href = url;
    link.download = fileName;
    link.click();
    URL.revokeObjectURL(url);
  }

  async copyDefinition(): Promise<void> {
    try {
      await navigator.clipboard.writeText(this.definitionJson);
      this.copied = true;
      clearTimeout(this.copiedTimer);
      this.copiedTimer = setTimeout(() => {
        this.copied = false;
        this.changeDetector.markForCheck();
      }, 2000);
    } catch {
      // The clipboard is unavailable (an insecure origin, or permission refused). The text is on the
      // page and can be selected, so there is nothing more to do.
      this.copied = false;
    }
    this.changeDetector.markForCheck();
  }

  private static settingValue(key: string, value: unknown): string {
    if (key === 'matchMode') {
      return value === 'ALL' ? 'All of its checks pass' : 'Any of its checks passes';
    }
    if (typeof value === 'boolean') {
      return value ? 'Yes' : 'No';
    }
    if (Array.isArray(value)) {
      return value.join('\n');
    }
    return typeof value === 'object' ? JSON.stringify(value) : String(value);
  }
}
