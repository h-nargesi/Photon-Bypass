import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { provideRouter } from '@angular/router';
import {
  BalanceDirection,
  BalanceStatus,
  PagedResult,
  WalletTransaction,
} from '../../@models';
import { API_BASE_URL } from '../../@services';
import { WalletComponent } from './wallet.component';

describe('WalletComponent (docs/20 phase 6)', () => {
  let http: HttpTestingController;
  let fixture: ComponentFixture<WalletComponent>;
  let component: WalletComponent;

  const history: PagedResult<WalletTransaction> = {
    page: 1,
    pageSize: 20,
    totalCount: 1,
    totalPages: 1,
    items: [
      {
        id: 11,
        amount: 5,
        direction: BalanceDirection.Credit,
        status: BalanceStatus.Completed,
        description: 'اصلاح دستی',
        actor: 'boss',
        eventTime: 1700000000000,
        eventTimeTitle: '۱۴۰۲/۰۸/۰۱',
      },
    ],
  };

  const flushHistory = (target?: string, page: string = '1') => {
    const request = http.expectOne(
      req => req.method === 'GET' && req.url === '/api/wallet/history',
    );
    expect(request.request.params.get('target')).toBe(target ?? null);
    expect(request.request.params.get('page')).toBe(page);
    request.flush(
      { code: 200, data: history },
      { status: 200, statusText: 'OK' },
    );
  };

  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideNoopAnimations(),
        provideRouter([]),
        { provide: API_BASE_URL, useValue: '/api' },
      ],
    });
    http = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(WalletComponent);
    component = fixture.componentInstance;
  });

  afterEach(() => {
    http.verify();
    localStorage.clear();
  });

  it('loads the own wallet history on start', () => {
    fixture.detectChanges();
    flushHistory();

    expect(component.rows.length).toBe(1);
    expect(component.statusTitle(history.items[0])).toBe('انجام‌شده');
    expect(component.directionTitle(history.items[0])).toBe('واریز');
  });

  it('a target switch asks the server for that account', () => {
    fixture.detectChanges();
    flushHistory();

    component.target = 'someone';
    component.applyTarget();

    flushHistory('someone');
  });

  it('adjust refuses incomplete input before any request', () => {
    fixture.detectChanges();
    flushHistory();

    component.target = 'someone';
    component.amount = 0;
    component.description = '';
    component.submitAdjust();

    http.expectNone(
      req => req.method === 'POST' && req.url === '/api/wallet/adjust',
    );
  });

  it('adjust posts the transparent transaction (D5) and refreshes the history', () => {
    fixture.detectChanges();
    flushHistory();

    component.target = 'someone';
    component.direction = BalanceDirection.Debit;
    component.amount = 3;
    component.description = '  تصحیح اشتباه  ';
    component.submitAdjust();

    const request = http.expectOne(
      req => req.method === 'POST' && req.url === '/api/wallet/adjust',
    );
    expect(request.request.body).toEqual({
      target: 'someone',
      direction: BalanceDirection.Debit,
      amount: 3,
      description: 'تصحیح اشتباه',
    });
    request.flush(
      { code: 200, message: 'ثبت شد' },
      { status: 200, statusText: 'OK' },
    );

    flushHistory('someone');
    expect(component.description).toBe('');
  });
});
