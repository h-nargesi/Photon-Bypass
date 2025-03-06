import { NgStyle } from '@angular/common';
import { Component } from '@angular/core';
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
} from '@coreui/angular';
import { ChartjsComponent } from '@coreui/angular-chartjs';
import { IconDirective } from '@coreui/icons-angular';
import { UserService } from '../@services';

@Component({
  selector: 'app-user',
  imports: [
    NgStyle,
    RowComponent,
    ColComponent,
    CardComponent,
    CardBodyComponent,
    ColDirective,
    IconDirective,
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
  ],
  templateUrl: './dashboard.component.html',
  styleUrl: './dashboard.component.scss',
})
export class DashboardComponent {

  constructor(private readonly user_service: UserService){}
}
