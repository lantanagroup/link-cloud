import {AfterViewInit, Component, ElementRef, Inject, OnInit, ViewChild} from '@angular/core';
import {MAT_DIALOG_DATA, MatDialogRef} from '@angular/material/dialog';
import {FormBuilder, FormControl, FormGroup, ReactiveFormsModule, Validators} from '@angular/forms';
import {IOperationModel} from "../../../../interfaces/normalization/operation-get-model.interface";
import {MatButton} from "@angular/material/button";
import {MatError, MatFormField, MatInput, MatLabel} from "@angular/material/input";
import {KeyValuePipe, NgForOf, NgIf} from "@angular/common";
import {MatOption, MatSelect} from "@angular/material/select";
import {MatCard} from "@angular/material/card";
import {OperationService} from "../../../../services/gateway/normalization/operation.service";
import {resourceTypeMatchValidator} from "../../../validators/ResourceTypeMatchValidator";
import {validJsonValidator} from "../../../validators/validJsonValidator";
import {OperationType} from "../../../../interfaces/normalization/operation-type-enumeration";


@Component({
  selector: 'app-test-operation',
  templateUrl: './test-operation.component.html',
  imports: [
    ReactiveFormsModule,
    MatFormField,
    MatOption,
    MatSelect,
    MatButton,
    MatInput,
    MatFormField,
    NgIf,
    NgForOf,
    MatLabel,
    MatCard,
    MatError,
    KeyValuePipe
  ],
  styleUrls: ['./test-operation.component.scss']
})
export class TestOperationComponent implements OnInit, AfterViewInit {
  operation: IOperationModel = {} as IOperationModel;
  form!: FormGroup;
  testResult = '';
  resourceTypes: string[] = [];

  @ViewChild('jsonTextarea') jsonTextarea!: ElementRef;

  constructor(
    private fb: FormBuilder,
    private dialogRef: MatDialogRef<TestOperationComponent>,
    private operationService: OperationService,
    @Inject(MAT_DIALOG_DATA) public data: { operation: IOperationModel }
  ) {
  }

  ngOnInit() {
    this.operation = this.data.operation;

    this.form = this.fb.group({
      selectedResourceType: [null, Validators.required],
      resourceJson: [null, [Validators.required, validJsonValidator]]
    }, {
      validators: resourceTypeMatchValidator()
    });

    const operationResourceTypes = this.operation?.operationResourceTypes ?? [];

    const allResourceTypes = operationResourceTypes.map(r => r.resource?.resourceName).filter((name): name is string => typeof name === 'string') ?? [];

    this.resourceTypes = [...new Set(allResourceTypes)];

    this.resourceJsonControl?.disable();

    this.selectedResourceTypeControl.updateValueAndValidity();

    this.form.get('selectedResourceType')?.valueChanges.subscribe(value => {
      const resourceJsonControl = this.form.get('resourceJson');
      if (value) {
        resourceJsonControl?.enable();
      } else {
        resourceJsonControl?.disable();
      }
    });
  }

  get resourceJsonControl() {
    return this.form.get('resourceJson') as FormControl;
  }


  get selectedResourceTypeControl(): FormControl {
    return this.form.get('selectedResourceType') as FormControl;
  }


  ngAfterViewInit() {
    this.jsonTextarea.nativeElement.addEventListener('paste', (event: ClipboardEvent) => {
      const clipboardData = event.clipboardData;
      if (!clipboardData) return;

      const pastedText = clipboardData.getData('text');
      if (!pastedText?.trim()) return;

      try {
        const parsed = JSON.parse(pastedText);
        const pretty = JSON.stringify(parsed, null, 2);
        event.preventDefault(); // stop default paste
        const control = this.form.get('resourceJson');
        control?.setValue(pretty);
      } catch (e) {
        console.debug('Pasted content is not valid JSON, using as-is');
      }
    });
  }

  clearResource(): void {
    this.form.get('resourceJson')?.reset(); // Or use setValue('')
  }

  onJsonInputChange() {
    this.form.updateValueAndValidity();
  }

  runTest() {
    if (!this.form.valid) {
      console.warn('Form is invalid, cannot run test');
      return;
    }
    const jsonValue = this.resourceJsonControl.value;
    if (!jsonValue?.trim()) {
      console.warn('No JSON content provided');
      return;
    }
    let parsedJson;
    try {
      parsedJson = JSON.parse(jsonValue);
    } catch (error) {
      console.error('Invalid JSON format:', error);
      return;
    }

    this.operationService.testExistingOperation(this.operation.id, parsedJson).subscribe({
      next: (result) => {
        this.testResult = result?.resource ? JSON.stringify(result.resource, null, 2) : 'No result returned';
      },
      error: (err) => {
        console.error('Test operation failed:', err);
        this.testResult = `Error: ${err?.message || 'Unknown error occurred'}`;
      }
    });
  }

  onClose() {
    this.dialogRef.close();
  }

  protected readonly OperationType = OperationType;

}
