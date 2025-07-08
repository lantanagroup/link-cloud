import {Component, Inject, OnDestroy, OnInit} from '@angular/core';
import {
  FormArray,
  FormBuilder,
  FormControl,
  FormGroup,
  FormsModule,
  ReactiveFormsModule,
  Validators
} from '@angular/forms';
import {MatFormField, MatFormFieldModule} from '@angular/material/form-field';
import {MatOption, MatSelect, MatSelectModule} from '@angular/material/select';
import {
  MatCell, MatCellDef,
  MatColumnDef,
  MatHeaderCell, MatHeaderCellDef,
  MatHeaderRow, MatHeaderRowDef,
  MatRow, MatRowDef,
  MatTable, MatTableDataSource
} from '@angular/material/table';
import {MatInput, MatInputModule, MatLabel} from '@angular/material/input';
import {MatCard, MatCardModule} from '@angular/material/card';
import {JsonPipe, NgForOf, NgIf} from "@angular/common";
import {MAT_DIALOG_DATA, MatDialog, MatDialogRef} from "@angular/material/dialog";
import {MatSnackBar} from "@angular/material/snack-bar";
import {OperationService} from "../../../../services/gateway/normalization/operation.service";
import {
  IOperationModel
} from "../../../../interfaces/normalization/operation-get-model.interface";
import {PaginationMetadata} from "../../../../models/pagination-metadata.model";
import {Observable, Subject, takeUntil} from "rxjs";
import {MatButton} from "@angular/material/button";
import {IVendor} from "../../../../interfaces/normalization/vendor-interface";
import {IOperationSequenceModel} from "../../../../interfaces/normalization/operation-sequence-model.interface";
import { ChangeDetectorRef } from '@angular/core';

@Component({
  selector: 'app-operations-sequence',
  templateUrl: './operations-sequence.component.html',
  imports: [
    MatTable,
    ReactiveFormsModule,
    MatSelect,
    MatFormField,
    MatOption,
    MatCard,
    MatHeaderCell,
    MatCell,
    MatColumnDef,
    MatHeaderRow,
    MatRow,
    MatInput,
    MatCellDef,
    MatHeaderCellDef,
    NgForOf,
    MatHeaderRowDef,
    MatRowDef,
    NgIf,
    MatLabel,
    FormsModule,
    MatButton
  ],
  styleUrls: ['./operations-sequence.component.scss']
})
export class OperationsSequenceComponent implements OnInit, OnDestroy {

  displayedColumns = ['type', 'name', 'id', 'sequence'];
  operations: IOperationModel[] = [];

  dataSource = new MatTableDataSource<IOperationModel>(this.operations);

  paginationMetadata: PaginationMetadata = new PaginationMetadata;

  resourceTypes: string[] = [];

  form!: FormGroup;

  operationsArray: FormArray;

  destroy$ = new Subject<void>();

  vendorFilterOptions: Record<string, string> = {};

  vendorIds: string[] = [];

  constructor(
    private fb: FormBuilder,
    private snackBar: MatSnackBar,
    private operationService: OperationService,
    private cdr: ChangeDetectorRef,
    @Inject(MAT_DIALOG_DATA) public data: any,
    private dialogRef: MatDialogRef<OperationsSequenceComponent>,
  ) {
    this.form = this.fb.group({
      selectedVendorId: new FormControl('', Validators.required),
      selectedResourceType: new FormControl('', Validators.required),
      operations: this.fb.array([])  // operations is a FormArray
    });
    this.operationsArray = this.form.get('operations') as FormArray;
  }


  ngOnInit(): void {

    this.operationService.getVendors().subscribe({
      next: (vendors: IVendor[]) => {
        this.vendorFilterOptions = vendors.reduce((acc, vendor) => {
          acc[vendor.id] = vendor.name;
          return acc;
        }, {} as Record<string, string>);
        this.vendorIds = vendors.map(v => v.id);
      },
      error: () => {
        this.snackBar.open('Failed to load vendors', '', {
          duration: 3500,
          panelClass: 'error-snackbar',
          horizontalPosition: 'end',
          verticalPosition: 'top'
        });
      }
    });


    // Watch for vendor selection to load resource types
    this.form.get('selectedVendorId')?.valueChanges.subscribe((vendorId: string) => {
      if (!vendorId) {
        this.resourceTypes = [];
        this.form.get('selectedResourceType')?.reset();
        return;
      }

      const selectedVendor = this.form.get('selectedVendorId')?.value;

      this.operationService.getOperationsByFacility(this.data.facilityId, selectedVendor).subscribe({
        next: (operationsSearch) => {
          // Filter operations by selected vendor
          this.operations = operationsSearch.records;

          const resourceNames = this.operations
            .flatMap(op => op.operationResourceTypes)
            .map(rt => rt.resource?.resourceName)
            .filter((name): name is string => !!name);

          this.resourceTypes = [...new Set(resourceNames)].sort();

          // Auto-select the first resource type
          if (this.resourceTypes.length > 0) {
            this.form.get('selectedResourceType')?.setValue(this.resourceTypes[0]);
            this.onResourceTypeSelected(this.resourceTypes[0]);
          } else {
            this.form.get('selectedResourceType')?.reset();
          }
        },
        error: () => {
          this.snackBar.open('Failed to load resource types', '', {
            duration: 3500,
            panelClass: 'error-snackbar',
            horizontalPosition: 'end',
            verticalPosition: 'top'
          });
        }
      });
    });

    this.selectedResourceTypeControl.valueChanges.subscribe((types) => {
      if (types && types.length > 0) {
        this.paginationMetadata.pageNumber = 0;
        this.loadOperations();
      }
    });
  }

  get hasSequencesAssigned(): boolean {
    // Check if any operation has a non-zero (or non-null) sequence assigned
    return this.operationsArray.controls.some(ctrl => {
      const seq = ctrl.get('sequence')?.value;
      return seq !== null && seq !== undefined && seq !== 0;
    });
  }

  onResourceTypeSelected(resourceType: string) {
    // Filter allOperations to only those that have the selected resourceType
    this.operations.filter(op =>
      op.operationResourceTypes.some(rt => rt.resource?.resourceName === resourceType)
    );

    // Reset the selected operation control because the filtered list changed
    this.form.get('selectedOperation')?.reset();
  }

  get selectedResourceTypeControl(): FormControl {
    return this.form.get('selectedResourceType') as FormControl;
  }

  get vendorTypeControl(): FormControl {
    return this.form.get('selectedVendorId') as FormControl;
  }

  loadSequences(): void {
    const resourceType = this.selectedResourceTypeControl.value;
    const selectedVendor = this.form.get('selectedVendorId')?.value;

    this.operationService.getOperationSequences(this.data.facilityId, resourceType).subscribe({
      next: (sequences: IOperationSequenceModel[]) => {
        const sequenceMap = new Map<string, number>();
        sequences.forEach(seq => {
          const opId = seq.operationResourceType?.operationId;
          if (opId) {
            sequenceMap.set(opId, seq.sequence);
          }
        });

        const operationsWithSequence = this.operations
          .map(op => ({
            ...op,
            sequence: sequenceMap.get(op.id) ?? 0
          }))
          .sort((a, b) => a.sequence - b.sequence);

        this.dataSource.data = operationsWithSequence;
        this.setOperations(operationsWithSequence);

        // Disable vendor selection if any sequence is > 0
        const hasSequencesAssigned = operationsWithSequence.some(op => (op.sequence ?? 0) > 0);
        const vendorControl = this.form.get('selectedVendorId');
        if (hasSequencesAssigned) {
          vendorControl?.disable({ emitEvent: false });
        } else {
          vendorControl?.enable({ emitEvent: false });
        }
      },
      error: (err) => {
        this.snackBar.open('Failed to load operation sequences', '', {
          duration: 3000,
          panelClass: 'error-snackbar',
          horizontalPosition: 'end',
          verticalPosition: 'top'
        });
      }
    });

  }

  loadOperations(): void {
    const resourceType = this.selectedResourceTypeControl.value;
    const selectedVendor = this.vendorTypeControl.value;

    // 1. Load all operations for the vendor + resource type
    this.operationService.getOperationsByFacility(
      this.data.facilityId, selectedVendor, resourceType
    ).subscribe({
      next: (vendorOpsResult) => {
        const vendorOps = vendorOpsResult.records;
        this.paginationMetadata = vendorOpsResult.metadata;

        // 2. Load all sequences (regardless of vendor/resource type)
        this.operationService.getOperationSequences(this.data.facilityId, resourceType).subscribe({
          next: (sequences: IOperationSequenceModel[]) => {
            const sequenceMap = new Map<string, number>();
            const sequenceOpIds = new Set<string>();

            sequences.forEach(seq => {
              const opId = seq.operationResourceType?.operationId;
              if (opId) {
                sequenceMap.set(opId, seq.sequence);
                sequenceOpIds.add(opId);
              }
            });

            // 3. Extract all unique operation IDs from sequences
            const uniqueSequencedOps: IOperationModel[] = this.operations.filter(op => sequenceOpIds.has(op.id));

            // 4. Merge vendorOps and sequencedOps without duplicates
            const combined = [...vendorOps, ...uniqueSequencedOps];
            const mergedMap = new Map<string, IOperationModel>();

            combined.forEach(op => {
              mergedMap.set(op.id, {
                ...op,
                sequence: sequenceMap.get(op.id) ?? 0
              });
            });

            const finalOps = Array.from(mergedMap.values()).sort((a, b) => (a.sequence ?? 0) - (b.sequence ?? 0));

            // 5. Update table and form
            this.operations = finalOps;
            this.dataSource.data = finalOps;
            this.setOperations(finalOps);
          },
          error: () => {
            this.snackBar.open('Failed to load operation sequences', '', {
              duration: 3000,
              panelClass: 'error-snackbar',
              horizontalPosition: 'end',
              verticalPosition: 'top'
            });
          }
        });
      },
      error: (error) => {
        console.error('Error loading vendor operations:', error);
      }
    });
  }


  /*loadOperations(): void {
    const resourceType = this.selectedResourceTypeControl.value; // array of strings

    const selectedVendor = this.form.get('selectedVendorId')?.value;

    this.operationService.getOperationsByFacility(
      this.data.facilityId, selectedVendor, resourceType
    ).subscribe({
      next: (operationsSearch) => {
        this.operations = operationsSearch.records;
        this.paginationMetadata = operationsSearch.metadata;
        this.loadSequences();
      },
      error: (error) => {
        console.error('Error loading operations:', error);
      }
    });
  }*/

  onClose(): void {
    this.dialogRef.close({updatedSequences: this.operations});
  }

  hasDuplicateSequences(): boolean {
    const sequences = this.form.value.operations?.map((op: { sequence: any; }) => op.sequence) ?? [];
    const uniqueSequences = new Set(sequences);
    return uniqueSequences.size !== sequences.length;
  }

  onSave(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const operations = this.form.value.operations.filter((op: { sequence: string | null | undefined; }) => {
      return op.sequence !== null && op.sequence !== undefined && op.sequence !== '';
    });

    this.operationService.saveOperationSequences(this.data.facilityId, this.form.value.selectedResourceType, operations).subscribe({
      next: () => {
        this.snackBar.open('Operation sequence saved successfully', 'Close', {duration: 3000});
        this.operations = [];
        this.loadOperations();
      },
      error: (err) => {
        console.error(err);
        this.snackBar.open('Failed to save operation sequence', 'Close', {duration: 3000});
      }
    });
  }

  setOperations(ops: any[]): void {
    ops.forEach(op => {
      if (!op.parsedOperationJson && op.operationJson) {
        try {
          op.parsedOperationJson = JSON.parse(op.operationJson);
        } catch {
          op.parsedOperationJson = {};
        }
      }
    });

    const operationControls = ops.map(op =>
      this.fb.group({
        operationId: [op.id],
        operationName: [op.parsedOperationJson.Name ?? ''],
        operationType: [op.operationType],
        sequence: [op.sequence??0]   // <-- use merged sequence here
      })
    );

    this.operationsArray.clear();
    operationControls.forEach(ctrl => this.operationsArray.push(ctrl));

    // 🧠 Force UI update
    this.cdr.detectChanges();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete()
  }

}
