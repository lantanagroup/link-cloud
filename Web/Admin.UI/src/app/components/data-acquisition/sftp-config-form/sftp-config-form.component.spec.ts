import { ChangeDetectorRef } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { FormGroup } from '@angular/forms';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { of } from 'rxjs';
import { FormMode } from 'src/app/models/FormMode.enum';
import { DataAcquisitionService } from 'src/app/services/gateway/data-acquisition/data-acquisition.service';
import {
  ICreateSftpConfigurationModel,
  SftpAcquisitionSubType
} from '../../../interfaces/data-acquisition/sftp-config-model.interface';
import { SftpConfigFormComponent } from './sftp-config-form.component';

describe('SftpConfigFormComponent ADT configuration', () => {
  let fixture: ComponentFixture<SftpConfigFormComponent>;
  let component: SftpConfigFormComponent;
  let service: jasmine.SpyObj<DataAcquisitionService>;

  beforeEach(async () => {
    service = jasmine.createSpyObj('DataAcquisitionService', ['createSftpConfiguration', 'getSftpCredentialStatus']);
    service.createSftpConfiguration.and.returnValue(of({ id: '1', message: 'created' } as any));

    await TestBed.configureTestingModule({
      imports: [SftpConfigFormComponent],
      providers: [provideNoopAnimations(), { provide: DataAcquisitionService, useValue: service }]
    }).compileComponents();

    fixture = TestBed.createComponent(SftpConfigFormComponent);
    component = fixture.componentInstance;
    component.item = {
      organizationId: 'facility-1',
      host: 'sftp.example.com',
      port: 22,
      remoteDirectory: '/',
      timeout: '00:01:00',
      removeAfterProcessing: false,
      authenticationProtocol: 'Basic',
      enableBenchmarking: false,
      acquisitionConfigurations: []
    };
    component.formMode = FormMode.Create;
    fixture.detectChanges();
  });

  function addAdtConfiguration(): FormGroup {
    component.addAcquisitionConfiguration();
    const group = component.acquisitionConfigurationsArray.at(0) as FormGroup;
    group.get('subType')?.setValue(SftpAcquisitionSubType.HL7ADT);
    return group;
  }

  function submitted(): ICreateSftpConfigurationModel {
    component.submitConfiguration();
    return service.createSftpConfiguration.calls.mostRecent().args[1];
  }

  it('shows the ADT fields only for the HL7ADT sub type, and the field for the chosen option', async () => {
    component.addAcquisitionConfiguration();
    const group = component.acquisitionConfigurationsArray.at(0) as FormGroup;
    const element: HTMLElement = fixture.nativeElement;
    const labels = async () => {
      // A click marks the view dirty in the app; these changes are made directly on the form
      fixture.componentRef.injector.get(ChangeDetectorRef).markForCheck();
      fixture.detectChanges();
      await fixture.whenStable();
      return Array.from(element.querySelectorAll('mat-label')).map(label => label.textContent?.trim());
    };

    expect(await labels()).not.toContain('Patient.Id Location');
    expect(component.acquisitionSubTypes).toContain(SftpAcquisitionSubType.HL7ADT);

    group.get('subType')?.setValue(SftpAcquisitionSubType.HL7ADT);
    expect(await labels()).toContain('Patient.Id Location');
    expect(await labels()).toContain('MRN Location');
    expect(await labels()).toContain('Encounter Id Location');
    expect(await labels()).not.toContain('MRN Identifier System');
    expect(element.querySelectorAll('mat-radio-button').length).toBe(2);

    group.get('adtConfiguration.patientIdSource')?.setValue('mrnSearch');
    expect(await labels()).toContain('MRN Identifier System');
    expect(await labels()).not.toContain('Patient.Id Location');
  });

  it('requires the Patient.Id location by default for an ADT feed', () => {
    const group = addAdtConfiguration();

    expect(group.get('adtConfiguration.patientIdLocation')?.hasError('required')).toBeTrue();
    expect(group.get('adtConfiguration.mrnIdentifierSystem')?.valid).toBeTrue();
  });

  it('requires the MRN system instead when using MRN search', () => {
    const group = addAdtConfiguration();
    group.get('adtConfiguration.patientIdSource')?.setValue('mrnSearch');

    expect(group.get('adtConfiguration.mrnIdentifierSystem')?.hasError('required')).toBeTrue();
    expect(group.get('adtConfiguration.patientIdLocation')?.valid).toBeTrue();
  });

  it('requires nothing ADT-specific for another sub type', () => {
    component.addAcquisitionConfiguration();
    const group = component.acquisitionConfigurationsArray.at(0) as FormGroup;

    expect(group.get('adtConfiguration')?.valid).toBeTrue();
    expect(component.configForm.hasError('adtFilesNotHandled')).toBeFalse();
  });

  ['PID-2', 'PID.18', 'PID-3[5=MR].1', 'PID-3[2].1', 'OBX[2]-5', 'OBX[3.1=FHIRID]-5.1', 'ZPI-1.2.3'].forEach(location => {
    it(`accepts the location ${location}`, () => {
      const group = addAdtConfiguration();
      group.get('adtConfiguration.patientIdLocation')?.setValue(location);

      expect(group.get('adtConfiguration.patientIdLocation')?.valid).toBeTrue();
    });
  });

  ['PID', 'PID-0', 'PID-3[]', 'PID-3[a=b]', 'PATIENT-1', 'PID-3.1.2.3'].forEach(location => {
    it(`rejects the location ${location}`, () => {
      const group = addAdtConfiguration();
      group.get('adtConfiguration.patientIdLocation')?.setValue(location);

      expect(group.get('adtConfiguration.patientIdLocation')?.hasError('hl7Location')).toBeTrue();
    });
  });

  it('requires ADT files to leave the directory once read', () => {
    const group = addAdtConfiguration();

    expect(component.configForm.hasError('adtFilesNotHandled')).toBeTrue();

    group.get('processedDirectory')?.setValue('/processed');
    expect(component.configForm.hasError('adtFilesNotHandled')).toBeFalse();

    group.get('processedDirectory')?.setValue('');
    component.removeAfterProcessingControl.setValue(true);
    expect(component.configForm.hasError('adtFilesNotHandled')).toBeFalse();
  });

  it('submits only the chosen way of identifying patients', () => {
    const group = addAdtConfiguration();
    group.get('processedDirectory')?.setValue('/processed');
    group.get('adtConfiguration.patientIdLocation')?.setValue('left over from the other option');
    group.get('adtConfiguration.patientIdSource')?.setValue('mrnSearch');
    group.get('adtConfiguration.mrnIdentifierSystem')?.setValue(' urn:oid:1.2.3 ');
    group.get('adtConfiguration.mrnLocation')?.setValue('PID-18');

    const adt = submitted().acquisitionConfigurations[0].adtConfiguration;

    expect(adt).toEqual({
      patientIdLocation: undefined,
      mrnIdentifierSystem: 'urn:oid:1.2.3',
      mrnLocation: 'PID-18',
      encounterIdLocation: undefined
    });
  });

  it('submits no ADT configuration for another sub type', () => {
    component.addAcquisitionConfiguration();

    expect(submitted().acquisitionConfigurations[0].adtConfiguration).toBeUndefined();
  });

  it('loads an existing MRN search configuration', () => {
    const group = component.createAcquisitionConfigGroup({
      acquisitionType: 'Census' as any,
      subType: SftpAcquisitionSubType.HL7ADT,
      adtConfiguration: { mrnIdentifierSystem: 'urn:oid:1.2.3' }
    });

    expect(group.get('adtConfiguration.patientIdSource')?.value).toBe('mrnSearch');
    expect(group.get('adtConfiguration.mrnIdentifierSystem')?.value).toBe('urn:oid:1.2.3');
  });
});
