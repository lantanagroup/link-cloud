import {Component, Inject, OnInit, ViewChild} from '@angular/core';
import {MAT_DIALOG_DATA, MatDialogModule, MatDialogRef} from "@angular/material/dialog";
import {MatSnackBar} from "@angular/material/snack-bar";
import {IEntityCreatedResponse} from "../../../../interfaces/entity-created-response.model";
import {NormalizationFormComponent} from "../../normalization-config/normalization.component";

import {MatButtonModule} from "@angular/material/button";
import {MatIconModule} from "@angular/material/icon";
import {FormMode} from '../../../../models/FormMode.enum';
import {CopyPropertyComponent} from "../copy-property/copy-property.component";
import {IOperationModel} from "../../../../interfaces/normalization/operation-get-model.interface";
import {OperationType} from "../../../../interfaces/normalization/operation-type-enumeration";
import {ConditionalTransformationComponent} from "../conditional-transformation/conditional-transformation.component";
import {CodeMapComponent} from "../code-map/code-map.component";
import {CopyLocationComponent} from "../copy-location/copy-location.component";

@Component({
  selector: 'app-normalization-dialog',
  standalone: true,
  imports: [MatDialogModule, MatButtonModule, MatIconModule, CopyPropertyComponent, ConditionalTransformationComponent, CodeMapComponent, CopyLocationComponent],
  templateUrl: './operation-dialog.component.html',
  styleUrl: './operation-dialog.component.scss'
})
export class OperationDialogComponent implements OnInit {

  @ViewChild(CopyPropertyComponent) copyPropertyForm!: CopyPropertyComponent;
  @ViewChild(ConditionalTransformationComponent) conditionalTransformForm!: ConditionalTransformationComponent;
  @ViewChild(CodeMapComponent) codeMapForm!: CodeMapComponent;
  @ViewChild(CopyLocationComponent) copyLocationForm!: CopyLocationComponent;

  dialogTitle: string = '';
  viewOnly: boolean = false;
  formMode!: FormMode;
  formIsInvalid: boolean = true;
  operation!: IOperationModel;
  OperationType = OperationType;
  operationType?: OperationType; // or dynamic value

  constructor(@Inject(MAT_DIALOG_DATA) public data: {
                dialogTitle: string,
                formMode: FormMode,
                operationType: OperationType,
                viewOnly: boolean,
                operation: any
              },
              private dialogRef: MatDialogRef<NormalizationFormComponent>,
              private snackBar: MatSnackBar) {
  }

  ngOnInit(): void {
    this.dialogTitle = this.data.dialogTitle;
    this.viewOnly = this.data.viewOnly;
    this.operation = this.data.operation;
    this.formMode = this.data.formMode;
    this.operationType = this.data.operationType;
  }

  //Form Mode enum getter
  get FormMode(): typeof FormMode {
    return FormMode;
  }

  onFormValueChanged(formValidity: boolean) {
    this.formIsInvalid = formValidity;
  }

  onSubmittedConfiguration(outcome: IEntityCreatedResponse) {
    if (outcome.id.length > 0 || outcome.message.length > 0) {
      this.dialogRef.close(outcome.message);
    } else {
      this.snackBar.open(`Failed to create operation configuration for the facility, see error for details.`, '', {
        duration: 3500,
        panelClass: 'error-snackbar',
        horizontalPosition: 'end',
        verticalPosition: 'top'
      });
    }
  }

  submitConfiguration() {
    switch (this.operationType) {
      case OperationType.CopyProperty:
        this.copyPropertyForm?.submitConfiguration();
        break;
      case OperationType.ConditionalTransform:
        this.conditionalTransformForm?.submitConfiguration();
        break;
      case OperationType.CodeMap:
        this.codeMapForm?.submitConfiguration();
        break;
      case OperationType.CopyLocation:
        this.copyLocationForm?.submitConfiguration();
        break;
      default:
        console.warn('Unknown operation type:', this.operationType);
    }
  }
}
