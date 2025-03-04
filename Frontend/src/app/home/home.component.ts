import { Component } from '@angular/core';
import {
  ColComponent,
  ContainerComponent,
  RowComponent,
} from '@coreui/angular';
import { TranslationPipe, TranslationService } from '../@services';

@Component({
  selector: 'app-home',
  imports: [ContainerComponent, RowComponent, ColComponent, TranslationPipe],
  templateUrl: './home.component.html',
  styleUrl: './home.component.scss',
})
export class HomeComponent {
  public translate(key: string) {
    return TranslationService.translate(key);
  }
}
