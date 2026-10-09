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
  FormControlDirective,
  RowComponent,
} from '@coreui/angular';
import { PaymentInvoice } from '../@models';
import { TranslationPipe, TranslationService, UserService } from '../@services';
import { printMoney } from '../@services';
import { PaymentService } from './payment-service';

enum InvoiceStatus {
  Pending = 0,
  Completed = 1,
  Failed = 2,
  Canceled = 3,
  Verifying = 4,
}

enum ReceiptMode {
  File = 'file',
  Text = 'text',
}

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
    FormControlDirective,
    TranslationPipe,
  ],
  templateUrl: './payment.component.html',
  styleUrl: './payment.component.scss',
  providers: [PaymentService],
})
export class PaymentComponent implements OnInit {
  readonly status = InvoiceStatus;
  readonly receipt_modes = ReceiptMode;

  invoice?: PaymentInvoice;
  target?: string;
  busy = false;
  receipt_mode: ReceiptMode = ReceiptMode.File;
  receipt_file?: File;
  receipt_text = '';

  constructor(
    private readonly route: ActivatedRoute,
    private readonly router: Router,
    private readonly service: PaymentService,
    private readonly user_service: UserService,
    private readonly translation: TranslationService
  ) {}

  ngOnInit() {
    const code = this.route.snapshot.queryParamMap.get('invoice');
    const n = +code!;

    if (!Number.isFinite(n) || n <= 0) {
      this.router.navigate(['dashboard']);
      return;
    }

    this.target = this.route.snapshot.queryParamMap.get('target') ?? undefined;

    this.loadInvoice(n);
  }

  get can_settle_wallet(): boolean {
    return !!this.invoice?.allowWallet;
  }

  get can_register_receipt(): boolean {
    return (
      !!this.invoice &&
      this.invoice.status === InvoiceStatus.Pending &&
      this.invoice.payable > 0
    );
  }

  loadInvoice(code: number) {
    this.service
      .getInvoice(code, this.target)
      .subscribe((result) => (this.invoice = result ?? undefined));
  }

  settleWallet() {
    if (!this.invoice || this.busy) return;

    this.busy = true;

    this.service.settleWallet(this.invoice.code, this.target).subscribe({
      next: () => {
        this.user_service.reload();
        this.router.navigate(['dashboard']);
      },
      error: () => (this.busy = false),
      complete: () => (this.busy = false),
    });
  }

  registerReceipt() {
    if (!this.invoice || this.busy) return;

    const file =
      this.receipt_mode === ReceiptMode.File ? this.receipt_file : undefined;
    const text =
      this.receipt_mode === ReceiptMode.Text
        ? this.receipt_text?.trim() || undefined
        : undefined;

    if (!file && !text) return;

    this.busy = true;

    this.service
      .registerReceipt(this.invoice.code, file, text, this.target)
      .subscribe({
        next: () => {
          this.receipt_file = undefined;
          this.receipt_text = '';
          this.loadInvoice(this.invoice!.code);
        },
        error: () => (this.busy = false),
        complete: () => (this.busy = false),
      });
  }

  onFileChange(event: Event) {
    const input = event.target as HTMLInputElement;
    this.receipt_file = input.files?.[0];
  }

  statusTitle(): string {
    var key: string;

    switch (this.invoice?.status) {
      case InvoiceStatus.Completed:
        key = 'payment.statuses.completed';
        break;
      case InvoiceStatus.Failed:
        key = 'payment.statuses.failed';
        break;
      case InvoiceStatus.Canceled:
        key = 'payment.statuses.canceled';
        break;
      case InvoiceStatus.Verifying:
        key = 'payment.statuses.verifying';
        break;
      default:
        key = 'payment.statuses.pending';
        break;
    }

    return this.translation.translate(key);
  }

  showBalance(value?: number): string {
    return printMoney(value);
  }
}
