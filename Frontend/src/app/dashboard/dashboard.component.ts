import { CommonModule, NgStyle } from '@angular/common';
import { Component, OnInit } from '@angular/core';
import {
  BgColorDirective,
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
  PlaceholderAnimationDirective,
  PlaceholderDirective,
  RowComponent,
  SpinnerComponent,
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
import { ICON_SUBSET } from '../@icons';
import { UserModel } from '../@models';
import { TranslationPipe, UserService } from '../@services';
import { DashboardService } from './dashboard.service';
import { RouterLink } from '@angular/router';

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
    PlaceholderDirective,
    RowComponent,
    ColComponent,
    CardComponent,
    CardBodyComponent,
    CardImgDirective,
    CardTitleDirective,
    CardTextDirective,
    ButtonDirective,
    ColDirective,
    RouterLink,
    PlaceholderAnimationDirective,
    PlaceholderDirective,
    BgColorDirective,
    SpinnerComponent,
  ],
  providers: [DashboardService],
  templateUrl: './dashboard.component.html',
  styleUrl: './dashboard.component.scss',
})
export class DashboardComponent implements OnInit {
  current_user: UserModel = {} as UserModel;
  sending_cert_email = false;
  readonly icons = ICON_SUBSET;

  constructor(
    private readonly user_service: UserService,
    private readonly service: DashboardService
  ) {}

  ngOnInit(): void {
    this.user_service.user().subscribe((user) => (this.current_user = user));
  }

  sendCertificateViaEmail(): void {
    this.sending_cert_email = true;
    this.service
      .sendCertificateViaEmail()
      .subscribe(() => (this.sending_cert_email = false));
  }
}
