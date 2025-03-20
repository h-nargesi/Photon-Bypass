import { HttpClient, HttpResponse } from '@angular/common/http';
import { Inject, Injectable } from '@angular/core';
import { Observable, of } from 'rxjs';
import { catchError, map, tap } from 'rxjs/operators';
import {
  ApiResult,
  ApiResultData,
  ApiResultStatus,
  MessageMethod,
  ResultStatus,
} from '../../@models';
import { LocalStorageService } from '../local-storage/local-storage-service';
import { TranslationService } from '../translation/translation-service';
import { FakeDataMaker } from './fake-data';
import { ApiParam } from './models/api-param-type';
import { API_BASE_URL } from './models/app-base-path';

@Injectable({ providedIn: 'root' })
export class HttpClientHandler {
  constructor(
    @Inject(API_BASE_URL)
    private readonly base_path: string,
    private readonly api: HttpClient,
    private readonly fake: FakeDataMaker
  ) {}

  public call(url: string, params?: ApiParam): Observable<ApiResult> {
    url = this.base_path + url;
    const headers = this.getHeader();
    return this.fake
      .get<ApiResult>(url, { params, observe: 'response', headers })
      .pipe(map(this.processResponse))
      .pipe(catchError(this.errorHandler));
  }

  public get<M>(url: string, params?: ApiParam): Observable<ApiResultData<M>> {
    url = this.base_path + url;
    const headers = this.getHeader();
    return this.fake
      .get<ApiResultData<M>>(url, { params, observe: 'response', headers })
      .pipe(map(this.processResponse))
      .pipe(catchError(this.errorHandler));
  }

  public job(url: string, body: any | null): Observable<ApiResult> {
    url = this.base_path + url;
    const headers = this.getHeader();
    return this.fake
      .post<ApiResult>(url, body, { observe: 'response', headers })
      .pipe(map(this.processResponse))
      .pipe(catchError(this.errorHandler));
  }

  public post<M>(url: string, body: any | null): Observable<ApiResultData<M>> {
    url = this.base_path + url;
    const headers = this.getHeader();
    return this.fake
      .post<ApiResultData<M>>(url, body, { observe: 'response', headers })
      .pipe(map(this.processResponse))
      .pipe(catchError(this.errorHandler));
  }

  public authorization(url: string, body: any | null): Observable<ApiResult> {
    url = this.base_path + url;
    return this.fake
      .post<string>(url, body, { observe: 'response' })
      .pipe(
        tap((response) => {
          if (response.body && response.body.data && response.body.code < 300) {
            const token = response.body.data as string;
            if (token) {
              LocalStorageService.set(['user', 'bearer'], token);
            }
          }

          return response;
        })
      )
      .pipe(map(this.processResponse))
      .pipe(catchError(this.errorHandler));
  }

  private getHeader(): Record<string, string | string[]> | undefined {
    const token = LocalStorageService.get(['user', 'bearer']);
    if (token)
      return {
        authorization: 'Bearer ' + token,
      };

    return undefined;
  }

  protected errorHandler(error: any): Observable<ApiResult> {
    const error_object: ApiResult = HttpClientHandler.setStatusFunction({
      code: error?.error?.code ?? error.status ?? 600,
      message:
        error?.error?.message ?? TranslationService.translate('api.error'),
      developer: error,
      method: MessageMethod.toaster,
    } as ApiResult);

    if (error_object.code < 400) error_object.code = 600;
    return of(error_object);
  }

  private processResponse(response: HttpResponse<ApiResult>): ApiResult {
    const result = HttpClientHandler.setStatusFunction(
      response.body ?? ({} as ApiResult)
    );

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

  private static setStatusFunction(result: ApiResult): ApiResult {
    return Object.assign({}, result, {
      status: function (): ResultStatus {
        if (this.code >= 600) return ResultStatus.uiFatal;
        else if (this.code >= 500) return ResultStatus.serverFatal;
        else if (this.code >= 400) return ResultStatus.error;
        else if (this.code >= 300) return ResultStatus.warning;
        else if (this.code >= 200) return ResultStatus.success;
        else return ResultStatus.info;
      },
    } as ApiResultStatus);
  }
}
