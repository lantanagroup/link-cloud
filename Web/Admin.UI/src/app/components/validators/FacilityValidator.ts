import { AbstractControl, ValidationErrors } from '@angular/forms';
import {Observable, of, tap} from 'rxjs';
import {debounceTime, switchMap, map, delay} from 'rxjs/operators';
import {TenantService} from "../../services/gateway/tenant/tenant.service";


export function facilityExistsValidator(tenantService: TenantService) {
  return (control: AbstractControl): Observable<ValidationErrors | null> => {
    if (!control.value) {
      return of(null); // If no value, no validation needed
    }
    return of(control.value).pipe(
      debounceTime(300), // Avoid too many API calls
      switchMap((facility) =>
        tenantService.checkFacility(facility).pipe(
          tap(_ => console.log(`Fetched facilities.`)),
          map(
            (exists) => (exists? null: { notFound : true})
        )
       )
      )
    );
  };
}
