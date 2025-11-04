import {Component, EventEmitter, Input, Output} from '@angular/core';
import {MatFormFieldModule} from '@angular/material/form-field';

import {MatButtonModule} from '@angular/material/button';
import {MatIconModule} from '@angular/material/icon';
import {MatInputModule} from '@angular/material/input';
import {MatTooltipModule} from '@angular/material/tooltip';

@Component({
  selector: 'file-upload',
  standalone: true,
  imports: [
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    MatIconModule,
    MatTooltipModule
],
  templateUrl: './file-upload.component.html',
  styleUrls: ['./file-upload.component.scss']
})

export class FileUploadComponent {

  file: any;
  fileName = '';
  disabled: boolean = false;

  @Output() fileChange = new EventEmitter<any>();


  onClick(fileUpload: HTMLInputElement) {
    fileUpload.click();
  }

  clearFile() {
    this.fileName = "";
    this.file = null;
    this.fileChange.emit(null);
  }


  onFileSelected(event: any) {
    const file: File = event.target.files[0];
    if (file) {
      this.file = file;
      this.fileChange.emit(this.file);
      this.fileName = file.name;
    }
  }
}
