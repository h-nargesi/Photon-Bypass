import {
  ApiResult,
  ApiResultData,
  MessageMethod,
  ShowMessageCase,
} from '../../../@models';
import { ApiMessageHandlerService } from '../../message-handler/message-handler-service';

export interface ApiOptions {
  title?: string;
  show_message?: ShowMessageCase;
  message_method?: MessageMethod;
}

interface ApiResultContextBase extends ApiOptions {
  title: string;
  show_message: ShowMessageCase;
  message_method?: MessageMethod;
  service: ApiMessageHandlerService;
}

export interface ApiResultContext extends ApiResultContextBase {
  result: ApiResult;
}

export interface ApiResultDataContext<Model> extends ApiResultContextBase {
  result: ApiResultData<Model>;
}
