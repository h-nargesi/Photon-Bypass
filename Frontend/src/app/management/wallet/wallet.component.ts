import { CommonModule } from '@angular/common';
import { Component, OnInit, ViewChild } from '@angular/core';
import { FormsModule } from '@angular/forms';
import {
  MatPaginator,
  MatPaginatorIntl,
  MatPaginatorModule,
  PageEvent,
} from '@angular/material/paginator';
import { MatTableModule } from '@angular/material/table';
import {
  BadgeComponent,
  ButtonDirective,
  CardBodyComponent,
  CardComponent,
  ColComponent,
  FormControlDirective,
  InputGroupComponent,
  InputGroupTextDirective,
  RowComponent,
  SpinnerComponent,
} from '@coreui/angular';
import { IconDirective } from '@coreui/icons-angular';
import { ICON_SUBSET } from '../../@icons';
import {
  BalanceDirection,
  BalanceStatus,
  PagedResult,
  ResultStatus,
  WalletTransaction,
} from '../../@models';
import {
  MessageService,
  TranslationPipe,
  TranslationService,
  printMoney,
} from '../../@services';
import { PaginatorInt } from '../../history/paginator-int';
import { WalletService } from './wallet.service';

@Component({
  selector: 'app-wallet',
  imports: [
    CommonModule,
    FormsModule,
    RowComponent,
    ColComponent,
    CardComponent,
    CardBodyComponent,
    InputGroupComponent,
    InputGroupTextDirective,
    IconDirective,
    FormControlDirective,
    ButtonDirective,
    BadgeComponent,
    TranslationPipe,
    MatTableModule,
    MatPaginatorModule,
    SpinnerComponent,
  ],
  templateUrl: './wallet.component.html',
  styleUrl: './wallet.component.scss',
  providers: [{ provide: MatPaginatorIntl, useClass: PaginatorInt }],
})
export class WalletComponent implements OnInit {
  readonly icons = ICON_SUBSET;
  readonly page_size = 20;
  readonly directions = [BalanceDirection.Credit, BalanceDirection.Debit];

  displayedColumns: string[] = [
    'time',
    'amount',
    'direction',
    'status',
    'description',
    'actor',
    'invoice',
  ];

  result?: PagedResult<WalletTransaction>;

  target = '';
  page = 1;
  loading = false;

  // adjust form (docs/20 D5)
  direction: BalanceDirection = BalanceDirection.Credit;
  amount?: number;
  description = '';
  busy = false;

  @ViewChild(MatPaginator) paginator?: MatPaginator;

  constructor(
    private readonly service: WalletService,
    private readonly messages: MessageService,
    private readonly translation: TranslationService,
  ) {}

  get rows(): WalletTransaction[] {
    return this.result?.items ?? [];
  }

  ngOnInit(): void {
    this.loadHistory();
  }

  loadHistory(page: number = this.page, target: string = this.target): void {
    this.loading = true;

    this.service.history(target.trim() || undefined, page).subscribe({
      next: (result) => {
        this.loading = false;
        this.result = result ?? undefined;
        this.page = result?.page ?? page;
      },
      error: () => (this.loading = false),
    });
  }

  onPage(event: PageEvent): void {
    this.loadHistory(event.pageIndex + 1);
  }

  applyTarget(): void {
    this.paginator?.firstPage();
    this.loadHistory(1, this.target);
  }

  submitAdjust(): void {
    if (this.busy) return;

    const target = this.target.trim();
    const amount = this.amount ?? 0;
    const description = this.description.trim();

    if (!target || amount < 1 || description.length < 1) {
      this.messages.messageHandler(
        ResultStatus.error,
        this.translation.translate('management.wallet.adjust.title'),
        this.translation.translate('management.wallet.adjust.invalid'),
      );
      return;
    }

    this.busy = true;
    this.service.adjust(target, this.direction, amount, description).subscribe((result) => {
      this.busy = false;
      if (result.status() !== ResultStatus.success) return;

      this.description = '';
      this.amount = undefined;
      this.paginator?.firstPage();
      this.loadHistory(1, target);
    });
  }

  amountTitle(row: WalletTransaction): string {
    const sign = row.direction === BalanceDirection.Credit ? '+' : '−';
    return `${sign} ${printMoney(row.amount)}`;
  }

  directionTitle(row: WalletTransaction): string {
    return this.translation.translate(
      'management.wallet.direction.' +
        (row.direction === BalanceDirection.Credit ? 'credit' : 'debit'),
    );
  }

  statusTitle(row: WalletTransaction): string {
    return this.translation.translate(
      'management.wallet.status.' + BalanceStatus[row.status],
    );
  }
}
