import { CommonModule } from '@angular/common';
import { Component } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import {
  ButtonCloseDirective,
  ButtonDirective,
  FormControlDirective,
  ModalBodyComponent,
  ModalComponent,
  ModalHeaderComponent,
  ModalToggleDirective,
} from '@coreui/angular';
import { ResultStatus } from '../../@models';
import {
  MessageService,
  TranslationPipe,
  TranslationService,
  UserService,
} from '../../@services';
import { PaymentModalService } from './payment-modal.service';

@Component({
  selector: 'app-payment-modal',
  imports: [
    CommonModule,
    FormsModule,
    FormControlDirective,
    ModalComponent,
    ModalHeaderComponent,
    ModalBodyComponent,
    ModalToggleDirective,
    ButtonCloseDirective,
    ButtonDirective,
    TranslationPipe,
  ],
  templateUrl: './payment-modal.component.html',
  styleUrl: './payment-modal.component.scss',
  providers: [PaymentModalService],
})
export class PaymentModalComponent {
  readonly id: string = 'paymentModal';

  visible: boolean = false;
  value: number = 100000;

  constructor(
    private readonly router: Router,
    private readonly service: PaymentModalService,
    private readonly message: MessageService,
    private readonly translation: TranslationService,
    private readonly user: UserService
  ) {}

  submit() {
    if (!(this.value > 10000)) {
      this.message.messageHandler(
        ResultStatus.error,
        this.translation.translate('payment.request.title'),
        this.translation.translate('payment.request.invalid-value')
      );
      return;
    }

    this.service.paymentRequest(this.value).subscribe((result) => {
      if (!result) {
        this.message.messageHandler(
          ResultStatus.error,
          this.translation.translate('payment.request.title'),
          this.translation.translate('global.messages.error')
        );
      } else {
        this.user.invoice = result;
        this.router.navigate(['payment'], { queryParams: { invoice: result } });
      }
    });
  }
}
