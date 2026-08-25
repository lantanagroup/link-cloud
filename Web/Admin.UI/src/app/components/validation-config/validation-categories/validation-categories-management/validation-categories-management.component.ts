import { CommonModule } from '@angular/common';
import { Component, OnInit, ViewChild } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatSort, MatSortModule } from '@angular/material/sort';
import { MatTableDataSource, MatTableModule } from '@angular/material/table';
import { RouterModule } from '@angular/router';
import { ValidationService } from 'src/app/services/gateway/validation/validation.service';
import { ErrorHandlingService } from 'src/app/services/error-handling.service';
import { OperationJsonDialogComponent } from 'src/app/components/normalization/operations/operations-list/operation-json-dialog-component';
import { ValidationCategoryBulkImportDialogComponent } from '../validation-category-bulk-import-dialog/validation-category-bulk-import-dialog.component';
import { DeleteConfirmationDialogComponent } from 'src/app/components/core/delete-confirmation-dialog/delete-confirmation-dialog.component';

interface ICategorySnapshot {
  id: string;
  title: string;
  severity: string;
  acceptable: boolean;
  submit: boolean;
  review: boolean;
  guidance: string;
  matcher: any;
}

@Component({
  selector: 'app-validation-categories-management',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    RouterModule,
    MatTableModule,
    MatSortModule,
    MatFormFieldModule,
    MatInputModule,
    MatButtonModule,
    MatIconModule,
    MatDialogModule,
    MatSnackBarModule,
    DeleteConfirmationDialogComponent
  ],
  templateUrl: './validation-categories-management.component.html',
  styleUrls: ['./validation-categories-management.component.scss']
})
export class ValidationCategoriesManagementComponent implements OnInit {
  @ViewChild('sort', { static: true }) sort: MatSort = new MatSort();

  dataSource = new MatTableDataSource<ICategorySnapshot>([]);
  filterText = '';

  columns = [
    { header: 'ID', key: 'id' },
    { header: 'Category', key: 'title' },
    { header: 'Severity', key: 'severity' },
    { header: 'Acceptable', key: 'acceptable' },
    { header: 'Submit', key: 'submit' },
    { header: 'Review', key: 'review' },
    { header: 'Guidance', key: 'guidance' },
    { header: 'Rules', key: 'rules' }
  ];

  columnKeys = this.columns.map(col => col.key);

  constructor(
    private validationService: ValidationService,
    private errorHandlingService: ErrorHandlingService,
    private dialog: MatDialog,
    private snackBar: MatSnackBar
  ) {}

  ngOnInit(): void {
    this.dataSource.sort = this.sort;
    this.dataSource.filterPredicate = (data: ICategorySnapshot, filter: string) => {
      const search = filter.trim().toLowerCase();
      if (!search) {
        return true;
      }
      const matcherJson = JSON.stringify(data.matcher || {}).toLowerCase();
      return [
        data.id,
        data.title,
        data.severity,
        data.guidance,
        data.acceptable ? 'yes' : 'no',
        data.submit ? 'yes' : 'no',
        data.review ? 'yes' : 'no',
        matcherJson
      ]
        .filter(Boolean)
        .some(value => value.toString().toLowerCase().includes(search));
    };

    this.loadCategories();
  }

  loadCategories(): void {
    this.validationService.getValidationCategoriesBulkExport().subscribe({
      next: (categories) => {
        this.dataSource.data = categories as ICategorySnapshot[];
      },
      error: (error) => {
        this.snackBar.open(this.formatProblemDetails(error, 'Failed to load categories.'), '', {
          duration: 5000,
          panelClass: 'error-snackbar',
          horizontalPosition: 'end',
          verticalPosition: 'top'
        });
      }
    });
  }

  applyFilter(): void {
    this.dataSource.filter = this.filterText.trim().toLowerCase();
  }

  openRulesDialog(matcher: any): void {
    this.dialog.open(OperationJsonDialogComponent, {
      width: '70%',
      maxWidth: '900px',
      data: matcher ?? {}
    });
  }

  confirmInitializeCategories(): void {
    const dialogRef = this.dialog.open(DeleteConfirmationDialogComponent, {
      width: '420px',
      data: {
        title: 'Reset validation categories',
        message: 'This will reset the validation categories to the default values that shipped with the codebase.',
        confirmButtonText: 'Reset',
        icon: 'restore',
        iconColor: 'primary'
      }
    });

    dialogRef.afterClosed().subscribe((confirmed: boolean) => {
      if (!confirmed) {
        return;
      }
      this.initializeCategories();
    });
  }

  initializeCategories(): void {
    this.validationService.initializeValidationCategories().subscribe({
      next: () => {
        this.snackBar.open('$initialize completed successfully', '', {
          duration: 3500,
          panelClass: 'success-snackbar',
          horizontalPosition: 'end',
          verticalPosition: 'top'
        });
        this.loadCategories();
      },
      error: (error) => {
        this.snackBar.open(this.formatProblemDetails(error, '$initialize failed.'), '', {
          duration: 5000,
          panelClass: 'error-snackbar',
          horizontalPosition: 'end',
          verticalPosition: 'top'
        });
      }
    });
  }

  openBulkImport(): void {
    const dialogRef = this.dialog.open(ValidationCategoryBulkImportDialogComponent, {
      width: '75%',
      maxWidth: '900px'
    });

    dialogRef.afterClosed().subscribe((categories: any[] | undefined) => {
      if (!categories) {
        return;
      }
      this.validationService.bulkImportValidationCategories(categories).subscribe({
        next: () => {
          this.snackBar.open('$bulk-import completed successfully', '', {
            duration: 3500,
            panelClass: 'success-snackbar',
            horizontalPosition: 'end',
            verticalPosition: 'top'
          });
          this.loadCategories();
        },
        error: (error) => {
          this.snackBar.open(this.formatProblemDetails(error, '$bulk-import failed.'), '', {
            duration: 5000,
            panelClass: 'error-snackbar',
            horizontalPosition: 'end',
            verticalPosition: 'top'
          });
        }
      });
    });
  }

  exportCategories(): void {
    this.validationService.getValidationCategoriesBulkExport().subscribe({
      next: (categories) => {
        this.downloadJson(categories, 'validation-categories.json');
      },
      error: (error) => {
        this.snackBar.open(this.formatProblemDetails(error, '$bulk-export failed.'), '', {
          duration: 5000,
          panelClass: 'error-snackbar',
          horizontalPosition: 'end',
          verticalPosition: 'top'
        });
      }
    });
  }

  private formatProblemDetails(error: any, fallbackDetail: string): string {
    return this.errorHandlingService.formatError(error, fallbackDetail);
  }

  private downloadJson(data: any, fileName: string): void {
    const jsonContent = JSON.stringify(data, null, 2);
    const blob = new Blob([jsonContent], { type: 'application/json' });
    const blobUrl = window.URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = blobUrl;
    link.download = fileName;
    link.style.display = 'none';
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
    setTimeout(() => window.URL.revokeObjectURL(blobUrl));
  }
}
