import { HttpHeaders, HttpParams, HttpResponse } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { from, Observable, of } from 'rxjs';
import {
  ApiResult,
  ApiResultData,
  FullUserModel,
  HistoryRecord,
  PlanType,
  TrafficData,
  TrafficDataModel,
  UserModel,
  UserPlanInfo,
} from '../../@models';
import { LocalStorageService } from '../local-storage/local-storage-service';

@Injectable({ providedIn: 'root' })
export class FakeDataMaker {
  public static readonly UseAPI: boolean = false;

  public get<M>(
    url: string,
    options: {
      headers?: HttpHeaders | Record<string, string | string[]>;
      observe: 'response';
      params?:
        | HttpParams
        | Record<
            string,
            string | number | boolean | ReadonlyArray<string | number | boolean>
          >;
    }
  ): Observable<HttpResponse<ApiResultData<M>>> {
    return this.makeResult<ApiResult>(url, options?.params);
  }

  public post<M>(
    url: string,
    body: any | null,
    options: {
      headers?: HttpHeaders | Record<string, string | string[]>;
      observe: 'response';
      params?:
        | HttpParams
        | Record<
            string,
            string | number | boolean | ReadonlyArray<string | number | boolean>
          >;
    }
  ): Observable<HttpResponse<ApiResultData<M>>> {
    return this.makeResult<ApiResult>(url, body);
  }

  private makeResult<M>(
    url: string,
    data: any | null
  ): Observable<HttpResponse<M>> {
    switch (url) {
      // AUTH
      case 'api/auth/token':
        return this.api_auth_token(data) as Observable<HttpResponse<M>>;
      case 'api/auth/logout':
        return this.api_auth_logout() as Observable<HttpResponse<M>>;
      case 'api/auth/register':
        return this.api_auth_register() as Observable<HttpResponse<M>>;
      case 'api/auth/check':
        return this.api_auth_check() as Observable<HttpResponse<M>>;
      case 'api/auth/change':
        return this.api_auth_change() as Observable<HttpResponse<M>>;
      // ACCOUNT
      case 'api/account/get-user':
        return this.api_account_get_user() as Observable<HttpResponse<M>>;
      case 'api/account/change-ovpn':
        return this.api_account_change_ovpn() as Observable<HttpResponse<M>>;
      case 'api/account/send-cert-email':
        return this.api_account_get_cert_email() as Observable<HttpResponse<M>>;
      case 'api/account/traffic-data':
        return this.api_account_traffic_data() as Observable<HttpResponse<M>>;
      case 'api/account/full-info':
        return this.api_account_full_info() as Observable<HttpResponse<M>>;
      case 'api/account/history':
        return this.api_account_history(data) as Observable<HttpResponse<M>>;
      case 'api/account/edit-user':
        return this.api_account_edit() as Observable<HttpResponse<M>>;
      // CONNECTION
      case 'api/connection/current-con-state':
        return this.api_con_current_state() as Observable<HttpResponse<M>>;
      case 'api/connection/close-con':
        return this.api_con_close_con() as Observable<HttpResponse<M>>;
      // PLAN
      case 'api/plan/plan-info':
        return this.api_plan_plan_info() as Observable<HttpResponse<M>>;
    }
    return of();
  }

  private api_auth_token(data: any): Observable<HttpResponse<ApiResult>> {
    LocalStorageService.set(['user', 'bearer'], 'token');
    if (data.password !== 'password') {
      return wait({
        code: 401,
        message: 'نام کاربری یا کلمه عبور اشتباه است.',
      } as ApiResult);
    } else {
      return wait({ code: 200, message: 'شما وارد شدید.' } as ApiResult);
    }
  }

  private api_auth_logout(): Observable<HttpResponse<ApiResult>> {
    LocalStorageService.set(['user', 'bearer'], undefined);
    return wait({ code: 200 } as ApiResult);
  }

  private api_auth_register(): Observable<HttpResponse<ApiResult>> {
    return wait({
      code: 200,
      message: 'کاربر با موفقیت ساخته شد.',
    } as ApiResult);
  }

  private api_auth_check(): Observable<HttpResponse<ApiResult>> {
    const bearer = LocalStorageService.get(['user', 'bearer']);
    if (bearer) {
      return wait({ code: 200 } as ApiResult, 1, 1);
    }

    return wait({ code: 401 } as ApiResult, 1, 1);
  }

  private api_auth_change(): Observable<HttpResponse<ApiResult>> {
    return wait({ code: 200, message: 'پسورد شما تغییر کرد.' } as ApiResult);
  }

  private api_account_change_ovpn(): Observable<HttpResponse<ApiResult>> {
    return wait({
      code: 200,
      message: 'پسورد وی‌پی‌ان شما تغییر کرد.',
    } as ApiResult);
  }

  private api_account_get_user(): Observable<
    HttpResponse<ApiResultData<UserModel>>
  > {
    const bearer = LocalStorageService.get(['user', 'bearer']);
    if (bearer) {
      return wait({
        code: 200,
        data: {
          username: 'hamed@na',
          fullname: 'حامد نرگسی',
          email: 'hamed.nargesi.jar@gmail.com',
        },
      } as ApiResultData<UserModel>);
    }

    return wait({ code: 401 } as ApiResultData<UserModel>);
  }

  private api_account_get_cert_email(): Observable<HttpResponse<ApiResult>> {
    return wait(
      { code: 200, message: 'ایمیل با موفقیت ارسال شد.' } as ApiResult,
      4000
    );
  }

  private api_account_traffic_data(): Observable<
    HttpResponse<ApiResultData<TrafficDataModel>>
  > {
    const data: TrafficDataModel = {
      title: 'ترافیک یک ماه گذشته',
      collections: [],
      labels: [
        'January',
        'February',
        'March',
        'April',
        'May',
        'June',
        'July',
        'August',
        'September',
        'October',
        'November',
        'December',
      ],
    };

    const upload: TrafficData = {
      title: 'Upload',
      data: [],
    };
    for (let i = 0; i < data.labels.length; i++) {
      upload.data.push(random(50, 240));
    }

    const download: TrafficData = {
      title: 'Download',
      data: [],
    };
    for (let i = 0; i < data.labels.length; i++) {
      download.data.push(random(20, 160));
    }

    const total: TrafficData = {
      title: 'Total',
      data: [],
    };
    let sum: number = 0;
    for (let i = 0; i < data.labels.length; i++) {
      sum += upload.data[i] + download.data[i];
      total.data.push(upload.data[i] + download.data[i]);
    }

    const average: TrafficData = {
      title: 'Average',
      data: [],
    };
    sum = sum / data.labels.length;
    for (let i = 0; i < data.labels.length; i++) {
      average.data.push(sum);
    }

    data.collections.push(upload);
    data.collections.push(download);
    data.collections.push(total);
    data.collections.push(average);

    return wait({ code: 200, data } as ApiResultData<TrafficDataModel>, 2000);
  }

  private api_account_full_info(): Observable<
    HttpResponse<ApiResultData<FullUserModel>>
  > {
    return wait(
      {
        code: 200,
        data: {
          username: 'hamed@aw',
          email: 'hamed.nargesi@gmail.com',
          emailValid: false,
          mobile: '+989125157305',
          mobileValid: true,
          firstname: 'حامد',
          lastname: 'نرگسی',
        },
      } as ApiResultData<FullUserModel>,
      200,
      300
    );
  }

  private api_account_edit(): Observable<HttpResponse<ApiResult>> {
    return wait({ code: 200, message: 'کاربر ویرایش شد.' } as ApiResult);
  }

  private api_account_history(
    data: any
  ): Observable<HttpResponse<ApiResultData<HistoryRecord[]>>> {
    if (data.to) data.to = (Math.floor(data.to / 86400000) + 1) * 86400000;
    if (data.from) data.from = Math.floor(data.from / 86400000) * 86400000;

    const result = Array.from({ length: 100 }, (_, k) =>
      createNewRecord(k)
    ).filter(
      (x) =>
        (!data.from || data.from <= x.eventTime) &&
        (!data.to || data.to > x.eventTime)
    );

    return wait({ code: 200, data: result } as ApiResultData<HistoryRecord[]>);
  }

  private api_con_current_state(): Observable<
    HttpResponse<ApiResultData<number[]>>
  > {
    return wait({ code: 200, data: [534, 325, 244, 16] } as ApiResultData<
      number[]
    >);
  }

  private api_con_close_con(): Observable<HttpResponse<ApiResult>> {
    return wait({ code: 200, message: 'کانکشن بسته شد.' } as ApiResult);
  }

  private api_plan_plan_info(): Observable<
    HttpResponse<ApiResultData<UserPlanInfo>>
  > {
    return wait({
      code: 200,
      data: {
        type: PlanType.Monthly,
        remainsTitle: '23 روز باقی مانده',
        remainsPercent: 23,
      },
    } as ApiResultData<UserPlanInfo>);
  }
}

function random(min: number, max: number) {
  return Math.floor(Math.random() * (max - min + 1) + min);
}

const wait = <M>(
  result: M,
  min: number = 200,
  length: number = 2000
): Observable<HttpResponse<M>> => {
  return from(
    new Promise<HttpResponse<M>>((resolve) => {
      setTimeout(() => {
        return resolve({
          ok: true,
          status: 200,
          body: result,
        } as HttpResponse<M>);
      }, Math.floor(Math.random() * (length + 1) + min));
    })
  );
};

function createNewRecord(id: number): HistoryRecord {
  const timeEvent =
    new Date().getTime() - id * 86400000 - Math.random() * 86400000;

  const index = 1 + Math.round(Math.random() * (TITLE.length - 2));
  const title = TITLE[index];

  const unit =
    UNITS[index === 2 || index === 3 ? 0 : index == 4 ? 1 : index == 5 ? 2 : 3];
  let value = undefined;
  if (unit) value = Math.floor(Math.random() * 5000);

  return {
    id: id,
    eventTime: timeEvent,
    eventTimeTitle: new Date(timeEvent).toDateString(),
    title: title,
    color: COLORS[Math.round(Math.random() * (COLORS.length - 1))],
    value: value,
    unit: unit,
    description: undefined,
  };
}

const COLORS: string[] = ['success', 'info', 'warning', 'danger'];

const UNITS: (string | undefined)[] = ['تومان', 'گیگابایت', 'روز', undefined];

const TITLE: string[] = [
  'ایجاد حساب کاربری',
  'تغییر پلن',
  'افزایش حساب',
  'کاهش حساب',
  'تمدید پلن',
  'تمدید پلن',
  'تغییر اطلاعات حساب',
  'بستن دستی کانکشن',
  'تغییر کلمه عبور vpn',
  'تغییر کلمه عبور حساب',
  'اعتبار سنجی ایمیل',
  'اعتبار سنجی واتساپ',
  'پیام',
];
