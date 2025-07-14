import {Component, ElementRef, Inject, OnDestroy, OnInit, ViewChild} from '@angular/core';
import {
  FormArray,
  FormBuilder,
  FormControl,
  FormGroup,
  FormsModule,
  ReactiveFormsModule,
  Validators
} from '@angular/forms';
import {MatFormField, MatSuffix} from '@angular/material/form-field';
import {MatOption, MatSelect} from '@angular/material/select';
import {MatTableDataSource
} from '@angular/material/table';
import {MatLabel} from '@angular/material/input';
import {MatCard,} from '@angular/material/card';
import {JsonPipe, NgForOf, NgIf} from "@angular/common";
import {MAT_DIALOG_DATA, MatDialogRef} from "@angular/material/dialog";
import {MatSnackBar} from "@angular/material/snack-bar";
import {OperationService} from "../../../../services/gateway/normalization/operation.service";
import {
  IOperationModel
} from "../../../../interfaces/normalization/operation-get-model.interface";
import {PaginationMetadata} from "../../../../models/pagination-metadata.model";
import {Subject} from "rxjs";
import {MatButton, MatIconButton} from "@angular/material/button";
import {IVendor} from "../../../../interfaces/normalization/vendor-interface";
import {IOperationSequenceModel} from "../../../../interfaces/normalization/operation-sequence-model.interface";
import {MatIcon} from "@angular/material/icon";
import {MatTooltip} from "@angular/material/tooltip";


@Component({
  selector: 'app-operations-sequence',
  templateUrl: './operations-sequence.component.html',
  imports: [
    ReactiveFormsModule,
    MatSelect,
    MatFormField,
    MatOption,
    MatCard,
    NgForOf,
    NgIf,
    MatLabel,
    FormsModule,
    MatButton,
    MatIconButton,
    MatIcon,
    JsonPipe,
    MatTooltip,
    MatSuffix
  ],
  styleUrls: ['./operations-sequence.component.scss']
})
export class OperationsSequenceComponent implements OnInit, OnDestroy {

  @ViewChild('duplicateErrorMsg') duplicateErrorMsg!: ElementRef<HTMLDivElement>;

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

  isVendorLocked = false;

  showDetailsMap: boolean[] = [];

  constructor(
    private fb: FormBuilder,
    private snackBar: MatSnackBar,
    private operationService: OperationService,
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

    this.loadVendors();
    this.showDetailsMap = this.operationsArray.controls.map(() => false);
    // Watch for vendor selection to load resource types associated with operations
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
            this.loadOperations();
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

  loadVendors(): void {
    this.operationService.getVendors().subscribe({
      next: (vendors: IVendor[]) => {
        this.vendorFilterOptions = vendors.reduce((acc, vendor) => {
          acc[vendor.id] = vendor.name;
          return acc;
        }, {} as Record<string, string>);
        this.vendorIds = vendors.map(v => v.id);

        // After vendors loaded, load sequences to check usage
        this.loadSequencesAndSetVendor();
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
  }

  loadSequencesAndSetVendor(): void {
    this.operationService.getOperationSequences(this.data.facilityId).subscribe({
      next: (sequences) => {
        // this.sequences = sequences;

        const usedVendorIds = new Set<string>();

        sequences.forEach(seq => {
          seq.vendorPresets?.forEach((preset) => {
            const vendorId = preset.vendorVersion?.vendor?.id;
            if (vendorId) {
              usedVendorIds.add(vendorId);
            }
          });
        });

        if (usedVendorIds.size === 1) {
          // Only one vendor used in sequences, lock dropdown and auto-select
          const [onlyVendorId] = Array.from(usedVendorIds);
          this.form.get('selectedVendorId')?.setValue(onlyVendorId);
          this.isVendorLocked = true;
        } else {
          // No vendor or multiple vendors in sequences — enable dropdown, no auto select
          this.isVendorLocked = false;
          this.form.get('selectedVendorId')?.reset();
        }
      },
      error: () => {
        this.snackBar.open('Failed to load sequences', '', {
          duration: 3500,
          panelClass: 'error-snackbar',
          horizontalPosition: 'end',
          verticalPosition: 'top'
        });
      }
    });
  }

  loadOperations(): void {
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
  }


  loadSequences(): void {

    const resourceType = this.selectedResourceTypeControl.value;

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


  get selectedResourceTypeControl(): FormControl {
    return this.form.get('selectedResourceType') as FormControl;
  }

  get vendorTypeControl(): FormControl {
    return this.form.get('selectedVendorId') as FormControl;
  }


  onClose(): void {
    this.dialogRef.close();
  }

  hasDuplicateSequences(): boolean {
    const sequences = this.form.value.operations?.map((op: {
      sequence: any;
    }) => op.sequence).filter((seq: any) => seq !== null && seq !== undefined && seq !== '') ?? [];
    const uniqueSequences = new Set(sequences);
    return uniqueSequences.size !== sequences.length;
  }

  onSave(): void {
    if (this.form.invalid  || this.hasDuplicateSequences()) {
      this.form.markAllAsTouched();

      if (this.hasDuplicateSequences() && this.duplicateErrorMsg) {
        this.duplicateErrorMsg.nativeElement.scrollIntoView({ behavior: 'smooth', block: 'center' });
      }

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

  toggleDetails(index: number): void {
    this.showDetailsMap[index] = !this.showDetailsMap[index];
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
      // Ensure vendorId and vendorName are populated from vendorPresets
      if (!op.vendorId) {
        const vendor = op.vendorPresets?.[0]?.vendorVersion?.vendor;
        op.vendorId = vendor?.id ?? '';
        op.vendorName = vendor?.name ?? 'Facility Only';
      } else if (!op.vendorName) {
        op.vendorName = this.vendorFilterOptions[op.vendorId] ?? 'Facility Only';
      }
    });

    const operationControls = ops.map(op =>
      this.fb.group({
        operationId: [op.id],
        operationName: [op.parsedOperationJson.Name ?? ''],
        operationType: [op.operationType],
        vendorId: [op.vendorId ?? ''],
        vendorName: [op.vendorName ?? 'Facility Only'],
        parsedOperationJson: [op.parsedOperationJson],
        sequence: [op.sequence !== 0 && op.sequence !== undefined ? op.sequence : '',  [Validators.min(1)]]
      })
    );

    this.operationsArray.clear();
    operationControls.forEach(ctrl => this.operationsArray.push(ctrl));
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete()
  }

}
