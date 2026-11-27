import { HttpRequest, provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { provideRouter } from '@angular/router';
import {
  OperatingSystem,
  ServerConnectionReport,
  ServerFeature,
  ServerRow,
  ServerTransport,
} from '../../@models';
import { API_BASE_URL, BYTES_PER_GIGABYTE, printCapacity } from '../../@services';
import { ServersComponent } from './servers.component';

describe('ServersComponent (Docs/20 phase 6)', () => {
  let http: HttpTestingController;
  let fixture: ComponentFixture<ServersComponent>;
  let component: ServersComponent;

  const rows: ServerRow[] = [
    {
      id: 7,
      isActive: true,
      realmId: 1,
      realm: 'main',
      ipAddress: '10.0.0.1',
      name: 'nas-1',
      domainName: 'nas1.example.com',
      capacityBytes: 200 * BYTES_PER_GIGABYTE,
      osType: OperatingSystem.Mikrotik,
      features: ServerFeature.UserManager | ServerFeature.Ovpn,
      hasConfig: true,
      created: 1700000000000,
      createdTitle: '۱۴۰۲/۰۸/۰۱',
    },
  ];

  const flushList = () => {
    http
      .expectOne(
        req => req.method === 'GET' && req.url === '/api/server/list',
      )
      .flush({ code: 200, data: rows }, { status: 200, statusText: 'OK' });
  };

  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideNoopAnimations(),
        provideRouter([]),
        { provide: API_BASE_URL, useValue: '/api' },
      ],
    });
    http = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(ServersComponent);
    component = fixture.componentInstance;
  });

  afterEach(() => {
    http.verify();
    localStorage.clear();
  });

  it('loads the server list', () => {
    fixture.detectChanges();
    flushList();

    expect(component.rows.length).toBe(1);
    expect(component.featureTitles(rows[0])).toEqual(['UserManager', 'OpenVPN']);
  });

  it('the list renders the capacity as a formatted value, never the raw bytes (Docs/24 D3)', () => {
    fixture.detectChanges();
    flushList();
    fixture.detectChanges();

    const text: string = fixture.nativeElement.textContent;

    expect(text).toContain(printCapacity(200 * BYTES_PER_GIGABYTE));
    expect(text).toContain('GB');
    expect(text).not.toContain(String(rows[0].capacityBytes));
  });

  it('toggle-active posts the two-way lifecycle flag', () => {
    fixture.detectChanges();
    flushList();

    component.toggleActive(rows[0]);

    const request = http.expectOne(
      req => req.method === 'POST' && req.url === '/api/server/set-active',
    );
    expect(request.request.body).toEqual({ id: 7, active: false });
    request.flush(
      { code: 200, message: 'غیرفعال شد' },
      { status: 200, statusText: 'OK' },
    );

    flushList();
  });

  it('create posts the parsed config json and computed feature bits', () => {
    fixture.detectChanges();
    flushList();

    component.openCreate();
    component.name = 'nas-2';
    component.ip_address = ' 10.0.0.2 ';
    component.domain_name = 'nas2.example.com';
    component.realm_id = 2;
    component.capacity_gigabytes = 200;
    component.os_type = OperatingSystem.Ubuntu;
    component.feature_state[ServerFeature.Xui] = true;
    component.feature_state[ServerFeature.V2ray] = true;
    component.max_clients = 50;
    component.config_text = '{ "sshConfig": { "username": "root" } }';
    component.submit();

    const request = http.expectOne(
      req => req.method === 'POST' && req.url === '/api/server/create',
    );
    expect(request.request.body).toEqual({
      id: 0,
      realmId: 2,
      ipAddress: '10.0.0.2',
      name: 'nas-2',
      domainName: 'nas2.example.com',
      capacityBytes: 200 * BYTES_PER_GIGABYTE,
      maxClients: 50,
      osType: OperatingSystem.Ubuntu,
      features: ServerFeature.Xui | ServerFeature.V2ray,
      config: { sshConfig: { username: 'root' } },
    });
    request.flush(
      { code: 200, message: 'ساخته شد' },
      { status: 200, statusText: 'OK' },
    );

    flushList();
    expect(component.edit_visible).toBeFalse();
  });

  it('invalid config json blocks the submit', () => {
    fixture.detectChanges();
    flushList();

    component.openCreate();
    component.name = 'nas-3';
    component.ip_address = '10.0.0.3';
    component.domain_name = '';
    component.realm_id = 1;
    component.capacity_gigabytes = 1;
    component.config_text = 'not-json';
    component.submit();

    http.expectNone(
      req => req.method === 'POST' && req.url.startsWith('/api/server/'),
    );
  });

  it('edit pre-fills the capacity in gigabytes and keeps the stored bytes on an unrelated edit', () => {
    fixture.detectChanges();
    flushList();

    component.openEdit(rows[0]);
    expect(component.capacity_gigabytes).toBe(200);

    // a value that is not a whole gigabyte: the pre-fill is not rounded, so the
    // domain edit below cannot shift the stored figure
    const odd_row: ServerRow = { ...rows[0], id: 9, capacityBytes: 1_073_741_825 };

    component.openEdit(odd_row);
    expect(component.capacity_gigabytes).toBe(1_073_741_825 / BYTES_PER_GIGABYTE);

    component.domain_name = 'odd.example.com';
    component.submit();

    const request = http.expectOne(
      req => req.method === 'POST' && req.url === '/api/server/update',
    );
    expect(request.request.body['id']).toBe(9);
    expect(request.request.body['capacityBytes']).toBe(1_073_741_825);
    expect(request.request.body['domainName']).toBe('odd.example.com');
    request.flush(
      { code: 200, message: 'ذخیره شد' },
      { status: 200, statusText: 'OK' },
    );

    flushList();
  });

  it('update with an empty config box keeps the stored blob (config omitted)', () => {
    fixture.detectChanges();
    flushList();

    component.openEdit(rows[0]);
    component.domain_name = 'changed.example.com';
    component.config_text = '';
    component.submit();

    const request = http.expectOne(
      req => req.method === 'POST' && req.url === '/api/server/update',
    );
    expect(request.request.body['id']).toBe(7);
    expect(request.request.body['config']).toBeUndefined();
    expect(request.request.body['domainName']).toBe('changed.example.com');
    expect(request.request.body['features']).toBe(rows[0].features);
    request.flush(
      { code: 200, message: 'ذخیره شد' },
      { status: 200, statusText: 'OK' },
    );

    flushList();
  });

  it('x-ui rows carry the client cap; dropping the Xui bit clears it', () => {
    fixture.detectChanges();
    flushList();

    const xui_row: ServerRow = {
      ...rows[0],
      id: 8,
      features: ServerFeature.Xui | ServerFeature.V2ray,
      maxClients: 12,
    };

    component.openEdit(xui_row);
    expect(component.is_xui).toBeTrue();
    expect(component.max_clients).toBe(12);
    component.submit();

    let request = http.expectOne(
      req => req.method === 'POST' && req.url === '/api/server/update',
    );
    expect(request.request.body['id']).toBe(8);
    expect(request.request.body['maxClients']).toBe(12);
    request.flush(
      { code: 200, message: 'ذخیره شد' },
      { status: 200, statusText: 'OK' },
    );

    flushList();

    // the cap is x-ui-only: without the bit the payload clears it (gate off)
    component.openEdit(xui_row);
    component.feature_state[ServerFeature.Xui] = false;
    component.submit();

    request = http.expectOne(
      req => req.method === 'POST' && req.url === '/api/server/update',
    );
    expect(request.request.body['maxClients']).toBeNull();
    request.flush(
      { code: 200, message: 'ذخیره شد' },
      { status: 200, statusText: 'OK' },
    );

    flushList();
  });

  // --- transport selector and connection test (Docs/25 D3, D8–D9) ---

  const report_of = (steps: ServerConnectionReport['steps'], host_key?: string): ServerConnectionReport => ({
    passed: steps.every(step => step.passed),
    steps,
    hostKeySha256: host_key,
    machineName: 'PANEL-HOST',
  });

  const report_body = (report: ServerConnectionReport) => ({ code: 200, data: report });

  const OK = { status: 200, statusText: 'OK' };

  const TEST_URL = (req: HttpRequest<unknown>) =>
    req.method === 'POST' && req.url === '/api/server/test-connection';

  it('the tunnel selector fills an empty config box with the tunnel template', () => {
    fixture.detectChanges();
    flushList();

    component.openCreate();
    component.transport = ServerTransport.SshTunnel;
    component.onTransportChange();

    expect(JSON.parse(component.config_text)).toEqual({
      transport: 'SshTunnel',
      sshConfig: {
        username: '',
        port: 22,
        privateKey: '-----BEGIN OPENSSH PRIVATE KEY-----\n...\n-----END OPENSSH PRIVATE KEY-----',
        hostKeySha256: 'SHA256:',
      },
      webApiConfig: { hostName: '127.0.0.1', port: 2053, username: '', password: '' },
    });
  });

  it('the selector writes the transport into a config that names none, and back to direct removes it', () => {
    fixture.detectChanges();
    flushList();

    component.openCreate();
    component.config_text = '{ "sshConfig": { "username": "root" } }';
    component.onConfigChange();

    component.transport = ServerTransport.SshTunnel;
    component.onTransportChange();

    expect(JSON.parse(component.config_text)).toEqual({
      transport: 'SshTunnel',
      sshConfig: { username: 'root' },
    });

    component.transport = ServerTransport.Direct;
    component.onTransportChange();

    expect(JSON.parse(component.config_text)).toEqual({ sshConfig: { username: 'root' } });
  });

  it('a config that names its transport moves the selector', () => {
    fixture.detectChanges();
    flushList();

    component.openCreate();
    component.config_text = '{ "transport": "SshTunnel", "sshConfig": { "username": "panel" } }';
    component.onConfigChange();

    expect(component.transport).toBe(ServerTransport.SshTunnel);

    component.config_text = 'not-json';
    component.onConfigChange();

    expect(component.transport).toBe(ServerTransport.SshTunnel);
  });

  it('create with the tunnel selector posts the transport inside the config', () => {
    fixture.detectChanges();
    flushList();

    component.openCreate();
    component.name = 'tunnel-1';
    component.ip_address = '10.0.0.8';
    component.realm_id = 1;
    component.capacity_gigabytes = 1;
    component.feature_state[ServerFeature.Xui] = true;
    component.feature_state[ServerFeature.V2ray] = true;
    component.config_text = '{ "sshConfig": { "username": "panel" } }';
    component.transport = ServerTransport.SshTunnel;
    component.submit();

    const request = http.expectOne(
      req => req.method === 'POST' && req.url === '/api/server/create',
    );

    expect(request.request.body['config']).toEqual({
      sshConfig: { username: 'panel' },
      transport: 'SshTunnel',
    });

    request.flush({ code: 200, message: 'ساخته شد' }, { status: 200, statusText: 'OK' });

    flushList();
  });

  it('test-connection posts the unsaved form; nothing is saved and an empty box keeps the config omitted', () => {
    fixture.detectChanges();
    flushList();

    component.openEdit(rows[0]);
    component.domain_name = 'changed.example.com';
    component.testForm();

    const request = http.expectOne(
      req => req.method === 'POST' && req.url === '/api/server/test-connection',
    );

    expect(request.request.body).toEqual({
      id: 7,
      realmId: 1,
      ipAddress: '10.0.0.1',
      name: 'nas-1',
      domainName: 'changed.example.com',
      capacityBytes: rows[0].capacityBytes,
      maxClients: null,
      osType: OperatingSystem.Mikrotik,
      features: rows[0].features,
    });

    http.expectNone(req => req.url === '/api/server/update' || req.url === '/api/server/create');

    request.flush(report_body(report_of([{ step: 'tcp', passed: true, detail: 'پورت ۲۲' }])), OK);

    expect(component.test_state).toBe('passed');
    expect(component.test_visible).toBeTrue();
    expect(component.edit_visible).toBeTrue();
  });

  it('test-connection with a config in the box posts that config', () => {
    fixture.detectChanges();
    flushList();

    component.openCreate();
    component.name = 'tunnel-2';
    component.ip_address = '10.0.0.9';
    component.realm_id = 1;
    component.capacity_gigabytes = 1;
    component.config_text = '{ "transport": "SshTunnel", "sshConfig": { "username": "panel" } }';
    component.testForm();

    const request = http.expectOne(
      req => req.method === 'POST' && req.url === '/api/server/test-connection',
    );

    expect(request.request.body['id']).toBe(0);
    expect(request.request.body['config']).toEqual({
      transport: 'SshTunnel',
      sshConfig: { username: 'panel' },
    });

    request.flush(report_body(report_of([{ step: 'tcp', passed: false, detail: 'refused' }])), OK);

    expect(component.test_state).toBe('failed');
  });

  it('invalid config json blocks the test like the submit', () => {
    fixture.detectChanges();
    flushList();

    component.openCreate();
    component.config_text = 'not-json';
    component.testForm();

    http.expectNone(req => req.method === 'POST' && req.url.startsWith('/api/server/'));
    expect(component.test_visible).toBeFalse();
  });

  it('the row action posts only the id, so the stored configuration is tested', () => {
    fixture.detectChanges();
    flushList();

    component.testRow(rows[0]);

    const request = http.expectOne(
      req => req.method === 'POST' && req.url === '/api/server/test-connection',
    );

    expect(request.request.body).toEqual({ id: 7 });

    request.flush(report_body(report_of([
      { step: 'tcp', passed: true },
      { step: 'host-key', passed: false, detail: 'mismatch' },
    ])), OK);

    expect(component.test_state).toBe('failed');
  });

  it('the report renders its steps in order with their details', () => {
    fixture.detectChanges();
    flushList();

    component.testRow(rows[0]);

    http.expectOne(TEST_URL).flush(report_body(report_of([
      { step: 'tcp', passed: true, detail: 'اتصال TCP برقرار شد (پورت ۲۲)' },
      { step: 'host-key', passed: false, detail: 'SHA256:BBBB' },
    ])), OK);
    fixture.detectChanges();

    const text: string = fixture.nativeElement.textContent;

    expect(text).toContain('اتصال برقرار نشد');
    expect(text.indexOf('اتصال TCP به پورت SSH')).toBeLessThan(text.indexOf('کلید میزبان (host key)'));
    expect(text).toContain('اتصال TCP برقرار شد (پورت ۲۲)');
    expect(text).toContain('SHA256:BBBB');
    expect(text).toContain('PANEL-HOST');
  });

  it('a refused request shows the error state instead of a report', () => {
    fixture.detectChanges();
    flushList();

    component.testRow(rows[0]);

    http
      .expectOne(req => req.method === 'POST' && req.url === '/api/server/test-connection')
      .flush({ code: 403, message: 'اجازه ندارید' }, { status: 403, statusText: 'Forbidden' });

    expect(component.test_state).toBe('error');
    expect(component.test_report).toBeUndefined();
  });

  it('a discovered host key of a form test is written into the config box (Docs/25 D8)', () => {
    fixture.detectChanges();
    flushList();

    component.openCreate();
    component.name = 'tunnel-3';
    component.ip_address = '10.0.0.10';
    component.config_text = '{ "transport": "SshTunnel", "sshConfig": { "username": "panel", "port": 22 } }';
    component.transport = ServerTransport.SshTunnel;
    component.testForm();

    http.expectOne(TEST_URL).flush(report_body(report_of(
      [{ step: 'tcp', passed: true }, { step: 'host-key', passed: true }, { step: 'auth', passed: false }],
      'SHA256:AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA',
    )), OK);

    expect(component.can_apply_host_key).toBeTrue();

    component.applyHostKey();

    expect(JSON.parse(component.config_text)).toEqual({
      transport: 'SshTunnel',
      sshConfig: {
        username: 'panel',
        port: 22,
        hostKeySha256: 'SHA256:AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA',
      },
    });
    expect(component.transport).toBe(ServerTransport.SshTunnel);
  });

  it('a host key learned from a stored-row test has no write-back: the stored config is never read', () => {
    fixture.detectChanges();
    flushList();

    component.openCreate();
    component.config_text = '{ "sshConfig": { "username": "panel" } }';
    component.testRow(rows[0]);

    http.expectOne(TEST_URL).flush(report_body(report_of(
      [{ step: 'tcp', passed: true }, { step: 'host-key', passed: true }, { step: 'auth', passed: false }],
      'SHA256:AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA',
    )), OK);

    expect(component.can_apply_host_key).toBeFalse();

    component.applyHostKey();

    expect(component.config_text).toBe('{ "sshConfig": { "username": "panel" } }');
  });
});
