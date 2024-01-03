import { Component, EventEmitter, Input, OnChanges, OnInit, Output, SimpleChanges } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormControl, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatChipsModule } from '@angular/material/chips';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatToolbarModule } from '@angular/material/toolbar';
import { IDataAcquisitionFacilityModel, ITenantDataAcquisitionConfigModel } from 'src/app/interfaces/data-acquisition/data-acquisition-config-model.interface';
import { IEntityCreatedResponse } from 'src/app/interfaces/entity-created-response.model';
import { FormMode } from 'src/app/models/FormMode.enum';
import { ENTER, COMMA } from '@angular/cdk/keycodes';
import { DataAcquisitionService } from 'src/app/services/gateway/data-acquisition/data-acquisition.service';
import { FhirVersion } from 'src/app/models/FhirVersion.enum';

@Component({
  selector: 'app-data-acquisition-config-form',
  standalone: true,
  imports: [
    CommonModule,
    MatButtonModule,
    MatFormFieldModule,    
    MatInputModule,
    MatIconModule,
    MatChipsModule,
    MatSlideToggleModule,
    ReactiveFormsModule,
    MatSnackBarModule,
    MatToolbarModule
  ],
  templateUrl: './data-acquisition-config-form.component.html',
  styleUrls: ['./data-acquisition-config-form.component.scss']
})
export class DataAcquisitionConfigFormComponent implements OnInit, OnChanges {
  @Input() item!: ITenantDataAcquisitionConfigModel;  

  @Input() formMode!: FormMode;

  private _viewOnly: boolean = false;
  @Input()
  set viewOnly(v: boolean) { if (v !== null) this._viewOnly = v; }
  get viewOnly() { return this._viewOnly; }

  @Output() formValueChanged = new EventEmitter<boolean>();

  @Output() submittedConfiguration = new EventEmitter<IEntityCreatedResponse>();

  configForm!: FormGroup;
  addOnBlur = true;
  readonly separatorKeysCodes = [ENTER, COMMA] as const;

  constructor(private snackBar: MatSnackBar, private dataAcquisitionService: DataAcquisitionService) { }

  ngOnInit(): void {
    this.configForm.reset(); 

    if(this.item) {
      //set form values
      this.tenantIdControl.setValue(this.item.tenantId);
      this.tenantIdControl.updateValueAndValidity();
    
    }    
   
    this.configForm.valueChanges.subscribe(() => {
      this.formValueChanged.emit(this.configForm.invalid);
    });
  }

  ngOnChanges(changes: SimpleChanges) {     
    
    if (changes['item'] && changes['item'].currentValue) {
      this.tenantIdControl.setValue(this.item.tenantId);
      this.tenantIdControl.updateValueAndValidity();

    
    }
  }

  //Form Mode enum getter
  get FormMode(): typeof FormMode {
    return FormMode;
  }

  get tenantIdControl(): FormControl {
    return this.configForm.get('facilityId') as FormControl;
  }

  clearFacilityId(): void {
    this.tenantIdControl.setValue('');
    this.tenantIdControl.updateValueAndValidity();
  }

  submitConfiguration(): void {
    if(this.configForm.valid) {
      let facility: IDataAcquisitionFacilityModel = {
        facilityId: this.tenantIdControl.value,
        fhirVersion: FhirVersion.R4,
        baseFhirUrl: '',
        resourceSettings: []
      };
      
      if(this.formMode == FormMode.Create) {      
        this.dataAcquisitionService.createConfiguration(this.tenantIdControl.value, []).subscribe((response: IEntityCreatedResponse) => {
          this.submittedConfiguration.emit(response);
        });
      }
      else if(this.formMode == FormMode.Edit) {
        this.dataAcquisitionService.updateConfiguration(this.item.id, this.item.tenantId, '', facility).subscribe((response: IEntityCreatedResponse) => {
          this.submittedConfiguration.emit(response);
        });
      }
    }
    else {
      this.snackBar.open(`Invalid form, please check for errors.`, '', {
        duration: 3500,
        panelClass: 'error-snackbar',
        horizontalPosition: 'end',
        verticalPosition: 'top'
      });
    }
  }

}
