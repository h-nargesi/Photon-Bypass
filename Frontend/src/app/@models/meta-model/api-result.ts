export interface ApiResult {
  code: number;
  message: string;
  developer?: any;
  method?: MessageMethod;
}

export interface ApiResultData<Model> extends ApiResult {
  data?: Model;
}


export enum MessageMethod {
  toaster, dialog, alert, console
}
