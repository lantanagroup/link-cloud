import { ComponentFixture, TestBed } from '@angular/core/testing';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatSnackBarModule } from '@angular/material/snack-bar';
import { of, throwError } from 'rxjs';

import { VendorDashboardComponent } from './vendor-dashboard.component';
import { VendorConfigDialogComponent } from '../vendor-config-dialog/vendor-config-dialog.component';
import { AlertDialogComponent } from '../../core/alert-dialog/alert-dialog.component';
import { VendorService } from '../../../services/gateway/vendor/vendor.service';
import { FormMode } from '../../../models/FormMode.enum';
import { IVendorConfigModel } from '../../../interfaces/vendor/vendor-config-model.interface';

describe('VendorDashboardComponent', () => {
  let component: VendorDashboardComponent;
  let fixture: ComponentFixture<VendorDashboardComponent>;
  let vendorService: jasmine.SpyObj<VendorService>;
  let dialog: jasmine.SpyObj<MatDialog>;
  /** Mutable so a test can set the flag before the first change detection. */

  const vendors: IVendorConfigModel[] = [
    { id: 'vendor-1', name: 'Epic', secretId: 'epic-signing-pem' },
    { id: 'vendor-2', name: 'Cerner' }
  ];

  beforeEach(async () => {
    vendorService = jasmine.createSpyObj<VendorService>('VendorService', ['getVendors', 'deleteVendor']);
    vendorService.getVendors.and.returnValue(of(vendors));

    dialog = jasmine.createSpyObj<MatDialog>('MatDialog', ['open']);

    await TestBed.configureTestingModule({
      imports: [VendorDashboardComponent, NoopAnimationsModule, MatDialogModule, MatSnackBarModule],
      providers: [
        { provide: VendorService, useValue: vendorService },
        { provide: MatDialog, useValue: dialog }
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

  it('offers an edit button', () => {
    fixture.detectChanges();

    const edit = (fixture.nativeElement as HTMLElement).querySelector('[aria-label="Edit Vendor"]');
    expect(edit).not.toBeNull();
  });

  it('offers a delete button', () => {
    fixture.detectChanges();

    const remove = (fixture.nativeElement as HTMLElement).querySelector('[aria-label="Delete Vendor"]');
    expect(remove).not.toBeNull();
  });

  // LEGLINK-1808: a vendor a facility still uses cannot be deleted. The API refuses it with 409 and
  // names the facilities; a generic "500 - use the trace id" toastr told the admin nothing they
  // could act on.
  it('names the facilities in a dialog when the API refuses the delete', () => {
    dialogClosingWith(true);
    vendorService.deleteVendor.and.returnValue(throwError(() => ({
      status: 409,
      error: {
        detail: "Vendor 'Epic' is in use by 1 facility configuration and cannot be deleted: "
          + 'ABC123 (Mercy General, version 2026.1). '
          + 'Clear the vendor version on each facility before deleting it.'
      },
      message: 'the toastr text, which the dialog replaces'
    })));
    fixture.detectChanges();

    component.onDelete(vendors[0]);

    const [opened, config] = dialog.open.calls.mostRecent().args as [unknown, any];
    expect(opened).toBe(AlertDialogComponent);
    expect(config.data.title).toBe('Vendor In Use');
    //the vendor by name, as the API names it: the row the admin clicked, not its id.
    expect(config.data.message).toContain("Vendor 'Epic' is in use");
    expect(config.data.message).toContain('ABC123 (Mercy General, version 2026.1)');
    expect(config.data.message).toContain('Clear the vendor version on each facility');
  });

  // An unexpected failure has no reason to show, so the trace id is the one useful thing in it.
  it('keeps the trace id in view when the delete fails unexpectedly', () => {
    dialogClosingWith(true);
    vendorService.deleteVendor.and.returnValue(throwError(() => ({
      status: 500,
      error: { detail: 'An error occured in our API. Please use the trace id when requesting assistence.' },
      message: 'An error occured in our API. Please use the trace id when requesting assistence.\nTrace ID: 00-abc'
    })));
    fixture.detectChanges();

    component.onDelete(vendors[0]);

    const [opened, config] = dialog.open.calls.mostRecent().args as [unknown, any];
    expect(opened).toBe(AlertDialogComponent);
    expect(config.data.title).toBe('Delete Failed');
    expect(config.data.message).toContain('Trace ID: 00-abc');
  });

  it('reports a successful delete and refreshes the list', () => {
    dialogClosingWith(true);
    vendorService.deleteVendor.and.returnValue(of({ success: true, message: '' } as any));
    fixture.detectChanges();
    vendorService.getVendors.calls.reset();

    component.onDelete(vendors[0]);

    expect(vendorService.deleteVendor).toHaveBeenCalledWith('vendor-1');
    expect(vendorService.getVendors).toHaveBeenCalled();
    expect(dialog.open.calls.mostRecent().args[0]).not.toBe(AlertDialogComponent);
  });
});
