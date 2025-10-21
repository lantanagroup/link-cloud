import {Component, OnInit, ViewChild} from '@angular/core';
import {CommonModule} from '@angular/common';
import {MatTableDataSource, MatTableModule} from '@angular/material/table';
import {MatSort, MatSortModule} from '@angular/material/sort';
import {MatFormFieldModule} from '@angular/material/form-field';
import {MatInputModule} from '@angular/material/input';
import {MatSelectModule} from '@angular/material/select';
import {MatButtonModule} from '@angular/material/button';
import {MatIconModule} from '@angular/material/icon';
import {MatProgressBarModule} from '@angular/material/progress-bar';
import {MatCardModule} from '@angular/material/card';
import {FormsModule} from '@angular/forms';
import {TerminologyService} from '../../services/gateway/terminology/terminology.service';
import {ITerminologyResource} from '../../interfaces/terminology/terminology-resource.interface';

@Component({
  selector: 'app-terminology-config',
  standalone: true,
  imports: [
    CommonModule,
    MatTableModule,
    MatSortModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatButtonModule,
    MatIconModule,
    MatProgressBarModule,
    MatCardModule,
    FormsModule
  ],
  templateUrl: './terminology-config.component.html',
  styleUrls: ['./terminology-config.component.scss']
})
export class TerminologyConfigComponent implements OnInit {
  displayedColumns: string[] = ['resourceType', 'id', 'url', 'version'];
  dataSource = new MatTableDataSource<ITerminologyResource>([]);

  searchResourceType: string = '';
  searchId: string = '';
  searchUrl: string = '';
  searchVersion: string = '';

  @ViewChild(MatSort) sort!: MatSort;

  // Placeholder properties for upload
  isUploading: boolean = false;
  uploadProgress: number = 0;
  uploadStatus: string = '';

  constructor(private terminologyService: TerminologyService) { }

  ngOnInit(): void {
    // Initial data from service
    this.loadResources();

    // Set up custom filter predicate
    this.dataSource.filterPredicate = (data: ITerminologyResource, filter: string) => {
      const searchTerms = JSON.parse(filter);
      return (searchTerms.resourceType === '' || data.resourceType === searchTerms.resourceType) &&
             (data.id || '').toLowerCase().includes(searchTerms.id.toLowerCase()) &&
             (data.url || '').toLowerCase().includes(searchTerms.url.toLowerCase()) &&
             (data.version || '').toLowerCase().includes(searchTerms.version.toLowerCase());
    };
  }

  ngAfterViewInit() {
    this.dataSource.sort = this.sort;
    // Set default sort by ID
    this.sort.sort({ id: 'id', start: 'asc', disableClear: false });
  }

  loadResources() {
    this.terminologyService.getResources().subscribe((resources: ITerminologyResource[]) => {
      this.dataSource.data = resources;
    });
  }

  applyFilter() {
    const filterValue = {
      resourceType: this.searchResourceType,
      id: this.searchId,
      url: this.searchUrl,
      version: this.searchVersion
    };
    this.dataSource.filter = JSON.stringify(filterValue);
  }

  onFileSelected(event: any) {
    const file: File = event.target.files[0];
    if (file) {
      this.uploadTerminologyPackage(file);
    }
  }

  uploadTerminologyPackage(file: File) {
    /*
    // Placeholder code for when the backend DOES support this
    this.isUploading = true;
    this.uploadProgress = 0;
    this.uploadStatus = 'Parsing ZIP package...';

    // 1. Parse and validate the ZIP
    // Using a library like JSZip to iterate through directories
    // Each directory should have a .json and .csv file

    // 2. Call backend operation for each ValueSet and CodeSystem found
    // for (const resource of resourcesFound) {
    //   this.uploadStatus = `Uploading ${resource.resourceType}/${resource.id}...`;
    //   await this.terminologyService.uploadResource(resource).toPromise();
    //   this.uploadProgress += (1 / resourcesFound.length) * 100;
    // }

    this.uploadStatus = 'Upload complete';
    this.isUploading = false;
    this.loadResources();
    */
    console.log('Upload functionality is currently a placeholder.', file);
  }
}
