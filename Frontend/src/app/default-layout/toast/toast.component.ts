import { CommonModule } from '@angular/common';
import { Component, Input, ViewChild } from '@angular/core';
import {
  Colors,
  TextColorDirective,
  ToastBodyComponent,
  ToastCloseDirective,
  ToastComponent,
  ToastHeaderComponent,
} from '@coreui/angular';

@Component({
  selector: 'app-toast',
  imports: [
    CommonModule,
    ToastBodyComponent,
    ToastHeaderComponent,
    ToastCloseDirective,
    TextColorDirective,
  ],
  templateUrl: './toast.component.html',
  styleUrl: './toast.component.scss',
})
export class AppToastComponent extends ToastComponent {
  @Input() title?: string;
  @Input() body?: string;

  headerClick() {
    super.onClose();
  }

  getTextColor(): Colors {
    if (this.color() === 'warning') return 'secondary';
    return 'light';
  }
}
