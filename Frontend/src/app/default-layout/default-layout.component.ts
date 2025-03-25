import { Component, ViewChild } from '@angular/core';
import { RouterOutlet } from '@angular/router';

import {
  ContainerComponent,
  ShadowOnScrollDirective,
  ToasterComponent,
  ToasterPlacement,
  ToastModule,
} from '@coreui/angular';

import { NgClass } from '@angular/common';
import { MessageService } from '../@services';
import { DefaultFooterComponent, DefaultHeaderComponent } from './';
import { UserSelectorModalComponent } from './user-selector-modal/user-selector-modal.component';

@Component({
  selector: 'app-dashboard',
  templateUrl: './default-layout.component.html',
  styleUrls: ['./default-layout.component.scss'],
  imports: [
    NgClass,
    ContainerComponent,
    DefaultFooterComponent,
    DefaultHeaderComponent,
    UserSelectorModalComponent,
    RouterOutlet,
    ShadowOnScrollDirective,
    ToastModule,
    ToasterComponent,
  ],
})
export class DefaultLayoutComponent {
  @ViewChild(ToasterComponent) toaster!: ToasterComponent;

  constructor(private readonly message_service: MessageService) {}

  ngAfterViewInit() {
    this.message_service.registerToaster(this.toaster);
  }

  readonly placement: ToasterPlacement = ToasterPlacement.TopEnd;
}
