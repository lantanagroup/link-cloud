import {Component, Input, OnDestroy, OnInit} from '@angular/core';
import {FormBuilder, FormGroup, ReactiveFormsModule} from '@angular/forms';
import {MatTableDataSource, MatTableModule} from '@angular/material/table';
import {MatFormFieldModule} from '@angular/material/form-field';
import {MatInputModule} from '@angular/material/input';
import {MatCheckboxModule} from '@angular/material/checkbox';
import {MatProgressSpinnerModule} from '@angular/material/progress-spinner';
import {MatIconModule} from '@angular/material/icon';
import {Subscription} from 'rxjs';
import {ValidationService} from '../../../services/gateway/validation/validation.service';
import {ITerminologyDependency} from '../../../interfaces/validation/terminology-dependency.interface';

interface TxDependencyRow extends ITerminologyDependency {
  foundResource: boolean;
  foundVersion: boolean;
}

interface TxDependencyFilters {
  search: string;
  missingResources: boolean;
  missingVersions: boolean;
}

@Component({
  selector: 'app-tx-dependencies',
  standalone: true,
  imports: [
    ReactiveFormsModule,
    MatTableModule,
    MatFormFieldModule,
    MatInputModule,
    MatCheckboxModule,
    MatProgressSpinnerModule,
    MatIconModule
  ],
  templateUrl: './tx-dependencies.component.html',
  styleUrls: ['./tx-dependencies.component.scss']
})
export class TxDependenciesComponent implements OnInit, OnDestroy {
  @Input({ required: true }) package!: string;
  filters: FormGroup;
  dataSource = new MatTableDataSource<TxDependencyRow>([]);
  displayedColumns: string[] = ['url', 'version', 'foundResource', 'foundVersion'];
  loading = false;
  errorMessage = '';

  private subscriptions = new Subscription();

  constructor(
    private validationService: ValidationService,
    private formBuilder: FormBuilder
  ) {
    this.filters = this.formBuilder.group({
      search: [''],
      missingResources: [false],
      missingVersions: [false]
    });
  }

  ngOnInit(): void {
    this.dataSource.filterPredicate = this.buildFilterPredicate();

    this.subscriptions.add(
      this.filters.valueChanges.subscribe(() => this.applyFilters())
    );

    this.loadDependencies();
  }

  ngOnDestroy(): void {
    this.subscriptions.unsubscribe();
  }

  get hasData(): boolean {
    return this.dataSource.data.length > 0;
  }

  get hasFilteredData(): boolean {
    return this.dataSource.filteredData.length > 0;
  }

  private loadDependencies(): void {
    if (!this.package || !this.package.trim()) {
      this.errorMessage = 'Please select a package to view TX dependencies.';
      return;
    }

    this.loading = true;
    this.errorMessage = '';

    this.subscriptions.add(
      this.validationService.getTxDependencies(this.package.trim()).subscribe({
        next: (dependencies) => {
          this.dataSource.data = dependencies.map((dependency) => this.mapRow(dependency));
          this.loading = false;
          this.applyFilters();
        },
        error: (error) => {
          console.error('Failed to load TX dependencies:', error);
          this.errorMessage = 'Failed to load TX dependencies.';
          this.loading = false;
        }
      })
    );
  }

  private applyFilters(): void {
    const filterValue: TxDependencyFilters = {
      search: (this.filters.get('search')?.value ?? '').toString(),
      missingResources: !!this.filters.get('missingResources')?.value,
      missingVersions: !!this.filters.get('missingVersions')?.value
    };

    this.dataSource.filter = JSON.stringify(filterValue);
  }

  private buildFilterPredicate() {
    return (data: TxDependencyRow, filter: string): boolean => {
      if (!filter) {
        return true;
      }

      let criteria: TxDependencyFilters;
      try {
        criteria = JSON.parse(filter) as TxDependencyFilters;
      } catch {
        return true;
      }
      const searchText = (criteria.search ?? '').trim().toLowerCase();
      const url = (data.url ?? '').toLowerCase();

      const matchesSearch = !searchText || url.includes(searchText);

      const missingResource = !data.foundResource;
      const missingVersion = data.foundResource && !data.foundVersion;

      let matchesMissing = true;
      if (criteria.missingResources && criteria.missingVersions) {
        matchesMissing = missingResource || missingVersion;
      } else if (criteria.missingResources) {
        matchesMissing = missingResource;
      } else if (criteria.missingVersions) {
        matchesMissing = missingVersion;
      }

      return matchesSearch && matchesMissing;
    };
  }

  private mapRow(dependency: ITerminologyDependency): TxDependencyRow {
    const hasVersion = !!dependency.version;
    const foundResource = dependency.resourceExists;
    const foundVersion = foundResource && (!hasVersion || dependency.versionExists);

    return {
      ...dependency,
      foundResource,
      foundVersion
    };
  }
}
