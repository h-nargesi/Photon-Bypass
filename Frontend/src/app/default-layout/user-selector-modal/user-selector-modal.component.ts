import { Component, ElementRef, Input, ViewChild } from '@angular/core';
import {
  ButtonCloseDirective,
  FormControlDirective,
  ListGroupDirective,
  ListGroupItemDirective,
  ModalBodyComponent,
  ModalComponent,
  ModalHeaderComponent,
  ModalToggleDirective,
} from '@coreui/angular';
import { IconDirective } from '@coreui/icons-angular';
import { ICON_SUBSET } from '../../@icons';
import { UserModel } from '../../@models';
import { TranslationPipe, UserService } from '../../@services';

@Component({
  selector: 'app-user-selector-modal',
  imports: [
    ModalComponent,
    ModalHeaderComponent,
    ModalBodyComponent,
    ModalToggleDirective,
    ButtonCloseDirective,
    FormControlDirective,
    IconDirective,
    ListGroupDirective,
    ListGroupItemDirective,
    TranslationPipe,
  ],
  templateUrl: './user-selector-modal.component.html',
  styleUrl: './user-selector-modal.component.scss',
})
export class UserSelectorModalComponent {
  readonly icons = ICON_SUBSET;
  readonly id: string = 'userSelectorModal';

  current_user?: UserModel;
  targetList?: string[];

  @Input() visible: boolean = false;
  @ViewChild(ModalComponent) modal!: ModalComponent;
  @ViewChild('searchInput') searchInput!: ElementRef;

  constructor(private readonly user_service: UserService) {}

  async ngOnInit() {
    this.current_user = await this.user_service.user();
    this.targetList = this.current_user.targetArea?.slice();
  }

  get Target(): string | undefined {
    return this.user_service.Target;
  }

  search() {
    if (!this.current_user?.targetArea) return;

    const key = this.searchInput.nativeElement.value;
    this.targetList = this.current_user.targetArea
      ?.filter((x) => x.includes(key))
      .slice();
  }

  setTargetIndex(index: number) {
    this.user_service.setTraget(index);
    this.visible = false;
  }
}
