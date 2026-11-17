import {AfterViewInit, ChangeDetectorRef, Component, DestroyRef, OnInit, ViewChild, inject} from '@angular/core';
import {takeUntilDestroyed} from '@angular/core/rxjs-interop';
import {FormsModule} from '@angular/forms';
import {MatButtonModule} from '@angular/material/button';
import {MatCheckboxModule} from '@angular/material/checkbox';
import {MatDialog, MatDialogModule} from '@angular/material/dialog';
import {MatFormFieldModule} from '@angular/material/form-field';
import {MatIconModule} from '@angular/material/icon';
import {MatInputModule} from '@angular/material/input';
import {MatPaginator, MatPaginatorModule} from '@angular/material/paginator';
import {MatProgressBarModule} from '@angular/material/progress-bar';
import {MatSelectModule} from '@angular/material/select';
import {MatSort, MatSortModule} from '@angular/material/sort';
import {MatTableDataSource, MatTableModule} from '@angular/material/table';
import {MatTooltipModule} from '@angular/material/tooltip';
import {finalize} from 'rxjs';
import {Hsloc, HslocService} from '../../services/gateway/normalization/hsloc.service';
import {DeleteConfirmationDialogComponent} from '../core/delete-confirmation-dialog/delete-confirmation-dialog.component';

/** Columns required by the HSLOC import (see HSLOCManager.ParseCsv). Header names are case-insensitive and may appear in any order. */
export const HSLOC_CSV_COLUMNS: readonly {name: string; description: string}[] = [
  {name: 'CDCCode', description: 'NHSN CDC location code, for example IN:ACUTE:CC:M'},
  {name: 'ShortDescription', description: 'Short location description'},
  {name: 'HSLOCCode', description: 'HL7 HSLOC code, for example 1027-2'},
  {name: 'LongDescription', description: 'Full location description'}
];

export const HSLOC_FORMAT_ERROR = 'This file is not in the required HSLOC format. The CSV downloaded from the CDC website ' +
  'must be reformatted before upload so its header row contains exactly these columns: ' +
  HSLOC_CSV_COLUMNS.map(column => column.name).join(', ') + '.';

/** Only the start of the file is read on the client, so very large files are never loaded into memory just to check the header. */
const HEADER_SAMPLE_BYTES = 64 * 1024;

/** Returns true when the first line of the CSV contains exactly the required columns. */
export function hasHslocHeader(text: string): boolean {
  const firstLine = text.replace(/^\uFEFF/, '').split(/\r?\n/, 1)[0] ?? '';
  const headers = firstLine.split(',').map(header => header.trim().replace(/^"(.*)"$/, '$1').trim().toLowerCase());
  const required = HSLOC_CSV_COLUMNS.map(column => column.name.toLowerCase());
  return headers.length === required.length && new Set(headers).size === headers.length &&
    required.every(name => headers.includes(name));
}

@Component({
  selector: 'app-hsloc',
  standalone: true,
  imports: [FormsModule, MatButtonModule, MatCheckboxModule, MatDialogModule, MatFormFieldModule,
    MatIconModule, MatInputModule, MatPaginatorModule, MatProgressBarModule, MatSelectModule,
    MatSortModule, MatTableModule, MatTooltipModule],
  templateUrl: './hsloc.component.html',
  styleUrls: ['./hsloc.component.scss']
})
export class HslocComponent implements OnInit, AfterViewInit {
  private readonly destroyRef = inject(DestroyRef);
  private readonly changeDetector = inject(ChangeDetectorRef);
  readonly csvColumns = HSLOC_CSV_COLUMNS;
  readonly sampleCsv = HSLOC_CSV_COLUMNS.map(column => column.name).join(',') +
    '\nIN:ACUTE:CC:M,Medical Critical Care,1027-2,Medical Critical Care';
  private fileCheck = 0;
  checkingFile = false;
  readonly dataSource = new MatTableDataSource<Hsloc>([]);
  readonly columns = ['hslocCode', 'cdcCode', 'shortDescription', 'longDescription', 'version', 'isActive', 'actions'];
  @ViewChild(MatPaginator) paginator!: MatPaginator;
  @ViewChild(MatSort) sort!: MatSort;
  search = '';
  version = '';
  includeInactive = false;
  versions: string[] = [];
  oldVersion = '';
  newVersion = '';
  file: File | null = null;
  busy = false;
  loaded = false;
  loadFailed = false;
  fileError = '';
  success = '';

  constructor(private service: HslocService, private dialog: MatDialog) {
    this.dataSource.filterPredicate = (row, filter) => {
      const criteria = JSON.parse(filter);
      return (criteria.includeInactive || row.isActive) &&
        (!criteria.version || row.version === criteria.version) &&
        [row.hslocCode, row.cdcCode, row.shortDescription, row.longDescription, row.version]
          .some(value => value.toLowerCase().includes(criteria.search));
    };
  }

  ngOnInit(): void {
    this.load();
  }

  ngAfterViewInit(): void {
    this.dataSource.paginator = this.paginator;
    this.dataSource.sort = this.sort;
  }

  load(): void {
    this.busy = true;
    this.loadFailed = false;
    this.service.getAll().pipe(takeUntilDestroyed(this.destroyRef), finalize(() => this.busy = false)).subscribe({
      next: rows => {
        this.loaded = true;
        this.dataSource.data = rows;
        this.versions = [...new Set(rows.map(row => row.version))].sort();
        if (!this.versions.includes(this.version)) this.version = '';
        this.applyFilter();
      },
      error: () => {
        this.loaded = false;
        this.dataSource.data = [];
        this.versions = [];
        this.success = '';
        this.loadFailed = true;
      }
    });
  }

  applyFilter(): void {
    this.dataSource.filter = JSON.stringify({search: this.search.trim().toLowerCase(),
      version: this.version, includeInactive: this.includeInactive});
    this.dataSource.paginator?.firstPage();
  }

  async selectFile(event: Event): Promise<void> {
    const input = event.target as HTMLInputElement;
    const selected = input.files?.[0] ?? null;
    const check = ++this.fileCheck;
    this.file = null;
    this.fileError = '';
    this.checkingFile = false;
    if (!selected) return;
    if (!selected.name.toLowerCase().endsWith('.csv') || selected.size === 0) {
      this.fileError = 'Select a non-empty CSV file.';
      input.value = '';
      return;
    }
    this.checkingFile = true;
    let error = '';
    try {
      if (!hasHslocHeader(await selected.slice(0, HEADER_SAMPLE_BYTES).text())) error = HSLOC_FORMAT_ERROR;
    } catch {
      error = 'Unable to read the selected file.';
    }
    if (check !== this.fileCheck) return;
    this.checkingFile = false;
    if (error) {
      this.fileError = error;
      input.value = '';
    } else {
      this.file = selected;
    }
    this.changeDetector.markForCheck();
  }

  upload(fileInput: HTMLInputElement): void {
    if (this.busy || !this.loaded || !this.file || !this.oldVersion.trim() || !this.newVersion.trim()) return;
    this.busy = true;
    this.success = '';
    this.service.update(this.oldVersion.trim(), this.newVersion.trim(), this.file)
      .pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
        next: () => {
          this.success = 'HSLOC codes updated.';
          this.oldVersion = this.newVersion.trim();
          this.newVersion = '';
          this.file = null;
          fileInput.value = '';
          this.load();
        },
        error: () => {
          this.busy = false;
        }
      });
  }

  deleteCode(row: Hsloc): void {
    if (this.busy) return;
    this.dialog.open(DeleteConfirmationDialogComponent, {
      width: '450px',
      data: {title: 'Delete HSLOC Code', message: `Delete HSLOC code ${row.hslocCode} (version ${row.version})? This cannot be undone.`}
    }).afterClosed().pipe(takeUntilDestroyed(this.destroyRef)).subscribe(confirmed => {
      if (!confirmed || this.busy) return;
      this.busy = true;
      this.success = '';
      this.service.delete(row.id).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
        next: () => {
          this.success = 'HSLOC code deleted.';
          this.load();
        },
        error: () => {
          this.busy = false;
        }
      });
    });
  }
}