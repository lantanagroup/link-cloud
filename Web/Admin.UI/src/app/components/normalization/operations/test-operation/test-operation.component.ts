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
    MatFormField,
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

    const allResourceTypes = this.operation?.operationResourceTypes?.map(r => r.resource?.resourceName).filter(Boolean) ?? [];

    this.resourceTypes = [...new Set(allResourceTypes.filter((r): r is string => !!r))];

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
      const pastedText = clipboardData?.getData('text');

      try {
        const parsed = JSON.parse(pastedText || '');
        const pretty = JSON.stringify(parsed, null, 2);
        event.preventDefault(); // stop default paste
        const control = this.form.get('resourceJson');
        control?.setValue(pretty);
      } catch (e) {
        // do nothing if it's not valid JSON
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
    const parsedJson = JSON.parse(this.resourceJsonControl.value);
    this.operationService.testExistingOperation(this.operation.id, parsedJson).subscribe({
      next: (result) => {
        this.testResult = JSON.stringify(result.resource, null, 2);
      },
      error: (err) => {
        console.error(err);
      }
    });
  }

  onClose() {
    this.dialogRef.close();
  }

  protected readonly OperationType = OperationType;

}
