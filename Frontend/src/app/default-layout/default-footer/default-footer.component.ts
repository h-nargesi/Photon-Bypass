import { Component } from '@angular/core';
import { Title } from '@angular/platform-browser';
import { FooterComponent } from '@coreui/angular';

@Component({
  selector: 'app-default-footer',
  templateUrl: './default-footer.component.html',
  styleUrls: ['./default-footer.component.scss'],
})
export class DefaultFooterComponent extends FooterComponent {
  constructor(public readonly title_service: Title) {
    super();
  }
}
