import { Component, EventEmitter, Input, OnChanges, OnInit, Output, SimpleChanges } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatChipsModule } from '@angular/material/chips';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatToolbarModule } from '@angular/material/toolbar';
import { ICensusConfiguration } from 'src/app/interfaces/census/census-config-model.interface';
import { IEntityCreatedResponse } from 'src/app/interfaces/entity-created-response.model';
import { ENTER, COMMA } from '@angular/cdk/keycodes';
import { FormMode } from 'src/app/models/FormMode.enum';
import { CensusService } from 'src/app/services/gateway/census/census.service';

@Component({
  selector: 'app-census-config-form',
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
  templateUrl: './census-config-form.component.html',
  styleUrls: ['./census-config-form.component.scss']
})
export class CensusConfigFormComponent implements OnInit, OnChanges {
  @Input() item!: ICensusConfiguration;

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

  constructor(private snackBar: MatSnackBar, private censusService: CensusService) {

    //initialize form with fields based on ICensusConfiguration
    this.configForm = new FormGroup({
      facilityId: new FormControl('', Validators.required),
      scheduledTrigger: new FormControl('', Validators.required),
    });
  }

  ngOnInit(): void {
    this.configForm.reset();

    if(this.item) {
      //set form values
      this.facilityIdControl.setValue(this.item.facilityId);
      this.facilityIdControl.updateValueAndValidity();

      this.scheduledTriggerControl.setValue(this.item.scheduledTrigger);
      this.scheduledTriggerControl.updateValueAndValidity();
    }

    this.configForm.valueChanges.subscribe(() => {
      this.formValueChanged.emit(this.configForm.invalid);
    });
  }

  ngOnChanges(changes: SimpleChanges) {

    if (changes['item'] && changes['item'].currentValue) {
      this.facilityIdControl.setValue(this.item.facilityId);
      this.facilityIdControl.updateValueAndValidity();

      this.scheduledTriggerControl.setValue(this.item.scheduledTrigger);
      this.scheduledTriggerControl.updateValueAndValidity();

      // toggle view
      this.toggleViewOnly(this.viewOnly);
    }
  }

  //Form Mode enum getter
  get FormMode(): typeof FormMode {
    return FormMode;
  }

  get facilityIdControl(): FormControl {
    return this.configForm.get('facilityId') as FormControl;
  }

  get scheduledTriggerControl(): FormControl {
    return this.configForm.get('scheduledTrigger') as FormControl;
  }

  // Dynamically disable or enable the form control based on viewOnly
  toggleViewOnly(viewOnly: boolean) {
    this.facilityIdControl.disable();
    if (viewOnly) {
      this.scheduledTriggerControl.disable();
    } else {
      this.scheduledTriggerControl.enable();
    }
  }

  clearScheduledTrigger(): void {
    this.scheduledTriggerControl.setValue('');
    this.scheduledTriggerControl.updateValueAndValidity();
  }

  submitConfiguration(): void {
    if(this.configForm.valid) {
      if(this.formMode == FormMode.Create) {
        this.censusService.createConfiguration(this.facilityIdControl.value, this.scheduledTriggerControl.value).subscribe((response: IEntityCreatedResponse) => {
          this.submittedConfiguration.emit({id: '', message: "Census Created"});
        });
      }
      else if(this.formMode == FormMode.Edit) {
        this.censusService.updateConfiguration(this.facilityIdControl.value, this.scheduledTriggerControl.value).subscribe((response: IEntityCreatedResponse) => {
          this.submittedConfiguration.emit({id: '', message: "Census Updated"});
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
