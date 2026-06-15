import {ComponentFixture, TestBed} from '@angular/core/testing';
import {RemoveExtensionsComponent} from './remove-extensions.component';
import {FormMode} from '../../../../models/FormMode.enum';
import {OperationService} from '../../../../services/gateway/normalization/operation.service';
import {MatSnackBar} from '@angular/material/snack-bar';
import {of} from 'rxjs';
import {NoopAnimationsModule} from '@angular/platform-browser/animations';
import {OperationType} from '../../../../interfaces/normalization/operation-type-enumeration';
import {AbstractControl} from '@angular/forms';

describe('RemoveExtensionsComponent', () => {
  let component: RemoveExtensionsComponent;
  let fixture: ComponentFixture<RemoveExtensionsComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [RemoveExtensionsComponent, NoopAnimationsModule],
      providers: [
        {
          provide: OperationService,
          useValue: {
            getResourceTypes: () => of([]),
            getVendors: () => of([])
          }
        },
        {provide: MatSnackBar, useValue: {open: () => {}}}
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(RemoveExtensionsComponent);
    component = fixture.componentInstance;
    component.operation = {
      id: '',
      facilityId: 'test-facility',
      operationJson: '{}',
      parsedOperationJson: {OperationType: OperationType.RemoveExtensions, Name: '', Description: '', ExtensionUrls: []} as any,
      operationType: OperationType.RemoveExtensions as string,
      description: '',
      isDisabled: false,
      createDate: '',
      operationResourceTypes: [],
      vendorPresets: []
    };
    component.formMode = FormMode.Create;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  describe('extension URL absolute-URL validation', () => {
    function addUrlControl(url: string): AbstractControl {
      component.addExtensionUrl();
      const ctrl = component.extensionUrlsArray.controls[component.extensionUrlsArray.length - 1];
      ctrl.setValue(url);
      ctrl.markAsTouched();
      return ctrl;
    }

    describe('valid absolute URLs (no absoluteUrl error)', () => {
      it('accepts an http URL', () => {
        expect(addUrlControl('http://example.com/fhir/extension/test').hasError('absoluteUrl')).toBeFalse();
      });

      it('accepts an https URL', () => {
        expect(addUrlControl('https://hl7.org/fhir/StructureDefinition/ext').hasError('absoluteUrl')).toBeFalse();
      });

      it('accepts a URN', () => {
        expect(addUrlControl('urn:oid:2.16.840.1.113883.4.3').hasError('absoluteUrl')).toBeFalse();
      });

      it('does not fire absoluteUrl error for an empty value (required handles empty separately)', () => {
        expect(addUrlControl('').hasError('absoluteUrl')).toBeFalse();
      });
    });

    describe('invalid URLs (absoluteUrl error set)', () => {
      it('rejects a relative path', () => {
        expect(addUrlControl('/relative/path').hasError('absoluteUrl')).toBeTrue();
      });

      it('rejects a plain word with no scheme', () => {
        expect(addUrlControl('not-a-url').hasError('absoluteUrl')).toBeTrue();
      });

      it('rejects a host-only string without a scheme', () => {
        expect(addUrlControl('example.com/fhir/extension').hasError('absoluteUrl')).toBeTrue();
      });
    });
  });
});
