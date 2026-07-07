import {ChangeDetectorRef, Component, Inject} from '@angular/core';
import {MAT_DIALOG_DATA, MatDialogModule, MatDialogRef} from '@angular/material/dialog';
import {MatButton} from '@angular/material/button';
import {MatIcon} from '@angular/material/icon';
import {MatProgressSpinner} from '@angular/material/progress-spinner';
import {FileUploadComponent} from '../../../../core/file-upload/file-upload.component';
import {OperationService} from '../../../../../services/gateway/normalization/operation.service';
import {ISaveOperationModel} from '../../../../../interfaces/normalization/operation-save-model.interface';
import {OperationType} from '../../../../../interfaces/normalization/operation-type-enumeration';
import {RemoveExtensionsOperation} from '../../../../../interfaces/normalization/remove-extensions-operation-interface';
import {firstValueFrom, map} from 'rxjs';
import {IOperationModel} from '../../../../../interfaces/normalization/operation-get-model.interface';
import * as Papa from 'papaparse';

interface CsvOperationGroup {
  resourceType: string;
  urls: string[];
  invalidUrls: string[];
  status: 'pending' | 'submitting' | 'success' | 'error';
  errorMessage?: string;
}

interface ConflictInfo {
  resourceType: string;
  url: string;
}

export interface CsvImportDialogData {
  facilityId?: string;
  isVendorMode: boolean;
  vendorIds: string[];
}

export interface CsvImportDialogResult {
  created: number;
  failed: number;
}

@Component({
  selector: 'app-remove-extensions-csv-import-dialog',
  standalone: true,
  imports: [MatDialogModule, MatButton, MatIcon, MatProgressSpinner, FileUploadComponent],
  templateUrl: './remove-extensions-csv-import-dialog.component.html',
  styleUrl: './remove-extensions-csv-import-dialog.component.scss'
})
export class RemoveExtensionsCsvImportDialogComponent {

  stage: 'upload' | 'preview' | 'submitting' | 'results' = 'upload';
  groups: CsvOperationGroup[] = [];
  conflicts: ConflictInfo[] = [];
  isChecking = false;
  parseError = '';
  created = 0;
  failed = 0;

  constructor(
    @Inject(MAT_DIALOG_DATA) public data: CsvImportDialogData,
    private dialogRef: MatDialogRef<RemoveExtensionsCsvImportDialogComponent>,
    private operationService: OperationService,
    private cdr: ChangeDetectorRef
  ) {}

  get totalUrlCount(): number {
    return this.groups.reduce((sum, g) => sum + g.urls.length, 0);
  }

  get hasInvalidUrls(): boolean {
    return this.groups.some(g => g.invalidUrls.length > 0);
  }

  get conflictGroups(): {resourceType: string; urls: string[]}[] {
    const grouped = new Map<string, string[]>();
    for (const c of this.conflicts) {
      if (!grouped.has(c.resourceType)) grouped.set(c.resourceType, []);
      grouped.get(c.resourceType)!.push(c.url);
    }
    return Array.from(grouped.entries()).map(([resourceType, urls]) => ({resourceType, urls}));
  }

  onFileSelected(file: File | null): void {
    this.conflicts = [];
    this.parseError = '';
    if (!file) {
      this.groups = [];
      return;
    }
    if (!file.name.toLowerCase().endsWith('.csv')) {
      this.parseError = 'Please select a valid CSV file (.csv extension).';
      this.groups = [];
      return;
    }
    const reader = new FileReader();
    reader.readAsText(file);
    reader.onload = () => {
      Papa.parse(reader.result as string, {
        header: false,
        skipEmptyLines: true,
        complete: (result) => this.processRows(result.data as string[][])
      });
    };
    reader.onerror = () => {
      this.parseError = 'Failed to read the file.';
      this.cdr.detectChanges();
    };
  }

  private processRows(rows: string[][]): void {
    const groupMap = new Map<string, string[]>();
    for (const row of rows) {
      const resourceType = (row[0] ?? '').trim();
      const url = (row[1] ?? '').trim();
      if (!resourceType || !url) continue;
      if (!groupMap.has(resourceType)) groupMap.set(resourceType, []);
      groupMap.get(resourceType)!.push(url);
    }

    this.groups = Array.from(groupMap.entries()).map(([resourceType, urls]) => ({
      resourceType,
      urls,
      invalidUrls: urls.filter(u => !this.isAbsoluteUrl(u)),
      status: 'pending'
    }));

    if (this.groups.length === 0) {
      this.parseError = 'No valid rows found. Ensure column 1 is the resource type and column 2 is the URL.';
      this.cdr.detectChanges();
      return;
    }

    this.isChecking = true;
    this.cdr.detectChanges();

    this.checkForConflicts()
      .then(conflicts => {
        this.isChecking = false;
        if (conflicts.length > 0) {
          this.conflicts = conflicts;
        } else {
          this.stage = 'preview';
        }
        this.cdr.detectChanges();
      })
      .catch(() => {
        this.isChecking = false;
        this.parseError = 'Failed to check for existing operations. Please try again.';
        this.cdr.detectChanges();
      });
  }

  private isAbsoluteUrl(url: string): boolean {
    try {
      new URL(url);
      return true;
    } catch {
      return false;
    }
  }

  private async checkForConflicts(): Promise<ConflictInfo[]> {
    const existing = await this.fetchExistingOperations();
    const conflicts: ConflictInfo[] = [];

    const csvPairs = new Set<string>();
    for (const group of this.groups) {
      for (const url of group.urls) {
        csvPairs.add(`${group.resourceType}::${url}`);
      }
    }

    const matchedPairs = new Set<string>();
    for (const op of existing) {
      const parsedOp = op.parsedOperationJson as RemoveExtensionsOperation;
      const resourceTypes = op.operationResourceTypes
        ?.map(rt => rt.resource?.resourceName)
        .filter((n): n is string => !!n) ?? [];
      const urls = parsedOp?.ExtensionUrls ?? [];

      for (const rt of resourceTypes) {
        for (const url of urls) {
          const pairKey = `${rt}::${url}`;
          if (csvPairs.has(pairKey) && !matchedPairs.has(pairKey)) {
            matchedPairs.add(pairKey);
            conflicts.push({resourceType: rt, url});
          }
        }
      }
    }

    return conflicts;
  }

  private async fetchExistingOperations(): Promise<IOperationModel[]> {
    if (!this.data.isVendorMode) {
      return this.fetchAllOperationPages(this.data.facilityId ?? null, null);
    }

    if (!this.data.vendorIds?.length) return [];

    const results = await Promise.all(
      this.data.vendorIds.map(vendorId => this.fetchAllOperationPages(null, vendorId))
    );
    return results.flat();
  }

  private async fetchAllOperationPages(facilityId: string | null, vendorId: string | null): Promise<IOperationModel[]> {
    const pageSize = 1000;
    const firstPage = await firstValueFrom(
      this.operationService.searchGlobalOperations(
        facilityId, OperationType.RemoveExtensions,
        null, null, null, vendorId, null, null, pageSize, 0
      )
    );

    const records = [...firstPage.records];
    const totalPages = firstPage.metadata?.totalPages ?? 1;

    if (totalPages > 1) {
      const remainingPages = await Promise.all(
        Array.from({length: totalPages - 1}, (_, i) =>
          firstValueFrom(
            this.operationService.searchGlobalOperations(
              facilityId, OperationType.RemoveExtensions,
              null, null, null, vendorId, null, null, pageSize, i + 1
            ).pipe(map(r => r.records))
          )
        )
      );
      records.push(...remainingPages.flat());
    }

    return records;
  }

  back(): void {
    this.stage = 'upload';
    this.groups = [];
    this.conflicts = [];
    this.parseError = '';
  }

  async submitAll(): Promise<void> {
    this.dialogRef.disableClose = true;
    this.stage = 'submitting';
    this.created = 0;
    this.failed = 0;
    this.cdr.detectChanges();

    for (const group of this.groups) {
      group.status = 'submitting';
      this.cdr.detectChanges();

      const operation: RemoveExtensionsOperation = {
        OperationType: OperationType.RemoveExtensions.toString(),
        Name: `Remove Extensions - ${group.resourceType}`,
        Description: '',
        ExtensionUrls: group.urls
      };

      const saveModel: ISaveOperationModel = {
        facilityId: this.data.facilityId || undefined,
        description: '',
        resourceTypes: [group.resourceType],
        operation,
        isDisabled: false,
        vendorIds: this.data.vendorIds
      };

      try {
        await firstValueFrom(this.operationService.createOperationConfiguration(saveModel));
        group.status = 'success';
        this.created++;
      } catch (e: any) {
        group.status = 'error';
        group.errorMessage = e?.message ?? 'Unknown error';
        this.failed++;
      }
      this.cdr.detectChanges();
    }

    this.stage = 'results';
    this.dialogRef.disableClose = false;
    this.cdr.detectChanges();
  }

  close(): void {
    const result: CsvImportDialogResult | null = this.stage === 'results'
      ? {created: this.created, failed: this.failed}
      : null;
    this.dialogRef.close(result);
  }
}
