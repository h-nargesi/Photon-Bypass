import { CommonModule, NgStyle } from '@angular/common';
import { Component, OnInit } from '@angular/core';
import {
  BorderDirective,
  ButtonDirective,
  CardBodyComponent,
  CardComponent,
  CardFooterComponent,
  CardGroupComponent,
  CardHeaderComponent,
  CardImgDirective,
  CardLinkDirective,
  CardSubtitleDirective,
  CardTextDirective,
  CardTitleDirective,
  ColComponent,
  ColDirective,
  GutterDirective,
  ListGroupDirective,
  ListGroupItemDirective,
  RowComponent,
  TabDirective,
  TabPanelComponent,
  TabsComponent,
  TabsContentComponent,
  TabsListComponent,
  TooltipDirective,
  WidgetStatCComponent,
  WidgetStatDComponent,
} from '@coreui/angular';
import { ChartjsComponent } from '@coreui/angular-chartjs';
import { IconDirective } from '@coreui/icons-angular';
import { ICON_SUBSET } from '../@icons/icon-subset';
import { UserModel } from '../@models';
import { TranslationPipe, UserService } from '../@services';

@Component({
  selector: 'app-user',
  imports: [
    NgStyle,
    CommonModule,
    RowComponent,
    ColComponent,
    CardComponent,
    CardBodyComponent,
    ColDirective,
    IconDirective,
    TooltipDirective,
    ChartjsComponent,
    BorderDirective,
    ButtonDirective,
    CardFooterComponent,
    CardGroupComponent,
    CardHeaderComponent,
    CardImgDirective,
    CardLinkDirective,
    CardSubtitleDirective,
    CardTextDirective,
    CardTitleDirective,
    GutterDirective,
    ListGroupDirective,
    ListGroupItemDirective,
    TabDirective,
    TabPanelComponent,
    TabsComponent,
    TabsContentComponent,
    TabsListComponent,
    WidgetStatCComponent,
    WidgetStatDComponent,
    TranslationPipe,
  ],
  templateUrl: './dashboard.component.html',
  styleUrl: './dashboard.component.scss',
})
export class DashboardComponent implements OnInit {
  current_user: UserModel = {} as UserModel;
  readonly icons = ICON_SUBSET;

  constructor(private readonly user_service: UserService) {}

  ngOnInit(): void {
    this.user_service.user().subscribe((user) => (this.current_user = user));
  }

  sendCertificateViaEmail(): void {}

  changeOvenVPNPassword(): void {}
}
