import { Injectable, TemplateRef } from '@angular/core';
import { ToasterComponent } from '@coreui/angular';
import { ApiResult, MessageMethod, ResultStatus } from '../../@models';
import { AppToastComponent } from '../../default-layout/toast/toast.component';

@Injectable({ providedIn: 'root' })
export class MessageService {
  private toaster?: ToasterComponent;
  private dialog?: TemplateRef<any>;
  private buffer: {
    toaster: any[];
    dialog: any[];
  } = {
    toaster: [],
    dialog: [],
  };

  public get dialogBox(): TemplateRef<any> | undefined {
    return this.dialog;
  }

  public registerToaster(toaster: ToasterComponent) {
    this.toaster = toaster;
    if (!toaster) return;
    while (this.buffer.toaster.length > 0) {
      this.toaster.addToast(AppToastComponent, this.buffer.toaster.pop());
    }
  }

  public registerDialogBox(dialog: TemplateRef<any>) {
    this.dialog = dialog;
    // if (!dialog) return;
    // while (this.buffer.dialog.length > 0) {
    //   this.dialog.show(AppToastComponent, this.buffer.dialog.pop());
    // }
  }

  public resultHandler(title: string, result: ApiResult) {
    if (!title && !result?.message?.length) return;
    this.messageHandler(result.status(), title, result.message, result.method);
  }

  public messageHandler(
    code: ResultStatus,
    title: string,
    body?: string,
    method: MessageMethod = MessageMethod.toaster,
    duration?: number
  ): any {
    if (!method) method = MessageMethod.toaster;
    if (duration == null) duration = 5000;

    const status = MessageService.getStatusCode(code);

    if (method === MessageMethod.toaster) {
      const props: any = {
        title,
        body,
        color: status,
        autohide: true,
        delay: duration,
        fade: true,
      };
      if (this.toaster) {
        this.toaster.addToast(AppToastComponent, props);
        return;
      } else this.buffer.toaster.push(props);
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

    console.info(title, body);
  }

  public static getStatusCode(status: ResultStatus): string {
    if (status >= ResultStatus.error) return 'danger';
    else if (status >= ResultStatus.warning) return 'warning';
    else if (status >= ResultStatus.success) return 'success';
    else return 'info';
  }
}
