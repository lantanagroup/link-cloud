import { ActivatedRoute, Router } from '@angular/router';
import { Component, OnInit } from '@angular/core';
import { FormBuilder, FormGroup, FormsModule, ReactiveFormsModule, Validators } from '@angular/forms';
import { IValidationIssueCategory, IValidationRule, IValidationRuleSet } from 'src/app/components/tenant/facility-view/report-view.interface';

import { CommonModule } from '@angular/common';
import { LinkAdminSubnavBarComponent } from 'src/app/components/core/link-admin-subnav-bar/link-admin-subnav-bar.component';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatDialog } from '@angular/material/dialog';
import { MatExpansionModule } from '@angular/material/expansion';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSelectModule } from '@angular/material/select';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MatTableModule } from '@angular/material/table';
import { RulesetAddEditDialogComponent } from '../ruleset-add-edit-dialog/ruleset-add-edit-dialog.component';
import { ValidationRuleDeleteDialogComponent } from '../validation-rule-delete-dialog/validation-rule-delete-dialog.component';
import { ValidationService } from 'src/app/services/gateway/validation/validation.service';
import { VdButtonComponent } from "../../../core/vd-button/vd-button.component";
import { VdIconComponent } from "../../../core/vd-icon/vd-icon.component";

@Component({
  selector: 'app-edit-validation-category',
  standalone: true,
  imports: [
    CommonModule,
    LinkAdminSubnavBarComponent,
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
    MatButtonModule,
    VdIconComponent,
    VdButtonComponent
],
  templateUrl: './edit-validation-category.component.html',
  styleUrls: ['./edit-validation-category.component.scss']
})
export class EditValidationCategoryComponent implements OnInit {
  categoryId: string = '';
  categoryTitle: string = '';
  categorySeverity: string = '';
  categoryAcceptable: boolean = true;
  categoryGuidance: string = '';
  categoryForm: FormGroup;
  ruleSets: IValidationRuleSet[] = [];
  ruleSetColumns = [
    { header: 'Number', key: 'ruleSetNumber' },
    { header: 'Inverted', key: 'inverted' },
    { header: 'Field', key: 'field' },
    { header: 'Regex', key: 'regex' },
    { header: '', key: 'actions' },
  ];
  ruleSetColumnKeys = this.ruleSetColumns.map(col => col.key);
  
  isLoading = false;
  error: string | null = null;

  constructor(
    private route: ActivatedRoute,
    private router: Router,
    private validationService: ValidationService,
    private fb: FormBuilder,
    private dialog: MatDialog,
    private snackBar: MatSnackBar) {
    this.categoryForm = this.fb.group({
      title: ['', Validators.required],
      severity: ['', Validators.required],
      acceptable: [false],
      guidance: [''],
      requireMatch: [false]
    });
  }

  validationCategory: IValidationIssueCategory | undefined;

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
        this.validationCategory = data;
        this.categoryTitle = this.validationCategory.title;
        this.categorySeverity = this.validationCategory.severity;
        this.categoryAcceptable = this.validationCategory.acceptable;
        this.categoryGuidance = this.validationCategory.guidance;
        
        this.categoryForm.patchValue({
          title: data.title,
          severity: data.severity,
          acceptable: data.acceptable,
          guidance: data.guidance,
          requireMatch: data.requireMatch
        });
        this.isLoading = false;
      },

      error: (error) => {
        console.error('Error loading category:', error);
        this.error = 'Failed to load category data';
        this.isLoading = false;
      }
    });
  }

  private loadRuleHistory(): void {
    this.isLoading = true;
    this.error = null;

    this.validationService.getValidationCategoryRuleHistory(this.categoryId).subscribe({
      next: (data) => {
        console.log('data ->', data);
        this.ruleSets = data.map((rule, index) => ({
          ruleSetNumber: index + 1,
          rules: [rule]
        }));
        this.isLoading = false;
      },
      error: (error) => {
        console.error('Error loading rules:', error);
        this.error = 'Failed to load rules';
        this.isLoading = false;
      }
    });
  }

  onAddRule(): void {
    let rule = null;

    const dialogRef = this.dialog.open(RulesetAddEditDialogComponent, {
      width: '830px',
      panelClass: ['vd-dialog', 'ruleset-add-edit-dialog']
    });

    // Set the properties directly on the component instance
    dialogRef.componentInstance.dialogTitle = 'Edit Rule Set';
    dialogRef.componentInstance.rules = [];

    // Get/set new rule data from form values here
  }

  onEdit(ruleSet?: IValidationRuleSet): void {
    let rule = null;
    
    if (ruleSet && ruleSet.rules && ruleSet.rules.length > 0) {
      const firstRule = ruleSet.rules[0];
      rule = {
        field: firstRule.matcher.field,
        regex: firstRule.matcher.regex
      };
    }
    
    const dialogRef = this.dialog.open(RulesetAddEditDialogComponent, {
      width: '830px',
      panelClass: ['vd-dialog', 'ruleset-add-edit-dialog']
    });
    
    // Set the properties directly on the component instance
    dialogRef.componentInstance.dialogTitle = 'Edit Rule Set';
    dialogRef.componentInstance.rules = rule ? [rule] : [];
  }

  onDelete(ruleSet?: IValidationRuleSet): void {
    if (ruleSet && ruleSet.rules && ruleSet.rules.length > 0) {
      const firstRule = ruleSet.rules[0];
      const ruleData = {
        ruleRegex: firstRule.matcher.regex,
        ruleField: firstRule.matcher.field
      };
      
      const dialogRef = this.dialog.open(ValidationRuleDeleteDialogComponent, {
        width: '380px',
        panelClass: ['vd-dialog', 'confirm-delete-dialog']
      });
      
      // Set the properties directly on the component instance
      dialogRef.componentInstance.ruleRegex = ruleData.ruleRegex;
      dialogRef.componentInstance.ruleField = ruleData.ruleField;
    }
  }

  onSubmit(): void {
    if (this.categoryForm.valid) {
      this.isLoading = true;

      const updatedCategory: IValidationIssueCategory = {
        id: this.categoryId,
        ...this.categoryForm.value
      };

      this.validationService.updateValidationCategory(this.categoryId, updatedCategory).subscribe({
        next: () => {
          this.snackBar.open('Category updated successfully', 'Close', {
            duration: 3000
          }

          );
          this.isLoading = false;
          this.router.navigate(['/validation-config/validation-categories']);
        },
        error: (error) => {
          console.error('Error updating category:', error);
          this.error = 'Failed to update category';
          this.isLoading = false;

          this.snackBar.open('Failed to update category', 'Close', {
            duration: 3000
          });
        }
      });
    }
  }

  onCancel(): void {
    this.router.navigate(['/validation-config/validation-categories']);
  }
}