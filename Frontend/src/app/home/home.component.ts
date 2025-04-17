import { CommonModule } from '@angular/common';
import { Component } from '@angular/core';
import {
  ColComponent,
  ContainerComponent,
  RowComponent,
} from '@coreui/angular';
import { IconDirective } from '@coreui/icons-angular';
import { ICON_SUBSET } from '../@icons';
import { PriceModel } from '../@models';
import { TranslationPipe, TranslationService } from '../@services';
import { HomeService } from './home.service';
import { Router } from '@angular/router';

@Component({
  selector: 'app-home',
  imports: [
    CommonModule,
    ContainerComponent,
    RowComponent,
    ColComponent,
    IconDirective,
    TranslationPipe,
  ],
  templateUrl: './home.component.html',
  styleUrl: './home.component.scss',
  providers: [HomeService],
})
export class HomeComponent {
  readonly icons = ICON_SUBSET;
  prices?: PriceModel[];

  constructor(
    private readonly translation: TranslationService,
    private readonly router: Router,
    service: HomeService
  ) {
    service.prices().subscribe((prices) => (this.prices = prices));
  }

  public lines(key: string): string[] {
    return this.translation.lines(key);
  }

  public data(key: string): any {
    return this.translation.data(key);
  }

  public getTitle(item: any): any {
    return item.title;
  }

  public renewal() {
    this.router.navigate(['renewal']);
  }
}
