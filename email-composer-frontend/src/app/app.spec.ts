import { TestBed } from '@angular/core/testing';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideHttpClient } from '@angular/common/http';
import { environment } from '../environments/environment';
import { App } from './app';

describe('App', () => {
  let httpTestingController: HttpTestingController;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [App],
      providers: [provideHttpClient(), provideHttpClientTesting()]
    }).compileComponents();

    httpTestingController = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpTestingController.verify();
  });

  function flushOrganizationRoles(organizationRoles: string[] = []): void {
    httpTestingController
      .expectOne(`${environment.apiBaseUrl}/api/mail/organization-roles`)
      .flush(organizationRoles);
  }

  it('should create the app', async () => {
    const fixture = TestBed.createComponent(App);
    const app = fixture.componentInstance;

    fixture.detectChanges();
    flushOrganizationRoles();
    await fixture.whenStable();

    expect(app).toBeTruthy();
  });

  it('should load organization roles on init', async () => {
    const fixture = TestBed.createComponent(App);
    const app = fixture.componentInstance;

    fixture.detectChanges();
    flushOrganizationRoles(['Operations', 'WFM']);

    await fixture.whenStable();

    expect(app.organizationRoles).toEqual(['Operations', 'WFM']);
  });

  it('should require organization role when SQL recipients are enabled', async () => {
    const fixture = TestBed.createComponent(App);
    const app = fixture.componentInstance;

    fixture.detectChanges();
    flushOrganizationRoles(['Operations']);
    await fixture.whenStable();

    app.includeSqlRecipients = true;

    await app.sendMail();

    expect(app.statusMessage).toContain('Select an organization role');
    httpTestingController.expectNone(`${environment.apiBaseUrl}/api/mail/send`);
  });
});
