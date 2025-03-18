import { Injectable } from '@angular/core';
import { Router } from '@angular/router';
import { Observable } from 'rxjs';
import { map, tap } from 'rxjs/operators';
import { ApiResult, ApiResultData, MessageMethod } from '../../@models';
import { LocalStorageService } from '../local-storage/local-storage-service';
import { ApiMessageHandlerService } from '../message-handler/message-handler-service';
import { MessageService } from '../message-handler/message-service';
import { TranslationService } from '../translation/translation-service';
import { HttpClientHandler } from './http-client';
import { ApiParam } from './models/api-param-type';
import {
  ApiResultContext,
  ApiResultDataContext,
} from './models/api-result-context';

@Injectable({ providedIn: 'root' })
export abstract class ApiBaseService extends ApiMessageHandlerService {
  private is_logout: boolean = false;

  constructor(
    protected readonly api: HttpClientHandler,
    protected readonly router?: Router,
    message_handler?: MessageService
  ) {
    super(message_handler);
  }

  protected call(
    url: string,
    params?: ApiParam,
    title?: string,
    show_message?: boolean,
    message_method?: MessageMethod
  ): Observable<ApiResult> {
    const service = this;
    return this.api
      .call(url, params)
      .pipe(
        map((result) => {
          return {
            result,
            title: title ?? TranslationService.translate('api.call'),
            show_message: show_message,
            message_method: message_method,
            service,
          } as ApiResultContext;
        })
      )
      .pipe(tap(this.checkLoginUser))
      .pipe(map(this.resultHandler));
  }

  protected get<M>(
    url: string,
    params?: ApiParam,
    title?: string,
    show_message?: boolean,
    message_method?: MessageMethod
  ): Observable<ApiResultData<M>> {
    const service = this;
    return this.api
      .get<M>(url, params)
      .pipe(
        map((result) => {
          return {
            result,
            title: title ?? TranslationService.translate('api.loading'),
            show_message: show_message,
            message_method: message_method,
            service,
          } as ApiResultDataContext<M>;
        })
      )
      .pipe(tap(this.checkLoginUser))
      .pipe(map(this.resultHandler));
  }

  protected getData<M>(
    url: string,
    params?: ApiParam,
    title?: string,
    show_message?: boolean,
    message_method?: MessageMethod
  ): Observable<M> {
    return this.get<M>(url, params, title, show_message, message_method).pipe(
      map((result) => result.data ?? (null as M))
    );
  }

  protected job(
    url: string,
    model: any | null,
    title?: string,
    show_message?: boolean,
    message_method?: MessageMethod
  ): Observable<ApiResult> {
    const service = this;
    return this.api
      .job(url, model)
      .pipe(
        map((result) => {
          return {
            result,
            title: title ?? TranslationService.translate('api.job'),
            show_message: show_message ?? true,
            message_method: message_method,
            service,
          } as ApiResultContext;
        })
      )
      .pipe(tap(this.checkLoginUser))
      .pipe(map(this.resultHandler));
  }

  protected post<M>(
    url: string,
    model: any | null,
    title?: string,
    show_message?: boolean,
    message_method?: MessageMethod
  ): Observable<ApiResultData<M>> {
    const service = this;
    return this.api
      .post<M>(url, model)
      .pipe(
        map((result) => {
          return {
            result,
            title: title ?? TranslationService.translate('api.saving'),
            show_message: show_message ?? true,
            message_method: message_method,
            service,
          } as ApiResultDataContext<M>;
        })
      )
      .pipe(tap(this.checkLoginUser))
      .pipe(map(this.resultHandler));
  }

  protected postData<M>(
    url: string,
    model: any | null,
    title?: string,
    show_message?: boolean,
    message_method?: MessageMethod
  ): Observable<M> {
    return this.post<M>(url, model, title, show_message, message_method).pipe(
      map((result) => result.data ?? (null as M))
    );
  }

  protected getApiParam(model: any): ApiParam | undefined {
    if (!model) return undefined;
    const data = {} as ApiParam;
    for (const key in model) data[key] = model[key];
    return data;
  }

  private checkLoginUser(context: ApiResultContext): void {
    const result = context.result;
    if (result.code == 401) {
      LocalStorageService.set(['user', 'bearer'], undefined);

      const service = context.service as ApiBaseService;
      if (!service) {
        throw 'Invalid service in `ApiResultContext`. (ApiBaseService)';
      }

      if (service.is_logout) return;
      service.is_logout = true;

      if (!service.router) {
        throw 'The router service is not set. (ApiBaseService)';
      }
      service.router.navigate(['/auth/login']);
    } else {
      LocalStorageService.set(['user', 'last_request'], new Date().getTime());
    }
  }
}
