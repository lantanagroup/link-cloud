import { Component, Input } from '@angular/core';

import { CommonModule } from '@angular/common';
import { LinkInterface } from '../../interfaces/globals.interface';

@Component({
  selector: 'vd-button',
  inputs: ['variant', 'disabled', 'type', 'onClickHandler'],
  standalone: true,
  imports: [CommonModule],
  templateUrl: './vd-button.component.html',
  styleUrls: ['./vd-button.component.scss']
})
export class VdButtonComponent {
  @Input() type: 'button' | 'submit' | 'link' = 'button';
  @Input() variant?: 'solid' | 'outline' | 'text' = 'solid';
  @Input() condensed?: boolean = false;
  @Input() disabled?: boolean = false;
  @Input() onClickHandler?: () => void = () => { };
  @Input() link?: LinkInterface;
  @Input() classes?: string = '';

  getButtonClass = () => {
    const buttonClasses = ['vd-btn']

    if (this.variant) {
      buttonClasses.push('vd-btn--' + this.variant)
    }

    if (this.condensed) {
      buttonClasses.push('vd-btn--condensed')
    }

    if (this.classes) {
      buttonClasses.push(this.classes)
    }

    return buttonClasses.join(' ')
  }
}
