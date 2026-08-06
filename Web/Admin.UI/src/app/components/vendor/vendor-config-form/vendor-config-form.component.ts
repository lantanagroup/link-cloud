import {Component, EventEmitter, Input, OnChanges, OnInit, Output, SimpleChanges} from '@angular/core';

import {MatSnackBar, MatSnackBarModule} from '@angular/material/snack-bar';
import {AbstractControl, FormBuilder, FormGroup, ReactiveFormsModule, ValidationErrors, Validators} from '@angular/forms';
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

  private static readonly KeyVaultSecretName = /^[0-9a-zA-Z-]{1,127}$/;

  static keyVaultSecretNameValidator(control: AbstractControl): ValidationErrors | null {
    const value = (control.value ?? '').trim();
    return value === '' || VendorConfigFormComponent.KeyVaultSecretName.test(value)
      ? null
      : {pattern: true};
  }

  /**
   * Whether the JWT / Authentication panel starts open. Expanded when the vendor already has a
   * secret id so existing configuration is visible without hunting for it, collapsed otherwise
   * so the common case stays uncluttered.
   */
  keySettingsExpanded = false;

  constructor(private snackBar: MatSnackBar, private vendorService: VendorService, private dialog: MatDialog, private fb: FormBuilder) {
    this.vendorForm = this.fb.group({
      name: ["", Validators.required],
      secretId: ["", VendorConfigFormComponent.keyVaultSecretNameValidator]
    });
  }

  get name() {
    return this.vendorForm.controls['name'];
  }

  get secretId() {
    return this.vendorForm.controls['secretId'];
  }

  ngOnInit(): void {
    this.vendorForm.reset();

    if (this.item) {
      //set form values
      this.name.setValue(this.item.name);
      this.secretId.setValue(this.item.secretId ?? "");
      this.keySettingsExpanded = !!this.item.secretId;
    }

    this.vendorForm.valueChanges.subscribe(() => {
      this.formValueChanged.emit(this.vendorForm.invalid);
    });
  }

  submitConfiguration(): void {
    if (this.vendorForm.status != 'VALID') {
      return;
    }

    const submitted: IVendorConfigModel = {
      ...this.item,
      name: this.name.value,
      secretId: this.secretId.value?.trim() || null
    };

    if (this.formMode == FormMode.Create) {
      this.vendorService.createVendor(submitted).subscribe({
        next: (response) => {
          if (response) {
            this.submittedConfiguration.emit({success: true, message: ""});
          }
        },
        error: (err) => {
          this.submittedConfiguration.emit({success: false, message: this.failureMessage(err)});
        }
      });
      return;
    }

    this.vendorService.updateVendor(submitted).subscribe({
      next: () => {
        this.submittedConfiguration.emit({success: true, message: ""});
      },
      error: (err) => {
        this.submittedConfiguration.emit({success: false, message: this.failureMessage(err)});
      }
    });
  }

  /**
   * ErrorHandlingService already surfaces the detail; this is the text the dialog shows in its
   * snackbar while staying open so the admin's input is not thrown away.
   */
  private failureMessage(err: any): string {
    const fieldMessages = Object.values(err?.error?.errors ?? {}).flat() as string[];
    if (fieldMessages.length) {
      return fieldMessages.join(' ');
    }

    return err?.message ?? 'Failed to save the vendor configuration. Please try again.';
  }
}
