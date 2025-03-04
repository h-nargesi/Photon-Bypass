import { CommonModule } from '@angular/common';
import { Component } from '@angular/core';
import {
  ColComponent,
  ContainerComponent,
  RowComponent,
} from '@coreui/angular';
import { TranslationPipe, TranslationService } from '../@services';

@Component({
  selector: 'app-home',
  imports: [
    CommonModule,
    ContainerComponent,
    RowComponent,
    ColComponent,
    TranslationPipe,
  ],
  templateUrl: './home.component.html',
  styleUrl: './home.component.scss',
})
export class HomeComponent {
  constructor(private readonly translation: TranslationService) {}

  public lines(key: string): string[] {
    return this.translation.lines(key);
  }

  public data(key: string): any {
    return this.translation.data(key);
  }

  public getTitle(item: any): any {
    return item.title;
  }
}
