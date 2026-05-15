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
    await fixture.whenStable();

    const app = fixture.componentInstance;
    expect(app).toBeTruthy();
  });

  it('should render title', async () => {
    const fixture = TestBed.createComponent(App);
    const httpTesting = TestBed.inject(HttpTestingController);
    fixture.detectChanges();
    httpTesting.expectOne(organizationRolesUrl).flush([]);
    await fixture.whenStable();
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('h1')?.textContent).toContain('HTML Email Composer');
  });

  it('should render composer controls in the requested order', async () => {
    const fixture = TestBed.createComponent(App);
    const httpTesting = TestBed.inject(HttpTestingController);
    fixture.detectChanges();
    httpTesting.expectOne(organizationRolesUrl).flush([]);
    await fixture.whenStable();
    fixture.detectChanges();

    const controls = Array.from(
      (fixture.nativeElement as HTMLElement).querySelectorAll(
        '.form-grid input, .form-grid select'
      )
    ).map((control) => {
      const input = control as HTMLInputElement | HTMLSelectElement;
      return input.name || input.type;
    });

    expect(controls).toEqual([
      'subject',
      'organizationRole',
      'userRole',
      'toRecipients',
      'file'
    ]);
  });

  it('should render organization role options from the API and default to Select Role', async () => {
    const fixture = TestBed.createComponent(App);
    const httpTesting = TestBed.inject(HttpTestingController);
    fixture.detectChanges();
    httpTesting.expectOne(organizationRolesUrl).flush(['Accounting', 'Construction Contractor']);
    await fixture.whenStable();
    fixture.detectChanges();

    const options = Array.from(
      (fixture.nativeElement as HTMLElement).querySelectorAll('select[name="organizationRole"] option')
    ).map((option) => option.textContent?.trim());

    expect(options).toContain('Accounting');
    expect(options).toContain('Construction Contractor');
    expect(options).toContain('Select Role');
    expect(fixture.componentInstance.selectedOrganizationRole).toBe('');
  });

  it('should not render an include SQL recipients checkbox', async () => {
    const fixture = TestBed.createComponent(App);
    const httpTesting = TestBed.inject(HttpTestingController);
    fixture.detectChanges();
    httpTesting.expectOne(organizationRolesUrl).flush([]);
    await fixture.whenStable();
    fixture.detectChanges();

    expect(
      (fixture.nativeElement as HTMLElement).querySelector('input[name="includeSqlRecipients"]')
    ).toBeNull();
  });

  it('should render user role options based on organization role and default to Select Role', async () => {
    const fixture = TestBed.createComponent(App);
    const httpTesting = TestBed.inject(HttpTestingController);
    fixture.detectChanges();
    httpTesting.expectOne(organizationRolesUrl).flush(['Construction Contractor']);

    fixture.componentInstance.onOrganizationRoleChange('Construction Contractor');
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
    expect(options).toContain('Select Role');
    expect(fixture.componentInstance.selectedUserRole).toBe('');
  });

  it('should reload user role options when the organization role changes', async () => {
    const fixture = TestBed.createComponent(App);
    const httpTesting = TestBed.inject(HttpTestingController);
    fixture.detectChanges();
    httpTesting.expectOne(organizationRolesUrl).flush(['Construction Contractor', 'Operations']);

    fixture.componentInstance.onOrganizationRoleChange('Construction Contractor');
    expectUserRolesRequest(httpTesting, 'Construction Contractor').flush(['WFM Administrator']);
    await fixture.whenStable();

    fixture.componentInstance.onOrganizationRoleChange('Operations');
    await Promise.resolve();
    expectUserRolesRequest(httpTesting, 'Operations').flush(['Supervisor']);
    await fixture.whenStable();

    expect(fixture.componentInstance.userRoles).toEqual(['Supervisor']);
    expect(fixture.componentInstance.selectedUserRole).toBe('');
  });

  it('should include SQL recipients automatically when a user role is selected', async () => {
    const fixture = TestBed.createComponent(App);
    const httpTesting = TestBed.inject(HttpTestingController);
    fixture.detectChanges();
    httpTesting.expectOne(organizationRolesUrl).flush(['Construction Contractor']);

    const app = fixture.componentInstance;
    app.onOrganizationRoleChange('Construction Contractor');
    expectUserRolesRequest(httpTesting, 'Construction Contractor').flush(['WFM Administrator']);
    await fixture.whenStable();

    app.selectedUserRole = 'WFM Administrator';
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

  it('should send manual recipients without SQL recipients when no user role is selected', async () => {
    const fixture = TestBed.createComponent(App);
    const httpTesting = TestBed.inject(HttpTestingController);
    fixture.detectChanges();
    httpTesting.expectOne(organizationRolesUrl).flush(['Construction Contractor']);
    await fixture.whenStable();

    const app = fixture.componentInstance;
    app.subject = 'Manual update';
    app.bodyHtml = '<p>Hello</p>';
    app.toRecipients = 'person@example.com';

    const sendPromise = app.sendMail();
    const sendRequest = httpTesting.expectOne(`${environment.apiBaseUrl}/api/mail/send`);
    expect(sendRequest.request.body).toEqual({
      subject: 'Manual update',
      bodyHtml: '<p>Hello</p>',
      includeSqlRecipients: false,
      organizationRole: null,
      userRole: null,
      toRecipients: ['person@example.com']
    });
    sendRequest.flush({ message: 'Mail sent.' });
    await sendPromise;
  });

  it('should disable send until there is a valid manual email or selected user role', async () => {
    const fixture = TestBed.createComponent(App);
    const httpTesting = TestBed.inject(HttpTestingController);
    fixture.detectChanges();
    httpTesting.expectOne(organizationRolesUrl).flush(['Construction Contractor']);
    await fixture.whenStable();
    fixture.detectChanges();

    const app = fixture.componentInstance;
    const sendButton = (fixture.nativeElement as HTMLElement).querySelector<HTMLButtonElement>('button');

    expect(sendButton?.disabled).toBe(true);
    expect(app.isSendDisabled).toBe(true);

    app.toRecipients = 'not-an-email';
    expect(app.isSendDisabled).toBe(true);

    app.toRecipients = 'person@example.com';
    expect(app.isSendDisabled).toBe(false);

    app.toRecipients = '';
    app.selectedOrganizationRole = 'Construction Contractor';
    app.selectedUserRole = 'WFM Administrator';
    expect(app.isSendDisabled).toBe(false);
  });

  function expectUserRolesRequest(httpTesting: HttpTestingController, organizationRole: string) {
    return httpTesting.expectOne((request) =>
      request.url === userRolesUrl &&
      request.params.get('organizationRole') === organizationRole
    );
  }
});
