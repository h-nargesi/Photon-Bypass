import { Injectable } from '@angular/core';
import { Router } from '@angular/router';
import { Observable } from 'rxjs';
import { map, tap } from 'rxjs/operators';
import { ApiResult, ApiResultData, ShowMessageCase } from '../../@models';
import { LocalStorageService } from '../local-storage/local-storage-service';
import { ApiMessageHandlerService } from '../message-handler/message-handler-service';
import { MessageService } from '../message-handler/message-service';
import { TranslationService } from '../translation/translation-service';
import { HttpClientHandler } from './http-client';
import { ApiParam } from './models/api-param-type';
import {
  ApiOptions,
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
    options?: ApiOptions
  ): Observable<ApiResult> {
    const service = this;
    return this.api
      .call(url, params)
      .pipe(
        map((result) => {
          const context: ApiResultContext = (options ?? {}) as ApiResultContext;
          context.title ??= TranslationService.translate('api.call');
          context.show_message ??= ShowMessageCase.errors;
          context.result = result;
          context.service = service;
          return context;
        })
      )
      .pipe(tap(this.checkLoginUser))
      .pipe(map(this.resultHandler));
  }

  protected get<M>(
    url: string,
    params?: ApiParam,
    options?: ApiOptions
  ): Observable<ApiResultData<M>> {
    const service = this;
    return this.api
      .get<M>(url, params)
      .pipe(
        map((result) => {
          const context: ApiResultDataContext<M> = (options ??
            {}) as ApiResultDataContext<M>;
          context.title ??= TranslationService.translate('api.loading');
          context.show_message ??= ShowMessageCase.errors;
          context.result = result;
          context.service = service;
          return context;
        })
      )
      .pipe(tap(this.checkLoginUser))
      .pipe(map(this.resultHandler));
  }

  protected getData<M>(
    url: string,
    params?: ApiParam,
    options?: ApiOptions
  ): Observable<M> {
    return this.get<M>(url, params, options).pipe(
      map((result) => result.data ?? (null as M))
    );
  }

  protected job(
    url: string,
    model: any | null,
    options?: ApiOptions
  ): Observable<ApiResult> {
    const service = this;
    return this.api
      .job(url, model)
      .pipe(
        map((result) => {
          const context: ApiResultContext = (options ?? {}) as ApiResultContext;
          context.title ??= TranslationService.translate('api.job');
          context.show_message ??= ShowMessageCase.success;
          context.result = result;
          context.service = service;
          return context;
        })
      )
      .pipe(tap(this.checkLoginUser))
      .pipe(map(this.resultHandler));
  }

  protected post<M>(
    url: string,
    model: any | null,
    options?: ApiOptions
  ): Observable<ApiResultData<M>> {
    const service = this;
    return this.api
      .post<M>(url, model)
      .pipe(
        map((result) => {
          const context: ApiResultDataContext<M> = (options ??
            {}) as ApiResultDataContext<M>;
          context.title ??= TranslationService.translate('api.saving');
          context.show_message ??= ShowMessageCase.success;
          context.result = result;
          context.service = service;
          return context;
        })
      )
      .pipe(tap(this.checkLoginUser))
      .pipe(map(this.resultHandler));
  }

  protected postData<M>(
    url: string,
    model: any | null,
    options?: ApiOptions
  ): Observable<M> {
    return this.post<M>(url, model, options).pipe(
      map((result) => result.data ?? (null as M))
    );
  }

  protected authorization(
    url: string,
    params?: ApiParam,
    options?: ApiOptions
  ): Observable<ApiResult> {
    const service = this;
    return this.api
      .authorization(url, params)
      .pipe(
        map((result) => {
          const context: ApiResultContext = (options ?? {}) as ApiResultContext;
          context.show_message ??= ShowMessageCase.success;
          context.result = result;
          context.service = service;
          return context;
        })
      )
      .pipe(map(this.resultHandler));
  }

  private checkLoginUser(context: ApiResultContext): void {
    const result = context.result;
    if (result.code === 401) {
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
