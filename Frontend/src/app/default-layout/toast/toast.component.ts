import { CommonModule } from '@angular/common';
import { Component, Input } from '@angular/core';
import {
  Colors,
  TextColorDirective,
  ToastBodyComponent,
  ToastComponent,
  ToastHeaderComponent,
} from '@coreui/angular';

@Component({
  selector: 'app-toast',
  imports: [
    CommonModule,
    ToastBodyComponent,
    ToastHeaderComponent,
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
