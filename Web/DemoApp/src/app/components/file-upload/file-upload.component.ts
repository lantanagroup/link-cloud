import {Component, EventEmitter, Input, Output} from '@angular/core';
import {MatFormFieldModule} from '@angular/material/form-field';
import {CommonModule} from '@angular/common';
import {MatButtonModule} from '@angular/material/button';
import {MatIconModule} from '@angular/material/icon';
import {MatInputModule} from '@angular/material/input';
import {MatTooltipModule} from '@angular/material/tooltip';

@Component({
  selector: 'file-upload',
  standalone: true,
  imports: [
    CommonModule,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    MatIconModule,
    MatTooltipModule,
  ],
  templateUrl: './file-upload.component.html',
  styleUrls: ['./file-upload.component.scss']
})


export class FileUploadComponent {

  @Input() file: any;

  @Input() fileName = '';

  @Input() viewOnly = false;


  disabled: boolean = false;

  @Output() fileChange = new EventEmitter<any>();

  onClick(fileUpload: HTMLInputElement) {
    fileUpload.click();
  }

  clearFile() {
    this.file = null;
    this.fileChange.emit(null);
  }


  onFileSelected(event: any) {

    const file: File = event.target.files[0];

    if (file) {

      const reader = new FileReader();

      reader.onload = (e: any) => {

        const result = e.target.result;

        try {
          this.file = JSON.parse(result);

          this.fileChange.emit(this.file);
        }
        catch (ex) {
          console.error(ex);
        }
      }

      reader.readAsText(file);

      this.fileName = file.name;

    }

  }

}
