import { CommonModule, NgTemplateOutlet } from '@angular/common';
import { Component, computed, inject, OnInit } from '@angular/core';
import { Title } from '@angular/platform-browser';
import { RouterLink } from '@angular/router';

import {
  AlertComponent,
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
import { printMoney, TranslationPipe, UserService } from '../../@services';

@Component({
  selector: 'app-default-header',
  templateUrl: './default-header.component.html',
  imports: [
    CommonModule,
    ContainerComponent,
    IconDirective,
    HeaderNavComponent,
    NavLinkDirective,
    RouterLink,
    NgTemplateOutlet,
    DropdownComponent,
    DropdownToggleDirective,
    DropdownMenuDirective,
    DropdownItemDirective,
    AlertComponent,
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

  async ngOnInit() {
    this.current_user = await this.user_service.user();
  }

  getTarget(): string | undefined {
    return this.user_service.Target;
  }

  setTarget(index: number) {
    this.user_service.setTraget(index);
  }

  showBalance(): string {
    return printMoney(this.current_user?.balance);
  }
}
