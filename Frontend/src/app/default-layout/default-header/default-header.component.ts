import { NgTemplateOutlet } from '@angular/common';
import { Component, computed, inject, OnInit } from '@angular/core';
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

import { ICON_SUBSET } from '../../@icons';
import { UserModel } from '../../@models';
import { TranslationPipe, UserService } from '../../@services';

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
    TranslationPipe,
  ],
})
export class DefaultHeaderComponent extends HeaderComponent implements OnInit {
  readonly icons = ICON_SUBSET;

  readonly #colorModeService = inject(ColorModeService);
  readonly colorMode = this.#colorModeService.colorMode;

  readonly colorModes = [
    { name: 'light', text: 'Light', icon: this.icons.cilSun },
    { name: 'dark', text: 'Dark', icon: this.icons.cilMoon },
    { name: 'auto', text: 'Auto', icon: this.icons.cilContrast },
  ];

  current_user?: UserModel;

  constructor(
    public readonly title_service: Title,
    private readonly user_service: UserService
  ) {
    super();
  }

  readonly icon_cilSun = computed(() => {
    const currentMode = this.colorMode();
    return (
      this.colorModes.find((mode) => mode.name === currentMode)?.icon ??
      this.icons.cilSun
    );
  });

  ngOnInit(): void {
    this.user_service.user().subscribe((user) => (this.current_user = user));
  }
}
