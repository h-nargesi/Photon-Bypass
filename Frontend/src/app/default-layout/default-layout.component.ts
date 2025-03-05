import { Component } from '@angular/core';
import { RouterOutlet } from '@angular/router';

import { ContainerComponent, ShadowOnScrollDirective } from '@coreui/angular';

import { DefaultFooterComponent, DefaultHeaderComponent } from './';

@Component({
  selector: 'app-dashboard',
  templateUrl: './default-layout.component.html',
  styleUrls: ['./default-layout.component.scss'],
  imports: [
    ContainerComponent,
    DefaultFooterComponent,
    DefaultHeaderComponent,
    RouterOutlet,
    ShadowOnScrollDirective,
  ],
})
export class DefaultLayoutComponent {}
