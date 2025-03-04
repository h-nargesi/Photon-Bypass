import { Injectable } from '@angular/core';
import { Router } from '@angular/router';
import { Observable } from 'rxjs';
import { map } from 'rxjs/operators';
import { ApiResult, MessageMethod } from '../../@models';
import { LocalStorageService } from '../local-storage/local-storage-service';
import { ApiMessageHandlerService } from '../message-handler/message-handler-service';
import { MessageService } from '../message-handler/message-service';
import { TranslationService } from '../translation/translation-service';
import { ApiParam } from './api-param-type';
import { ApiResultContext } from './api-result-context';
import { HttpClientHandler } from './http-client';

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

  protected get<M>(
    url: string,
    title?: string,
    params?: ApiParam,
    show_message?: boolean,
    message_method?: MessageMethod
  ): Observable<M | undefined> {
    return this.getResult<M>(
      url,
      title,
      params,
      show_message,
      message_method
    ).pipe(map((result) => result.data));
  }

  protected getResult<M>(
    url: string,
    title?: string,
    params?: ApiParam,
    show_message?: boolean,
    message_method?: MessageMethod
  ): Observable<ApiResult<M>> {
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
          } as ApiResultContext<M>;
        })
      )
      .pipe(map(this.checkLoginUser<M>))
      .pipe(map(this.resultHandler<M>));
  }

  protected post<M>(
    url: string,
    title: string,
    model: any | null,
    show_message?: boolean,
    message_method?: MessageMethod
  ): Observable<M | undefined> {
    return this.postResult<M>(
      url,
      title,
      model,
      show_message,
      message_method
    ).pipe(map((result) => result.data));
  }

  protected postResult<M>(
    url: string,
    title: string,
    model: any | null,
    show_message?: boolean,
    message_method?: MessageMethod
  ): Observable<ApiResult<M>> {
    const service = this;
    return this.api
      .post<M>(url, model)
      .pipe(
        map((result) => {
          return {
            result,
            title: title,
            show_message: show_message ?? true,
            message_method: message_method,
            service,
          } as ApiResultContext<M>;
        })
      )
      .pipe(map(this.checkLoginUser<M>))
      .pipe(map(this.resultHandler<M>));
  }

  private checkLoginUser<M>(context: ApiResultContext<M>): ApiResultContext<M> {
    const result = context.result;
    if (result.code == 401) {
      LocalStorageService.set(['user', 'bearer'], undefined);

      const service = context.service as ApiBaseService;
      if (!service) {
        throw 'Invalid service in `ApiResultContext`. (ApiBaseService)';
      }

      if (service.is_logout) return context;
      service.is_logout = true;

      if (!service.router) {
        throw 'The router service is not set. (ApiBaseService)';
      }
      service.router.navigate(['/auth/login']);
    } else {
      LocalStorageService.set(['user', 'last_request'], new Date().getTime());
    }

    return context;
  }
}
