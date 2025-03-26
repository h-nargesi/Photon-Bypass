import { CommonModule, NgTemplateOutlet } from '@angular/common';
import { Component, computed, inject, Input, OnInit } from '@angular/core';
import { Title } from '@angular/platform-browser';

import {
  AlertComponent,
  ButtonDirective,
  ColorModeService,
  ContainerComponent,
  DropdownComponent,
  DropdownItemDirective,
  DropdownMenuDirective,
  DropdownToggleDirective,
  HeaderComponent,
  HeaderNavComponent,
  ModalToggleDirective,
  NavLinkDirective,
  TextColorDirective,
  TooltipDirective,
} from '@coreui/angular';

import { RouterLink } from '@angular/router';
import { IconDirective } from '@coreui/icons-angular';
import { ICON_SUBSET } from '../../@icons';
import { UserModel } from '../../@models';
import { printMoney, TranslationPipe, UserService } from '../../@services';

@Component({
  selector: 'app-default-header',
  templateUrl: './default-header.component.html',
  imports: [
    CommonModule,
    NgTemplateOutlet,
    NavLinkDirective,
    RouterLink,
    ContainerComponent,
    IconDirective,
    HeaderNavComponent,
    DropdownComponent,
    DropdownToggleDirective,
    DropdownMenuDirective,
    DropdownItemDirective,
    ButtonDirective,
    TextColorDirective,
    AlertComponent,
    ModalToggleDirective,
    TooltipDirective,
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

  @Input() userSelector?: string;

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

  get hasSubUsers(): boolean {
    return this.user_service.hasSubUsers;
  }

  get targetName(): string | undefined {
    return this.user_service.targetName;
  }

  showBalance(): string {
    return printMoney(this.current_user?.balance);
  }
}
