import { ActivatedRoute, Router } from '@angular/router';
import { Component, OnInit } from '@angular/core';
import { FormBuilder, FormGroup, FormsModule, ReactiveFormsModule, Validators } from '@angular/forms';
import { IValidationIssueCategory, IValidationRule, IValidationRuleSet } from 'src/app/components/tenant/facility-view/report-view.interface';

import { CommonModule } from '@angular/common';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatExpansionModule } from '@angular/material/expansion';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSelectModule } from '@angular/material/select';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MatTableModule } from '@angular/material/table';
import { ValidationService } from 'src/app/services/gateway/validation/validation.service';

@Component({
  selector: 'app-edit-validation-category',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    ReactiveFormsModule,
    MatCardModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatCheckboxModule,
    MatTableModule,
    MatProgressSpinnerModule,
    MatExpansionModule,
    MatButtonModule
  ],
  templateUrl: './edit-validation-category.component.html',
  styleUrls: ['./edit-validation-category.component.scss']
}

) export class EditValidationCategoryComponent implements OnInit {
  categoryId: string = '';
  categoryForm: FormGroup;
  ruleSets: IValidationRuleSet[] = [];
  displayedColumns: string[] = ['ruleSetNumber', 'timestamp', 'rules'];
  isLoading = false;
  error: string | null = null;

  constructor(private route: ActivatedRoute,
    private router: Router,
    private validationService: ValidationService,
    private fb: FormBuilder,
    private snackBar: MatSnackBar) {
    this.categoryForm = this.fb.group({
      title: ['', Validators.required],
      severity: ['', Validators.required],
      acceptable: [false],
      guidance: [''],
      requireMatch: [false]
    });
  }

  ngOnInit(): void {
    this.route.params.subscribe(params => {
      this.categoryId = params['id'];
      this.loadCategoryData();
      this.loadRuleHistory();
    });
  }

  private loadCategoryData(): void {
    this.isLoading = true;
    this.error = null;

    this.validationService.getValidationCategory(this.categoryId).subscribe({
      next: (data) => {
        this.categoryForm.patchValue({
          title: data.title,
          severity: data.severity,
          acceptable: data.acceptable,
          guidance: data.guidance,
          requireMatch: data.requireMatch
        }

        );
        this.isLoading = false;
      }

      ,
      error: (error) => {
        console.error('Error loading category:', error);
        this.error = 'Failed to load category data';
        this.isLoading = false;
      }
    }

    );
  }

  private loadRuleHistory(): void {
    this.isLoading = true;
    this.error = null;

    this.validationService.getValidationCategoryRuleHistory(this.categoryId).subscribe({
      next: (data) => {
        this.ruleSets = data.map((rule, index) => ({
          ruleSetNumber: index + 1,
          rules: [rule]
        }

        ));
        this.isLoading = false;
      }

      ,
      error: (error) => {
        console.error('Error loading rule history:', error);
        this.error = 'Failed to load rule history';
        this.isLoading = false;
      }
    }

    );
  }

  onSubmit(): void {
    if (this.categoryForm.valid) {
      this.isLoading = true;

      const updatedCategory: IValidationIssueCategory = {
        id: this.categoryId,
        ...this.categoryForm.value
      }

        ;

      this.validationService.updateValidationCategory(this.categoryId, updatedCategory).subscribe({
        next: () => {
          this.snackBar.open('Category updated successfully', 'Close', {
            duration: 3000
          }

          );
          this.isLoading = false;
          this.router.navigate(['/validation-config/validation-categories']);
        }

        ,
        error: (error) => {
          console.error('Error updating category:', error);
          this.error = 'Failed to update category';
          this.isLoading = false;

          this.snackBar.open('Failed to update category', 'Close', {
            duration: 3000
          }

          );
        }
      }

      );
    }
  }

  onCancel(): void {
    this.router.navigate(['/validation-config/validation-categories']);
  }
}