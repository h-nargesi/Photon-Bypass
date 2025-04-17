import { Injectable } from '@angular/core';
import { ApiResult, ResultStatus, ShowMessageCase } from '../../@models';
import { ApiResultContext } from '../api-services/models/api-result-context';
import { MessageService } from '../message-handler/message-service';

@Injectable({ providedIn: 'root' })
export class ApiMessageHandlerService {
  constructor(protected readonly message_handler?: MessageService) {}

  public resultHandler(context: ApiResultContext): ApiResult {
    if (!context || !context.result) return context.result;

    const result = context.result;
    const status = result.status();
    if (
      status >= ResultStatus.warning ||
      context.show_message === ShowMessageCase.success
    ) {
      if (context.message_method && status >= ResultStatus.warning) {
        result.method = context.message_method;
      }

      if (
        context.show_message !== ShowMessageCase.silence &&
        context.service.message_handler
      ) {
        context.service.message_handler.resultHandler(context.title, result);
      } else {
        if (context.show_message !== ShowMessageCase.silence) {
          console.error('The message-handler is not set.');
        }

        if (status >= ResultStatus.error) {
          console.error(result.message ?? 'unknown error');
        } else if (status >= ResultStatus.warning) {
          console.warn(result.message ?? 'unknown warning');
        } else if (result.message) {
          console.info(result.message);
        }
      }
    }

    return result;
  }
}
