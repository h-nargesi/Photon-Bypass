import { Injectable, TemplateRef } from '@angular/core';
import { ApiResult, ResultStatus } from '../../@models';
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
    if (status >= ResultStatus.warning || context.show_message === true) {
      if (context.message_method && status >= ResultStatus.warning) {
        result.method = context.message_method;
      }

      if (context.show_message !== false && context.service.message_handler) {
        context.service.message_handler.resultHandler(context.title, result);
      } else {
        if (context.show_message !== false) {
          console.error('The message-handler is not set.');
        }

        if (status >= ResultStatus.error) {
          console.error(result.message);
        } else {
          console.info(result.message);
        }
      }
    }

    return result;
  }
}
