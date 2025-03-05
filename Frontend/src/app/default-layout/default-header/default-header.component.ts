import { NgTemplateOutlet } from '@angular/common';
import { Component, computed, inject } from '@angular/core';
import { Title } from '@angular/platform-browser';
import { RouterLink } from '@angular/router';

import {
  BreadcrumbRouterComponent,
  ColorModeService,
  ContainerComponent,
  DropdownComponent,
  DropdownItemDirective,
  DropdownMenuDirective,
  DropdownToggleDirective,
  HeaderComponent,
  HeaderNavComponent,
  NavLinkDirective,
} from '@coreui/angular';

import { IconDirective } from '@coreui/icons-angular';
import { iconSubset } from '../../@icons/icon-subset';

@Component({
  selector: 'app-default-header',
  templateUrl: './default-header.component.html',
  imports: [
    ContainerComponent,
    IconDirective,
    HeaderNavComponent,
    NavLinkDirective,
    RouterLink,
    NgTemplateOutlet,
    BreadcrumbRouterComponent,
    DropdownComponent,
    DropdownToggleDirective,
    DropdownMenuDirective,
    DropdownItemDirective,
  ],
})
export class DefaultHeaderComponent extends HeaderComponent {
  constructor(public readonly titleService: Title) {
    super();
  }

  readonly #colorModeService = inject(ColorModeService);
  readonly colorMode = this.#colorModeService.colorMode;

  readonly icons = iconSubset;

  readonly colorModes = [
    { name: 'light', text: 'Light', icon: this.icons.cilSun },
    { name: 'dark', text: 'Dark', icon: this.icons.cilMoon },
    { name: 'auto', text: 'Auto', icon: this.icons.cilContrast },
  ];

  readonly icon_cilSun = computed(() => {
    const currentMode = this.colorMode();
    return (
      this.colorModes.find((mode) => mode.name === currentMode)?.icon ??
      this.icons.cilSun
    );
  });
}
