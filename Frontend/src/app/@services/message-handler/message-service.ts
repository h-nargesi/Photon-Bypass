import { Injectable, TemplateRef } from '@angular/core';
import { ApiResult, MessageMethod } from '../../@models';

@Injectable({ providedIn: 'root' })
export class MessageService {
  private dialog: TemplateRef<any> | undefined;
  private buffer: any[] = [];

  constructor() // private readonly toaster?: NbToastrService,
  // private readonly dialoger?: NbDialogService,
  {}

  public get dialogBox(): TemplateRef<any> | undefined {
    return this.dialog;
  }

  public set dialogBox(dialog: TemplateRef<any>) {
    this.dialog = dialog;
    // while (this.buffer.length > 0) {
    //   this.dialoger.open(this.dialog, {
    //     context: this.buffer.pop(),
    //     closeOnBackdropClick: false,
    //   });
    // }
  }

  public resultHandler(title: string, result: ApiResult) {
    if (!title && !result?.message?.length) return;
    this.messageHandler(result.code, title, result.message, result.method);
  }

  public messageHandler(
    code: number,
    title: string,
    body: string,
    method: MessageMethod = MessageMethod.toaster,
    duration?: number
  ): any {
    if (!method) method = MessageMethod.toaster;
    if (duration == null) duration = 5000;

    const status = MessageService.getStatusCode(code);

    if (method === MessageMethod.toaster) {
      // if (this.toaster) {
      //   this.toaster.show(body, title, {
      //     status: status,
      //     destroyByClick: true,
      //     duration: duration,
      //     hasIcon: true,
      //     position: NbGlobalPhysicalPosition.TOP_LEFT,
      //     preventDuplicates: false,
      //   });
      //   return;
      // } else
      console.error('The toaster-service is not set.');
    } else if (method === MessageMethod.dialog) {
      // if (this.dialoger) {
      //   if (this.dialog) {
      //     return this.dialoger.open(this.dialog, {
      //       context: { title, body, status },
      //       closeOnBackdropClick: false,
      //     });
      //   } else {
      //     this.buffer.push({ title, body, status });
      //   }
      // } else
      console.error('The dialog-service is not set.');
    } else if (method === MessageMethod.alert) {
      alert(title + '\n' + body);
      return;
    }

    console.log(title, body);
  }

  public static getStatusCode(code: number): string {
    if (code >= 400) return 'danger';
    else if (code >= 300) return 'warning';
    else if (code >= 200) return 'success';
    else return 'info';
  }
}
