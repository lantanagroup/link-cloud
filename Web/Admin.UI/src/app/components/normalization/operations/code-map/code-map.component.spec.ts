import { ComponentFixture, TestBed } from '@angular/core/testing';
import {MatSnackBar} from '@angular/material/snack-bar';
import {MatDialog} from '@angular/material/dialog';
import {of} from 'rxjs';

import { CodeMapComponent } from './code-map.component';
import {OperationService} from '../../../../services/gateway/normalization/operation.service';
import {OperationType} from '../../../../interfaces/normalization/operation-type-enumeration';
import {FormMode} from '../../../../models/FormMode.enum';

describe('CodeMapComponent', () => {
  let component: CodeMapComponent;
  let fixture: ComponentFixture<CodeMapComponent>;
  let operationService: jasmine.SpyObj<OperationService>;

  beforeEach(async () => {
    operationService = jasmine.createSpyObj('OperationService', [
      'getResourceTypes', 'getVendorVersions', 'createOperationConfiguration', 'updateOperationConfiguration'
    ]);
    operationService.getResourceTypes.and.returnValue(of(['Location']));
    operationService.getVendorVersions.and.returnValue(of([]));
    operationService.createOperationConfiguration.and.returnValue(of({id: 'operation-id', message: ''}));
    operationService.updateOperationConfiguration.and.returnValue(of({id: 'operation-id', message: ''}));
    await TestBed.configureTestingModule({
      imports: [CodeMapComponent],
      providers: [
        {provide: OperationService, useValue: operationService},
        {provide: MatSnackBar, useValue: jasmine.createSpyObj('MatSnackBar', ['open'])},
        {provide: MatDialog, useValue: jasmine.createSpyObj('MatDialog', ['open'])}
      ]
    })
    .compileComponents();

    fixture = TestBed.createComponent(CodeMapComponent);
    component = fixture.componentInstance;
  });

  it('should create', () => {
    expect(component).toBeTruthy();
    expect(component.operationType).toBe(OperationType.CodeMap);
  });

  for (const operationType of [OperationType.CodeMap, OperationType.HSLOCMap] as const) {
    for (const formMode of [FormMode.Create, FormMode.Edit]) {
      it(`preserves ${operationType} in ${formMode} submissions`, () => {
        const codeSystemMaps = [{
          SourceSystem: 'urn:local',
          TargetSystem: 'urn:hsloc',
          CodeMaps: {ICU: {Code: '1027-4', Display: 'Medical critical care'}}
        }];
        fixture.componentRef.setInput('operationType', operationType);
        component.formMode = formMode;
        component.operation = {
          id: 'operation-id', facilityId: 'test-facility', operationType,
          operationJson: '', description: 'Map locations', isDisabled: false, createDate: '',
          operationResourceTypes: [], vendorPresets: [],
          parsedOperationJson: {
            OperationType: operationType, Name: 'Map locations', Description: 'Map locations',
            FhirPath: 'type.coding', CodeSystemMaps: codeSystemMaps
          }
        };
        component.ngOnInit();
        component.form.patchValue({
          name: 'Map locations', selectedResourceTypes: ['Location'], fhirPath: 'type.coding'
        });
        if (formMode === FormMode.Create) {
          component.codeSystemMaps.at(0).patchValue({
            sourceSystem: 'urn:local', targetSystem: 'urn:hsloc',
            codeMaps: [{key: 'ICU', value: {code: '1027-4', display: 'Medical critical care'}}]
          });
        }

        expect(component.form.valid).toBeTrue();
        component.submitConfiguration();

        const request = formMode === FormMode.Create
          ? operationService.createOperationConfiguration
          : operationService.updateOperationConfiguration;
        expect(request).toHaveBeenCalledTimes(1);
        expect(request.calls.mostRecent().args[0].operation).toEqual(jasmine.objectContaining({
          OperationType: operationType, FhirPath: 'type.coding', CodeSystemMaps: codeSystemMaps
        }));
      });
    }
  }
});
