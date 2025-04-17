import { CommonModule } from '@angular/common';
import { Component, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
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
import { PaymentInvoice } from '../@models';
import { printMoney, TranslationPipe } from '../@services';
import { PaymentService } from './payment-service';

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
  providers: [PaymentService],
})
export class PaymentComponent implements OnInit {
  invoice?: PaymentInvoice;

  constructor(
    private readonly route: ActivatedRoute,
    private readonly router: Router,
    private readonly service: PaymentService
  ) {}

  ngOnInit() {
    const code = this.route.snapshot.queryParamMap.get('invoice');

    if (!code) {
      this.router.navigate(['dashboard']);
      return;
    }

    this.service
      .getInvlice(code ?? '')
      .subscribe((result) => (this.invoice = result));
  }

  submit() {
    if (!this.invoice) return;
    this.service
      .pay(this.invoice.code)
      .subscribe((url) => (window.location.href = url));
  }

  showBalance(value?: number): string {
    return printMoney(value);
  }
}
