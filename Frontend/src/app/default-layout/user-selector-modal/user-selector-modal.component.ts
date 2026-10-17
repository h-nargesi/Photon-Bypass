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
  SpinnerComponent,
} from '@coreui/angular';
import { IconDirective } from '@coreui/icons-angular';
import { ICON_SUBSET } from '../../@icons';
import { SubUsers, UserModel } from '../../@models';
import { TranslationPipe, UserService } from '../../@services';
import { UsersService } from '../../management/users/users.service';

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
    SpinnerComponent,
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

  /// admins pick from the whole user list, not just their own targetArea
  /// (docs/20 phase 6 step 22)
  admin = false;
  loading = false;
  admin_names: { [username: string]: string } = {};

  @Input() visible: boolean = false;
  @ViewChild(ModalComponent) modal!: ModalComponent;
  @ViewChild('searchInput') searchInput!: ElementRef;

  constructor(
    private readonly user_service: UserService,
    private readonly users_service: UsersService,
  ) {}

  async ngOnInit() {
    this.current_user = await this.user_service.user();
    this.admin = this.user_service.isAdmin;

    if (this.admin) {
      this.search();
      return;
    }

    if (!this.current_user?.targetArea) return;

    this.usernameList = this.getUserTitle(this.current_user.targetArea);
  }

  get targetName(): string | undefined {
    return this.user_service.targetName;
  }

  search() {
    const key = this.searchInput?.nativeElement?.value;

    if (this.admin) {
      this.loading = true;
      this.users_service.list(key, 1).subscribe((result) => {
        this.loading = false;
        const rows = result?.items ?? [];
        this.usernameList = rows.map((row) => row.username);
        this.admin_names = {};
        for (const row of rows) {
          this.admin_names[row.username] = [row.name, row.surname]
            .filter(Boolean)
            .join(' ');
        }
      });
      return;
    }

    if (!this.current_user?.targetArea) return;

    this.usernameList = this.getUserTitle(this.current_user.targetArea, key);
  }

  fullnameOf(username: string): string {
    if (this.admin) return this.admin_names[username] ?? '';
    return this.current_user?.targetArea?.[username]?.fullname ?? '';
  }

  setTargetIndex(username: string) {
    if (this.admin) {
      this.user_service.setExternalTarget({
        username,
        fullname: this.fullnameOf(username) || username,
        email: '',
      });
    } else {
      this.user_service.setTraget(username);
    }
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
