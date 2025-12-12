// file-download.service.ts
import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import {catchError, mapTo, Observable, tap} from 'rxjs';
import { map } from 'rxjs/operators';
import {throwError} from "rxjs/internal/observable/throwError";

export interface FileResponse {
  fileName: string;
  fileType: string;
  fileContent: string; // Base64 string
}

@Injectable({
  providedIn: 'root'
})
export class FileDownloadService {

  constructor(private http: HttpClient) {}

  downloadFileFromJson(url: string, params?: any): Observable<void> {
    return this.http.get(url, { params }).pipe(
      tap(jsonData => {
        const jsonString = JSON.stringify(jsonData, null, 2);
        const blob = new Blob([jsonString], { type: 'application/json' });

        const blobUrl = window.URL.createObjectURL(blob);
        const link = document.createElement('a');
        link.href = blobUrl;
        link.download = 'data.json';
        link.click();

        window.URL.revokeObjectURL(blobUrl);
      }),

     map(() => undefined),

      catchError(err => {
        console.error('JSON download failed', err);
        alert('Failed to download JSON.');
        return throwError(() => err);
      })
    );
  }

}
