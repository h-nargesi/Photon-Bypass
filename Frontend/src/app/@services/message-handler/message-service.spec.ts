import { TestBed } from '@angular/core/testing';
import { ToasterComponent } from '@coreui/angular';
import { ResultStatus } from '../../@models';
import { MessageService } from './message-service';

describe('MessageService', () => {
  let service: MessageService;

  const toaster = () =>
    ({ addToast: jasmine.createSpy('addToast') }) as unknown as ToasterComponent;

  beforeEach(() => {
    TestBed.configureTestingModule({});
    service = TestBed.inject(MessageService);
  });

  it('forwards a toast to the registered toaster', () => {
    const registered = toaster();
    service.registerToaster(registered);

    service.messageHandler(ResultStatus.error, 'title', 'body');

    expect(registered.addToast).toHaveBeenCalledTimes(1);
  });

  it('queues nothing without a registered toaster: a later registration shows nothing', () => {
    service.messageHandler(ResultStatus.error, 'title', 'body');

    const late = toaster();
    service.registerToaster(late);

    expect(late.addToast).not.toHaveBeenCalled();
  });

  it('unregisterToaster stops later messages', () => {
    const registered = toaster();
    service.registerToaster(registered);

    service.unregisterToaster(registered);
    service.messageHandler(ResultStatus.error, 'title', 'body');

    expect(registered.addToast).not.toHaveBeenCalled();
  });

  it('unregistering a different instance keeps the registered one', () => {
    const registered = toaster();
    service.registerToaster(registered);

    service.unregisterToaster(toaster());
    service.messageHandler(ResultStatus.error, 'title', 'body');

    expect(registered.addToast).toHaveBeenCalledTimes(1);
  });
});
