import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { provideRouter } from '@angular/router';
import { OperatingSystem, ServerFeature, ServerRow } from '../../@models';
import { API_BASE_URL } from '../../@services';
import { ServersComponent } from './servers.component';

describe('ServersComponent (docs/20 phase 6)', () => {
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
      bandWidth: 100,
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
    component.band_width = 200;
    component.os_type = OperatingSystem.Ubuntu;
    component.feature_state[ServerFeature.Xui] = true;
    component.feature_state[ServerFeature.V2ray] = true;
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
      bandWidth: 200,
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
    component.band_width = 1;
    component.config_text = 'not-json';
    component.submit();

    http.expectNone(
      req => req.method === 'POST' && req.url.startsWith('/api/server/'),
    );
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
});
