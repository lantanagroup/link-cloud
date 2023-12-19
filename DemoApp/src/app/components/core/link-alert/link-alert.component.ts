import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatIconModule } from '@angular/material/icon';
import { LinkAlertType } from './link-alert-type.enum';

@Component({
  selector: 'app-link-alert',
  standalone: true,
  imports: [
    CommonModule,
    MatIconModule
  ],
  templateUrl: './link-alert.component.html',
  styleUrls: ['./link-alert.component.scss']
})
export class LinkAlertComponent {

  @Input() type!: LinkAlertType; 
  @Input() message!: string;

  constructor() { }

  get LinkAlertType(): typeof LinkAlertType {
    return LinkAlertType;
  }

  getIconName(): string {
    switch (this.type) {
      case LinkAlertType.info:
        return 'info';
      case LinkAlertType.warning:
        return 'warning';
      case LinkAlertType.error:
        return 'error';
      case LinkAlertType.success:
        return 'check_circle';
      default:
        return 'info';
    }
  }

  getAlertClass() {
    switch(this.type) {
      case LinkAlertType.info:
        return 'info-alert';
      case LinkAlertType.warning:
        return 'warning-alert';
      case LinkAlertType.error:
        return 'error-alert';
      case LinkAlertType.success:
        return 'success-alert';
      default:
        return '';
    }
  }
  
}
