import { ChangeDetectorRef, Component, OnDestroy } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { Subscription, switchMap } from 'rxjs';

import {
  IRubricDefinition,
  IRubricSavePrecondition,
  IRubricValidationResult,
  RubricDefinitionFormat
} from '../../../interfaces/validation/rubric.interface';
import { RubricApiError, RubricService } from '../../../services/gateway/validation/rubric.service';
import { scoringLabel } from '../rubric-display';
import { nextVersion } from '../rubric-draft';
import { EDITOR_DEFINITION_STATE } from '../rubric-editor/rubric-editor-state';

/** What importing the reviewed definition would do. */
type ImportEffect = 'new-rubric' | 'new-version' | 'replace-draft' | 'unchanged-draft' | 'immutable';

/**
 * Brings in a rubric that exists as a file: upload it or paste it, review what it would create, then
 * import it as a draft. The service reads and checks the text; nothing is saved until Import, and an
 * import never publishes anything or changes a published or retired version.
 */
@Component({
  selector: 'app-rubric-import',
  imports: [FormsModule, RouterLink],
  templateUrl: './rubric-import.component.html',
  styleUrls: ['./rubric-import.component.scss']
})
export class RubricImportComponent implements OnDestroy {
  readonly scoringLabel = scoringLabel;

  text = '';
  /** The file the text came from, when it was uploaded. */
  fileName: string | null = null;
  /** Why the chosen file could not be used. */
  fileError: string | null = null;

  busy: 'reviewing' | 'importing' | null = null;
  /** What the service made of the text. Cleared when the text changes: it is no longer about it. */
  review: IRubricValidationResult | null = null;
  /** Why the review or the import could not be done at all. */
  failure: string | null = null;

  private action?: Subscription;

  constructor(private rubricService: RubricService, private router: Router, private changeDetector: ChangeDetectorRef) {
  }

  ngOnDestroy(): void {
    this.action?.unsubscribe();
  }

  get definition(): IRubricDefinition | undefined {
    return this.review?.definition;
  }

  /** What importing would do, once a valid definition has been reviewed. */
  get effect(): ImportEffect | null {
    const review = this.review;
    if (!review?.valid || !review.definition) {
      return null;
    }
    switch (review.existingStatus) {
      case undefined:
        return review.rubricExists ? 'new-version' : 'new-rubric';
      case 'DRAFT':
        return review.checksum && review.checksum === review.existingChecksum ? 'unchanged-draft' : 'replace-draft';
      default:
        return 'immutable';
    }
  }

  get canImport(): boolean {
    return this.effect === 'new-rubric' || this.effect === 'new-version' || this.effect === 'replace-draft';
  }

  get checkCount(): number {
    return (this.definition?.checks ?? []).length;
  }

  get enabledCheckCount(): number {
    return (this.definition?.checks ?? []).filter(check => check.enabled !== false).length;
  }

  async onFileChosen(event: Event): Promise<void> {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    // Cleared so choosing the same file again, after changing it on disk, is noticed.
    input.value = '';
    if (!file) {
      return;
    }
    this.fileError = null;
    if (!/\.(ya?ml|json)$/i.test(file.name)) {
      this.fileError = `"${file.name}" is not a .yaml, .yml or .json file.`;
    } else {
      try {
        this.text = await file.text();
        this.fileName = file.name;
        this.onTextChanged(false);
      } catch {
        this.fileError = `"${file.name}" could not be read.`;
      }
    }
    this.changeDetector.markForCheck();
  }

  /** The text changed, so what was reviewed is no longer what would be imported. */
  onTextChanged(typed = true): void {
    if (typed) {
      this.fileName = null;
    }
    this.review = null;
    this.failure = null;
  }

  /** Has the service read and check the text, and say what importing it would do. Saves nothing. */
  reviewText(): void {
    const text = this.text.trim();
    if (!text || this.busy) {
      return;
    }
    this.busy = 'reviewing';
    this.review = null;
    this.failure = null;
    this.action = this.rubricService.validateText(text, this.format(text)).subscribe({
      next: result => this.reviewed(result),
      error: (error: RubricApiError) => this.failed(error.message)
    });
  }

  /** Saves the reviewed definition as a draft and opens it. */
  importDefinition(): void {
    const review = this.review;
    if (!review?.definition || !this.canImport || this.busy) {
      return;
    }
    // What was reviewed is what is imported: a draft that changed since, or a version that appeared, refuses it.
    const precondition: IRubricSavePrecondition = this.effect === 'replace-draft'
      ? { expectedChecksum: review.existingChecksum }
      : { mustNotExist: true };
    const definition = review.definition;

    this.busy = 'importing';
    this.failure = null;
    this.action = this.rubricService.saveDraft(definition, precondition).subscribe({
      next: version => {
        this.busy = null;
        void this.router.navigate(['/rubrics', version.rubricId, 'versions', version.semver]);
      },
      error: (error: RubricApiError) => {
        if (error.preconditionFailed) {
          this.failed('This rubric changed while you were reviewing it. Review it again to see what importing would do now.');
          this.review = null;
        } else {
          this.failed(error.errors.length ? `${error.message}: ${error.errors.join('; ')}` : error.message);
        }
      }
    });
  }

  /**
   * The version in the file is published or retired, which cannot change. Reviews the same definition
   * under the next version number the rubric has free.
   */
  reviewAsNextVersion(): void {
    const definition = this.definition;
    if (!definition || this.busy) {
      return;
    }
    this.busy = 'reviewing';
    this.failure = null;
    this.action = this.rubricService.getRubric(definition.id).pipe(
      switchMap(rubric => {
        const renumbered = { ...definition, semver: nextVersion((rubric.versions ?? []).map(version => version.semver)) };
        // The text follows, so what is shown is what was reviewed.
        this.text = JSON.stringify(renumbered, null, 2);
        this.fileName = null;
        return this.rubricService.validate(renumbered);
      })
    ).subscribe({
      next: result => this.reviewed(result),
      error: (error: RubricApiError) => this.failed(error.message)
    });
  }

  /** Opens the reviewed definition in the draft editor, unsaved, to put right what is wrong with it. */
  openInEditor(): void {
    const review = this.review;
    if (!review?.definition) {
      return;
    }
    const state = { [EDITOR_DEFINITION_STATE]: review.definition };
    void this.router.navigate(
      review.rubricExists ? ['/rubrics', review.definition.id, 'versions', 'new'] : ['/rubrics', '-', 'new'], { state });
  }

  private reviewed(result: IRubricValidationResult): void {
    this.busy = null;
    this.review = result;
    this.changeDetector.markForCheck();
  }

  private failed(message: string): void {
    this.busy = null;
    this.failure = message;
    this.changeDetector.markForCheck();
  }

  // The file's name says what it is when there is one; otherwise JSON starts with a brace and YAML does not.
  private format(text: string): RubricDefinitionFormat {
    if (this.fileName) {
      return /\.json$/i.test(this.fileName) ? 'json' : 'yaml';
    }
    return text.startsWith('{') ? 'json' : 'yaml';
  }
}
