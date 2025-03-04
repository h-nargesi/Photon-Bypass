export interface ApiResult<Model> {
  code: number;
  message: string;
  developer?: any;
  method?: MessageMethod;
  data?: Model;
}

export enum MessageMethod {
  toaster, dialog, alert, console
}
