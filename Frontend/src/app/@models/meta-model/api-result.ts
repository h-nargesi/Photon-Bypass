export interface ApiResultStatus {
  code: number;
  status(): ResultStatus;
}

export interface ApiResult extends ApiResultStatus {
  code: number;
  message?: string;
  developer?: any;
  method?: MessageMethod;
}

export interface ApiResultData<Model> extends ApiResult {
  data?: Model;
}

export enum MessageMethod {
  toaster,
  dialog,
  alert,
  console,
}

export enum ResultStatus {
  info,
  success,
  warning,
  error,
  serverFatal,
  uiFatal,
}
