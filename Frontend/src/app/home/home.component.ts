import { Component } from '@angular/core';
import {
  ColComponent,
  ContainerComponent,
  RowComponent
} from '@coreui/angular';

@Component({
  selector: 'app-home',
  imports: [
    ContainerComponent,
    RowComponent,
    ColComponent,
  ],
  templateUrl: './home.component.html',
  styleUrl: './home.component.scss'
})
export class HomeComponent {

}
