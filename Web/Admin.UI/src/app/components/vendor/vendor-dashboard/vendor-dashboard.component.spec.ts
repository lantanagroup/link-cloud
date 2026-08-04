import { ComponentFixture, TestBed } from '@angular/core/testing';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatSnackBarModule } from '@angular/material/snack-bar';
import { of } from 'rxjs';

import { VendorDashboardComponent } from './vendor-dashboard.component';
import { VendorConfigDialogComponent } from '../vendor-config-dialog/vendor-config-dialog.component';
import { VendorService } from '../../../services/gateway/vendor/vendor.service';
import { FormMode } from '../../../models/FormMode.enum';
import { IVendorConfigModel } from '../../../interfaces/vendor/vendor-config-model.interface';
import { AppConfigService } from '../../../services/app-config.service';

describe('VendorDashboardComponent', () => {
  let component: VendorDashboardComponent;
  let fixture: ComponentFixture<VendorDashboardComponent>;
  let vendorService: jasmine.SpyObj<VendorService>;
  let dialog: jasmine.SpyObj<MatDialog>;
  /** Mutable so a test can set the flag before the first change detection. */
  let appConfigService: { config: { vendorEditEnabled?: boolean } };

  const vendors: IVendorConfigModel[] = [
    { id: 'vendor-1', name: 'Epic', secretId: 'epic-signing-pem' },
    { id: 'vendor-2', name: 'Cerner' }
  ];

  beforeEach(async () => {
    vendorService = jasmine.createSpyObj<VendorService>('VendorService', ['getVendors', 'deleteVendor']);
    vendorService.getVendors.and.returnValue(of(vendors));

    dialog = jasmine.createSpyObj<MatDialog>('MatDialog', ['open']);

    // Editing is off by default in shipped config; most tests here cover the enabled behaviour.
    appConfigService = { config: { vendorEditEnabled: true } };

    await TestBed.configureTestingModule({
      imports: [VendorDashboardComponent, NoopAnimationsModule, MatDialogModule, MatSnackBarModule],
      providers: [
        { provide: VendorService, useValue: vendorService },
        { provide: MatDialog, useValue: dialog },
        { provide: AppConfigService, useValue: appConfigService }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(VendorDashboardComponent);
    component = fixture.componentInstance;
  });

  /** Stubs the dialog so afterClosed() resolves with `result`. */
  function dialogClosingWith(result: unknown): void {
    dialog.open.and.returnValue({ afterClosed: () => of(result) } as any);
  }

  it('should create', () => {
    fixture.detectChanges();
    expect(component).toBeTruthy();
  });

  it('clears loading once the vendors arrive', () => {
    fixture.detectChanges();

    expect(component.vendors.length).toBe(2);
    expect(component.loading).toBeFalse();
  });

  it('shows the secret id column', () => {
    fixture.detectChanges();

    expect(component.displayedColumns).toContain('secretId');
  });

  it('renders the secret id, and "Not set" for a vendor without one', () => {
    fixture.detectChanges();

    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('epic-signing-pem');
    expect(text).toContain('Not set');
  });

  it('opens the dialog in Edit mode with the row', () => {
    dialogClosingWith(null);
    fixture.detectChanges();

    component.onEdit(vendors[0]);

    const config = dialog.open.calls.mostRecent().args[1] as any;
    expect(dialog.open.calls.mostRecent().args[0]).toBe(VendorConfigDialogComponent);
    expect(config.data.formMode).toBe(FormMode.Edit);
    expect(config.data.vendorConfig).toBe(vendors[0]);
    expect(config.data.viewOnly).toBeFalse();
  });

  it('refreshes the list after a saved edit', () => {
    dialogClosingWith({ success: true, message: '' });
    fixture.detectChanges();
    const before = vendorService.getVendors.calls.count();

    component.onEdit(vendors[1]);

    expect(vendorService.getVendors.calls.count()).toBe(before + 1);
  });

  it('does not refresh when the edit dialog is dismissed', () => {
    dialogClosingWith(null);
    fixture.detectChanges();
    const before = vendorService.getVendors.calls.count();

    component.onEdit(vendors[1]);

    expect(vendorService.getVendors.calls.count()).toBe(before);
  });

  // Editing is gated because no vendor update endpoint exists yet -- VendorController offers
  // list, add and delete only, so an enabled edit button would save into a 404.
  describe('when vendorEditEnabled is off', () => {
    beforeEach(() => {
      appConfigService.config.vendorEditEnabled = false;
      fixture.detectChanges();
    });

    it('offers no edit button', () => {
      const edit = (fixture.nativeElement as HTMLElement).querySelector('[aria-label="Edit Vendor"]');
      expect(edit).toBeNull();
    });

    it('keeps delete available', () => {
      const remove = (fixture.nativeElement as HTMLElement).querySelector('[aria-label="Delete Vendor"]');
      expect(remove).not.toBeNull();
    });

    it('opens no dialog even if onEdit is called directly', () => {
      dialogClosingWith(null);

      component.onEdit(vendors[0]);

      expect(dialog.open).not.toHaveBeenCalled();
    });
  });

  it('offers an edit button when the flag is on', () => {
    fixture.detectChanges();

    const edit = (fixture.nativeElement as HTMLElement).querySelector('[aria-label="Edit Vendor"]');
    expect(edit).not.toBeNull();
  });
});
