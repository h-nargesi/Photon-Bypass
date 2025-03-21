import { Injectable, TemplateRef } from '@angular/core';
import { ApiResult, ResultStatus, ShowMessageCase } from '../../@models';
import { ApiResultContext } from '../api-services/models/api-result-context';
import { MessageService } from '../message-handler/message-service';

@Injectable({ providedIn: 'root' })
export class ApiMessageHandlerService {
  constructor(protected readonly message_handler?: MessageService) {}

  public get dialogBox(): TemplateRef<any> | undefined {
    return this.message_handler?.dialogBox;
  }

  public set dialogBox(dialog: TemplateRef<any>) {
    if (this.message_handler) this.message_handler.dialogBox = dialog;
  }

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
          console.error(result.message);
        } else if (status >= ResultStatus.warning) {
          console.warn(result.message);
        } else {
          console.info(result.message);
        }
      }
    }

    return result;
  }
}
