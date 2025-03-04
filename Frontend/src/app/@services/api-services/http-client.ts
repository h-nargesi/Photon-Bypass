import { HttpClient, HttpResponse } from '@angular/common/http';
import { Inject, Injectable } from '@angular/core';
import { Observable, of } from 'rxjs';
import { catchError, map } from 'rxjs/operators';
import { ApiResult, MessageMethod } from '../../@models';
import { LocalStorageService } from '../local-storage/local-storage-service';
import { TranslationService } from '../translation/translation-service';
import { ApiParam } from './api-param-type';
import { APP_BASE_PATH } from './app-base-path';

@Injectable({ providedIn: 'root' })
export class HttpClientHandler {
  constructor(
    @Inject(APP_BASE_PATH)
    private readonly base_path: string,
    private readonly api: HttpClient
  ) {}

  public get<M>(url: string, params?: ApiParam): Observable<ApiResult<M>> {
    url = this.base_path + url;
    const headers = this.getHeader();
    return this.api
      .get<ApiResult<M>>(url, { params, observe: 'response', headers })
      .pipe(map(this.processResponse<M>))
      .pipe(catchError(this.errorHandler<M>));
  }

  public post<M>(url: string, body: any | null): Observable<ApiResult<M>> {
    url = this.base_path + url;
    const headers = this.getHeader();
    return this.api
      .post<ApiResult<M>>(url, body, { observe: 'response', headers })
      .pipe(map(this.processResponse<M>))
      .pipe(catchError(this.errorHandler<M>));
  }

  public authorization<M>(
    url: string,
    body: any | null
  ): Observable<ApiResult<M>> {
    url = this.base_path + url;
    return this.api
      .post<ApiResult<M>>(url, body, { observe: 'response' })
      .pipe(
        map((response) => {
          if (response.body && response.body.data && response.body.code < 300) {
            const token = (response.body.data as any).access_token;
            if (token) {
              LocalStorageService.set(['user', 'bearer'], token);
            }
          }

          return response;
        })
      )
      .pipe(map(this.processResponse<M>))
      .pipe(catchError(this.errorHandler<M>));
  }

  private getHeader(): Record<string, string | string[]> | undefined {
    const token = LocalStorageService.get(['user', 'bearer']);
    if (token)
      return {
        authorization: 'Bearer ' + token,
      };

    return undefined;
  }

  protected errorHandler<M>(error: any): Observable<ApiResult<M>> {
    const error_object: ApiResult<M> = {
      code: error?.error?.code ?? error.status ?? 600,
      message:
        error?.error?.message ?? TranslationService.translate('api.error'),
      developer: error,
      method: MessageMethod.toaster,
      data: undefined,
    };
    if (error_object.code < 400) error_object.code = 600;
    return of(error_object);
  }

  private processResponse<M>(
    response: HttpResponse<ApiResult<M>>
  ): ApiResult<M> {
    const result = response.body ?? ({} as ApiResult<M>);

    if (!result.code) {
      result.code = response.status;
    }

    if (!response.ok) {
      if (!result.message) {
        result.message = TranslationService.translate('api.error');
      }

      if (result.code < 300) {
        result.code = 600 + (result.code % 100);
      }
    }

    if (!result.developer) {
      result.developer = response.statusText;
    }

    return result;
  }
}
