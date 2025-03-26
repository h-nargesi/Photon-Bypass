import { CommonModule } from '@angular/common';
import { Component, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import {
  BorderDirective,
  ButtonDirective,
  CardBodyComponent,
  CardComponent,
  CardFooterComponent,
  CardHeaderComponent,
  ColComponent,
  RowComponent,
} from '@coreui/angular';
import { printMoney, TranslationPipe } from '../@services';

@Component({
  selector: 'app-payment',
  imports: [
    CommonModule,
    FormsModule,
    RowComponent,
    ColComponent,
    CardComponent,
    CardHeaderComponent,
    CardBodyComponent,
    CardFooterComponent,
    BorderDirective,
    ButtonDirective,
    TranslationPipe,
  ],
  templateUrl: './payment.component.html',
  styleUrl: './payment.component.scss',
})
export class PaymentComponent implements OnInit {
  sum: number = 0;
  tax: number = 0.1;
  items?: [
    {
      title: string;
      value: number;
    }
  ];

  ngOnInit() {
    this.items = [
      {
        title: 'افزایش ترافیک اکانت به مقدار ۲۵ گیگ',
        value: 2540,
      },
    ];
    this.sum = 2540;
  }

  submit() {}

  showBalance(value: number, sum?: boolean): string {
    return printMoney(value);
  }
}
