import { ApiResult, ApiResultData, MessageMethod } from '../../../@models';
import { ApiMessageHandlerService } from '../../message-handler/message-handler-service';

interface ApiResultContextBase {
  title: string;
  show_message?: boolean;
  message_method?: MessageMethod;
  service: ApiMessageHandlerService;
}

export interface ApiResultContext extends ApiResultContextBase {
  result: ApiResult;
}

export interface ApiResultDataContext<Model> extends ApiResultContextBase {
  result: ApiResultData<Model>;
}
