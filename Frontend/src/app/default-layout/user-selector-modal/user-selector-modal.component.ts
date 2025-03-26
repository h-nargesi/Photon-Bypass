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
import { SubUsers, UserModel } from '../../@models';
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
  usernameList?: string[];

  @Input() visible: boolean = false;
  @ViewChild(ModalComponent) modal!: ModalComponent;
  @ViewChild('searchInput') searchInput!: ElementRef;

  constructor(private readonly user_service: UserService) {}

  async ngOnInit() {
    this.current_user = await this.user_service.user();
    if (!this.current_user?.targetArea) return;

    this.usernameList = this.getUserTitle(this.current_user.targetArea);
  }

  get targetName(): string | undefined {
    return this.user_service.targetName;
  }

  search() {
    if (!this.current_user?.targetArea) return;

    const key = this.searchInput.nativeElement.value;
    this.usernameList = this.getUserTitle(this.current_user.targetArea, key);
  }

  setTargetIndex(username: string) {
    this.user_service.setTraget(username);
    this.visible = false;
  }

  private getUserTitle(sub_users: SubUsers, key?: string): string[] {
    const targetList: string[] = [];
    for (const username in sub_users) {
      if (
        !key ||
        username.includes(key) ||
        sub_users[username].fullname.includes(key)
      ) {
        targetList.push(username);
      }
    }
    return targetList;
  }
}
