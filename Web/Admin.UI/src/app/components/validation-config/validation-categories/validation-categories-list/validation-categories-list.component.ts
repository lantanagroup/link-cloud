import { Component, OnInit, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { MatTableModule, MatTableDataSource } from '@angular/material/table';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { IValidationCategory } from 'src/app/interfaces/validation/validation-category.interface';
import { ValidationService } from 'src/app/services/gateway/validation/validation.service';
import { Subject } from 'rxjs';
import { takeUntil } from 'rxjs/operators';

@Component({
  selector: 'app-validation-categories-list',
  standalone: true,
  imports: [CommonModule, RouterModule, MatTableModule, MatButtonModule, MatIconModule],
  templateUrl: './validation-categories-list.component.html',
  styleUrls: ['./validation-categories-list.component.scss']
})
export class ValidationCategoriesListComponent implements OnInit, OnDestroy {
  categories: MatTableDataSource<IValidationCategory>;
  displayedColumns: string[] = ['title', 'severity', 'acceptable', 'guidance', 'actions'];
  private destroy$ = new Subject<void>();

  constructor(private validationService: ValidationService) {
    this.categories = new MatTableDataSource<IValidationCategory>([]);
  }

  ngOnInit(): void {
    this.loadCategories();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  private loadCategories(): void {
    this.validationService.getValidationCategories()
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (categories) => {
          this.categories.data = categories;
        },
        error: (error) => {
          console.error('Error loading validation categories:', error);
        }
      });
  }
}