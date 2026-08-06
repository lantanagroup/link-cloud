import { ComponentFixture, TestBed } from '@angular/core/testing';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';
import { MatDialogModule } from '@angular/material/dialog';
import { MatSnackBarModule } from '@angular/material/snack-bar';
import { of, throwError } from 'rxjs';

import { VendorConfigFormComponent } from './vendor-config-form.component';
import { VendorService } from '../../../services/gateway/vendor/vendor.service';
import { FormMode } from '../../../models/FormMode.enum';
import { IVendorConfigModel } from '../../../interfaces/vendor/vendor-config-model.interface';

describe('VendorConfigFormComponent', () => {
  let component: VendorConfigFormComponent;
  let fixture: ComponentFixture<VendorConfigFormComponent>;
  let vendorService: jasmine.SpyObj<VendorService>;

  const epic: IVendorConfigModel = { id: 'vendor-1', name: 'Epic', secretId: 'epic-signing-pem' };
  const cerner: IVendorConfigModel = { id: 'vendor-2', name: 'Cerner' };

  beforeEach(async () => {
    vendorService = jasmine.createSpyObj<VendorService>('VendorService', ['createVendor', 'updateVendor']);

    await TestBed.configureTestingModule({
      imports: [VendorConfigFormComponent, NoopAnimationsModule, MatDialogModule, MatSnackBarModule],
      providers: [{ provide: VendorService, useValue: vendorService }]
    }).compileComponents();

    fixture = TestBed.createComponent(VendorConfigFormComponent);
    component = fixture.componentInstance;
  });

  /** ngOnInit reads `item`, so the inputs have to be set before the first change detection. */
  function initWith(item: IVendorConfigModel | undefined, formMode: FormMode): void {
    component.item = item as IVendorConfigModel;
    component.formMode = formMode;
    fixture.detectChanges();
  }

  it('populates the secret id from the item in Edit mode', () => {
    initWith(epic, FormMode.Edit);

    expect(component.name.value).toBe('Epic');
    expect(component.secretId.value).toBe('epic-signing-pem');
  });

  it('expands the key settings panel only when a secret id is already set', () => {
    initWith(epic, FormMode.Edit);
    expect(component.keySettingsExpanded).toBeTrue();

    // A second component, because keySettingsExpanded is decided once in ngOnInit.
    const bare = TestBed.createComponent(VendorConfigFormComponent);
    bare.componentInstance.item = cerner;
    bare.componentInstance.formMode = FormMode.Edit;
    bare.detectChanges();

    expect(bare.componentInstance.keySettingsExpanded).toBeFalse();
  });

  ['has space', 'has/slash', 'has_underscore', 'https://v.vault.azure.net/secrets/k', 'a'.repeat(128)]
    .forEach(invalid => {
      it(`rejects a secret id Key Vault cannot accept: "${invalid.slice(0, 24)}"`, () => {
        initWith(cerner, FormMode.Edit);

        component.secretId.setValue(invalid);

        expect(component.secretId.valid).toBeFalse();
        expect(component.vendorForm.valid).toBeFalse();
      });
    });

  it('marks the form invalid on the keystroke, before the field is blurred', () => {
    initWith(cerner, FormMode.Edit);

    component.secretId.setValue('has space');

    expect(component.secretId.touched).toBeFalse();
    expect(component.vendorForm.invalid).toBeTrue();
  });

  it('shows the inline message once the field is blurred', () => {
    initWith(cerner, FormMode.Edit);

    component.secretId.setValue('has space');
    fixture.detectChanges();
    const whileTyping = (fixture.nativeElement as HTMLElement)
      .querySelector('[data-testid="vendor-secret-id-error"]');

    component.secretId.markAsTouched();
    fixture.detectChanges();
    const afterBlur = (fixture.nativeElement as HTMLElement)
      .querySelector('[data-testid="vendor-secret-id-error"]');

    expect(whileTyping).toBeNull();
    expect(afterBlur).not.toBeNull();
  });

  it('accepts a secret id Key Vault can resolve', () => {
    initWith(cerner, FormMode.Edit);

    component.secretId.setValue('epic-signing-key');

    expect(component.secretId.valid).toBeTrue();
  });

  it('accepts an empty secret id, which clears the association', () => {
    initWith(epic, FormMode.Edit);

    component.secretId.setValue('');

    expect(component.secretId.valid).toBeTrue();
  });

  it('does not call the service when the secret id is invalid', () => {
    initWith(epic, FormMode.Edit);

    component.secretId.setValue('has space');
    component.submitConfiguration();

    expect(vendorService.updateVendor).not.toHaveBeenCalled();
  });

  it('surfaces the field message when the API rejects the value', () => {
    vendorService.updateVendor.and.returnValue(throwError(() => ({
      message: 'An error occured in our API. Please use the trace id when requesting assistence. - 00-abc',
      error: {
        errors: {
          'Authentication.SigningKeySecretId': ['SigningKeySecretId must be a Key Vault secret name.']
        }
      }
    })));
    initWith(epic, FormMode.Edit);

    const outcomes: { success: boolean; message: string }[] = [];
    component.submittedConfiguration.subscribe(o => outcomes.push(o));

    component.submitConfiguration();

    expect(outcomes[0].success).toBeFalse();
    expect(outcomes[0].message).toContain('SigningKeySecretId must be a Key Vault secret name.');
  });

  it('requires a name but not a secret id', () => {
    initWith(cerner, FormMode.Edit);

    expect(component.vendorForm.valid).toBeTrue();

    component.name.setValue('');
    expect(component.vendorForm.valid).toBeFalse();
  });

  it('sends the secret id when updating', () => {
    vendorService.updateVendor.and.returnValue(of({ success: true, message: '' }));
    initWith(cerner, FormMode.Edit);

    component.secretId.setValue('cerner-signing-pem');
    component.submitConfiguration();

    expect(vendorService.updateVendor).toHaveBeenCalledWith(
      jasmine.objectContaining({ id: 'vendor-2', name: 'Cerner', secretId: 'cerner-signing-pem' })
    );
  });

  it('clears the association by sending an explicit null, not undefined or ""', () => {
    vendorService.updateVendor.and.returnValue(of({ success: true, message: '' }));
    initWith(epic, FormMode.Edit);

    component.secretId.setValue('   ');
    component.submitConfiguration();

    const sent = vendorService.updateVendor.calls.mostRecent().args[0];
    expect(sent.secretId).toBeNull();
    // Undefined would be dropped by JSON.stringify, leaving the field absent from the request
    // body -- indistinguishable from "leave it alone" for a partial-update endpoint.
    expect('secretId' in sent).toBeTrue();
    expect(JSON.parse(JSON.stringify(sent)).secretId).toBeNull();
  });

  it('emits success after a saved update', () => {
    vendorService.updateVendor.and.returnValue(of({ success: true, message: '' }));
    initWith(epic, FormMode.Edit);

    const outcomes: boolean[] = [];
    component.submittedConfiguration.subscribe(o => outcomes.push(o.success));

    component.submitConfiguration();

    expect(outcomes).toEqual([true]);
  });

  it('emits failure without closing when the update fails', () => {
    vendorService.updateVendor.and.returnValue(throwError(() => new Error('boom')));
    initWith(epic, FormMode.Edit);

    const outcomes: { success: boolean; message: string }[] = [];
    component.submittedConfiguration.subscribe(o => outcomes.push(o));

    component.submitConfiguration();

    expect(outcomes.length).toBe(1);
    expect(outcomes[0].success).toBeFalse();
    expect(outcomes[0].message).toBe('boom');
  });

  it('does not call the service while the form is invalid', () => {
    initWith(cerner, FormMode.Edit);

    component.name.setValue('');
    component.submitConfiguration();

    expect(vendorService.updateVendor).not.toHaveBeenCalled();
  });

  it('creates with the secret id entered on the add form', () => {
    vendorService.createVendor.and.returnValue(of({ success: true }));
    initWith(undefined, FormMode.Create);

    component.name.setValue('Veradigm');
    component.secretId.setValue('veradigm-signing-pem');
    component.submitConfiguration();

    expect(vendorService.createVendor).toHaveBeenCalledWith(
      jasmine.objectContaining({ name: 'Veradigm', secretId: 'veradigm-signing-pem' }));
    expect(vendorService.updateVendor).not.toHaveBeenCalled();
  });

  it('creates with a null secret id when the field is left blank', () => {
    vendorService.createVendor.and.returnValue(of({ success: true }));
    initWith(undefined, FormMode.Create);

    component.name.setValue('Veradigm');
    component.submitConfiguration();

    expect(vendorService.createVendor).toHaveBeenCalledWith(
      jasmine.objectContaining({ name: 'Veradigm', secretId: null }));
  });

  it('emits failure without falling through to an update when the create fails', () => {
    vendorService.createVendor.and.returnValue(throwError(() => new Error('boom')));
    initWith(undefined, FormMode.Create);

    const outcomes: { success: boolean; message: string }[] = [];
    component.submittedConfiguration.subscribe(o => outcomes.push(o));

    component.name.setValue('Veradigm');
    component.submitConfiguration();

    expect(outcomes.length).toBe(1);
    expect(outcomes[0].success).toBeFalse();
    expect(outcomes[0].message).toBe('boom');
    expect(vendorService.updateVendor).not.toHaveBeenCalled();
  });
});
