import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { App } from './app';
import { environment } from '../environments/environment';

describe('App', () => {
  const organizationRolesUrl = `${environment.apiBaseUrl}/api/mail/organization-roles`;
  const userRolesUrl = `${environment.apiBaseUrl}/api/mail/user-roles`;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [App],
      providers: [provideHttpClient(), provideHttpClientTesting()]
    }).compileComponents();
  });

  afterEach(() => {
    TestBed.inject(HttpTestingController).verify();
  });

  it('should create the app', async () => {
    const fixture = TestBed.createComponent(App);
    const httpTesting = TestBed.inject(HttpTestingController);
    fixture.detectChanges();
    httpTesting.expectOne(organizationRolesUrl).flush([]);
    await Promise.resolve();
    expectUserRolesRequest(httpTesting, 'construction contractor').flush([]);
    await fixture.whenStable();

    const app = fixture.componentInstance;
    expect(app).toBeTruthy();
  });

  it('should render title', async () => {
    const fixture = TestBed.createComponent(App);
    const httpTesting = TestBed.inject(HttpTestingController);
    fixture.detectChanges();
    httpTesting.expectOne(organizationRolesUrl).flush([]);
    await Promise.resolve();
    expectUserRolesRequest(httpTesting, 'construction contractor').flush([]);
    await fixture.whenStable();
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('h1')?.textContent).toContain('HTML Email Composer');
  });

  it('should render organization role options from the API and default to construction contractor', async () => {
    const fixture = TestBed.createComponent(App);
    const httpTesting = TestBed.inject(HttpTestingController);
    fixture.detectChanges();
    httpTesting.expectOne(organizationRolesUrl).flush(['Accounting', 'Construction Contractor']);
    await Promise.resolve();
    expectUserRolesRequest(httpTesting, 'Construction Contractor').flush(['WFM Administrator']);
    await fixture.whenStable();
    fixture.detectChanges();

    const options = Array.from(
      (fixture.nativeElement as HTMLElement).querySelectorAll('select[name="organizationRole"] option')
    ).map((option) => option.textContent?.trim());

    expect(options).toContain('Accounting');
    expect(options).toContain('Construction Contractor');
    expect(fixture.componentInstance.selectedOrganizationRole).toBe('Construction Contractor');
  });

  it('should render user role options based on organization role and default to WFM Administrator', async () => {
    const fixture = TestBed.createComponent(App);
    const httpTesting = TestBed.inject(HttpTestingController);
    fixture.detectChanges();
    httpTesting.expectOne(organizationRolesUrl).flush(['Construction Contractor']);
    await Promise.resolve();
    expectUserRolesRequest(httpTesting, 'Construction Contractor').flush([
      'Project Manager',
      'WFM Administrator'
    ]);
    await fixture.whenStable();
    fixture.detectChanges();

    const options = Array.from(
      (fixture.nativeElement as HTMLElement).querySelectorAll('select[name="userRole"] option')
    ).map((option) => option.textContent?.trim());

    expect(options).toContain('Project Manager');
    expect(options).toContain('WFM Administrator');
    expect(fixture.componentInstance.selectedUserRole).toBe('WFM Administrator');
  });

  it('should reload user role options when the organization role changes', async () => {
    const fixture = TestBed.createComponent(App);
    const httpTesting = TestBed.inject(HttpTestingController);
    fixture.detectChanges();
    httpTesting.expectOne(organizationRolesUrl).flush(['Construction Contractor', 'Operations']);
    await Promise.resolve();
    expectUserRolesRequest(httpTesting, 'Construction Contractor').flush(['WFM Administrator']);
    await fixture.whenStable();

    fixture.componentInstance.onOrganizationRoleChange('Operations');
    await Promise.resolve();
    expectUserRolesRequest(httpTesting, 'Operations').flush(['Supervisor']);
    await fixture.whenStable();

    expect(fixture.componentInstance.userRoles).toEqual(['Supervisor']);
    expect(fixture.componentInstance.selectedUserRole).toBe('Supervisor');
  });

  it('should allow sending with empty manual recipients when SQL recipients are selected', async () => {
    const fixture = TestBed.createComponent(App);
    const httpTesting = TestBed.inject(HttpTestingController);
    fixture.detectChanges();
    httpTesting.expectOne(organizationRolesUrl).flush(['Construction Contractor']);
    await Promise.resolve();
    expectUserRolesRequest(httpTesting, 'Construction Contractor').flush(['WFM Administrator']);
    await fixture.whenStable();

    const app = fixture.componentInstance;
    app.includeSqlRecipients = true;
    app.subject = 'Project update';
    app.bodyHtml = '<p>Hello</p>';
    app.toRecipients = '';

    const sendPromise = app.sendMail();
    const sendRequest = httpTesting.expectOne(`${environment.apiBaseUrl}/api/mail/send`);
    expect(sendRequest.request.body).toEqual({
      subject: 'Project update',
      bodyHtml: '<p>Hello</p>',
      includeSqlRecipients: true,
      organizationRole: 'Construction Contractor',
      userRole: 'WFM Administrator',
      toRecipients: []
    });
    sendRequest.flush({ message: 'Mail sent.' });
    await sendPromise;
  });

  function expectUserRolesRequest(httpTesting: HttpTestingController, organizationRole: string) {
    return httpTesting.expectOne((request) =>
      request.url === userRolesUrl &&
      request.params.get('organizationRole') === organizationRole
    );
  }
});
