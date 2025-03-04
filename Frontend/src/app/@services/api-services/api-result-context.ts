import { ApiResult, MessageMethod } from '../../@models';
import { ApiMessageHandlerService } from '../message-handler/message-handler-service';

export interface ApiResultContext<Model> {
  result: ApiResult<Model>;
  title: string;
  show_message: boolean;
  message_method: MessageMethod;
  service: ApiMessageHandlerService;
}
