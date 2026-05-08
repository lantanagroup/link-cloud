import {Component, EventEmitter, Input, OnChanges, OnInit, Output, SimpleChanges} from '@angular/core';

import {MatSnackBar, MatSnackBarModule} from '@angular/material/snack-bar';
import {FormBuilder, FormGroup, ReactiveFormsModule, Validators} from '@angular/forms';
import {MatButtonModule} from '@angular/material/button';
import {MatSelectModule} from '@angular/material/select';
import {MatChipsModule} from '@angular/material/chips';
import {MatFormFieldModule} from '@angular/material/form-field';
import {MatIconModule} from '@angular/material/icon';
import {MatInputModule} from '@angular/material/input';
import {MatSlideToggleModule} from '@angular/material/slide-toggle';
import {MatToolbarModule} from '@angular/material/toolbar';
import {MatExpansionModule} from '@angular/material/expansion';
import {MatTooltipModule} from "@angular/material/tooltip";
import {FormMode} from "../../../models/FormMode.enum";
import {MatDialog} from "@angular/material/dialog";
import {IApiResponse} from "../../../interfaces/api-response.interface";
import {VendorService} from "../../../services/gateway/vendor/vendor.service";
import {IVendorConfigModel} from "../../../interfaces/vendor/vendor-config-model.interface";


@Component({
  selector: 'app-vendor-config-form',
  standalone: true,
  imports: [
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    MatIconModule,
    MatChipsModule,
    MatSlideToggleModule,
    ReactiveFormsModule,
    MatSnackBarModule,
    MatToolbarModule,
    MatExpansionModule,
    MatTooltipModule,
    MatSelectModule
],
  templateUrl: './vendor-config-form.component.html',
  styleUrls: ['./vendor-config-form.component.scss']
})
export class VendorConfigFormComponent {

  @Input() item!: IVendorConfigModel;
  @Input() formMode!: FormMode;

  private _viewOnly: boolean = false;

  @Input()
  set viewOnly(v: boolean) {
    if (v) this._viewOnly = v;
  }

  get viewOnly() {
    return this._viewOnly;
  }

  @Output() formValueChanged = new EventEmitter<boolean>();

  @Output() submittedConfiguration = new EventEmitter<IApiResponse>();

  vendorForm!: FormGroup;

  constructor(private snackBar: MatSnackBar, private vendorService: VendorService, private dialog: MatDialog, private fb: FormBuilder) {
    this.vendorForm = this.fb.group({
      name: ["", Validators.required]
    });
  }

  get name() {
    return this.vendorForm.controls['name'];
  }

  ngOnInit(): void {
    this.vendorForm.reset();

    if (this.item) {
      //set form values
      this.name.setValue(this.item.name);
    }

    this.vendorForm.valueChanges.subscribe(() => {
      this.formValueChanged.emit(this.vendorForm.invalid);
    });
  }

  submitConfiguration(): void {
    if (this.vendorForm.status == 'VALID') {
      if (this.formMode == FormMode.Create) {
        this.vendorService.createVendor(this.name.value).subscribe({
          next: (response) => {
            if (response) {
              this.submittedConfiguration.emit({success: true, message: ""});
            }
          },
          error: (err) => {
          }
        });
      }
    }
  }
}
