import { Component, EventEmitter, Output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { debounceTime, distinctUntilChanged, switchMap, Subject } from 'rxjs';
import { TenantService } from 'src/app/services/gateway/tenant/tenant.service';
import { FontAwesomeModule } from '@fortawesome/angular-fontawesome';
import { faSearch } from '@fortawesome/free-solid-svg-icons';
import { Router } from '@angular/router';

@Component({
  selector: 'tenant-search-bar',
  standalone: true,
  imports: [
    CommonModule,
     FormsModule, 
     FontAwesomeModule
  ],
  templateUrl: './tenant-search-bar.component.html',
  styleUrls: ['./tenant-search-bar.component.scss']
})
export class TenantSearchBarComponent { 

  faSearch = faSearch;
  searchTerm = '';
  facilities: Array<{ facilityId: string, facilityName: string }> = [];
  showDropdown = false;

  private searchSubject = new Subject<string>();

  constructor(private http: HttpClient, private tenantService: TenantService, private router: Router) {

    this.searchSubject.pipe(
      debounceTime(300),
      distinctUntilChanged(),
      switchMap(term => this.tenantService.autocompleteFacilities(term))
    ).subscribe(results => {   
      this.facilities = Object.entries(results).map(([facilityId, facilityName]) => ({
        facilityId,
        facilityName
      }));
      this.showDropdown = !!this.searchTerm && this.facilities.length > 0;
    });
  }

  onInput(event: Event) {
    const value = (event.target as HTMLInputElement).value;
    this.searchTerm = value;
    this.searchSubject.next(value);
  } 

  selectFacility(facility: { facilityId: string, facilityName: string }) {
    
    if(facility)
    {
      this.searchTerm = "";
      this.showDropdown = false;
      this.router.navigate(['/tenant/facility', facility.facilityId]);
    }
    
  }

  onBlur() {
    setTimeout(() => this.showDropdown = false, 200);
  }
}
