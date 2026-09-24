import {AfterViewInit, Component, DestroyRef, OnInit, ViewChild, inject} from '@angular/core';
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

  selectFile(event: Event): void {
    const input = event.target as HTMLInputElement;
    this.file = input.files?.[0] ?? null;
    this.fileError = '';
    if (this.file && (!this.file.name.toLowerCase().endsWith('.csv') || this.file.size === 0)) {
      this.fileError = 'Select a non-empty CSV file.';
      this.file = null;
      input.value = '';
    }
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