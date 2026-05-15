import { HttpClient } from '@angular/common/http';
import { Component, OnInit, ViewChild, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { QuillEditorComponent, QuillModule } from 'ngx-quill';
import Quill from 'quill';
import { firstValueFrom } from 'rxjs';
import { environment } from '../environments/environment';

@Component({
  selector: 'app-root',
  imports: [FormsModule, QuillModule],
  templateUrl: './app.html',
  styleUrl: './app.scss'
})
export class App implements OnInit {
  @ViewChild(QuillEditorComponent) editorComponent?: QuillEditorComponent;
  private readonly http = inject(HttpClient);
  private userRoleRequestId = 0;

  toRecipients = '';
  selectedWorkerFunction = '';
  selectedPosition = '';
  organizationRoles: string[] = [];
  selectedOrganizationRole = '';
  userRoles: string[] = [];
  selectedUserRole = '';
  isLoadingOrganizationRoles = false;
  organizationRolesLoadFailed = false;
  isLoadingUserRoles = false;
  userRolesLoadFailed = false;
  subject = '';
  bodyHtml = '';
  isSending = false;
  statusMessage = '';

  readonly workerFunctions = environment.WorkerFunctions;
  readonly positions = environment.Positions;

  readonly editorModules = {
    toolbar: [
      [{ header: [1, 2, 3, false] }],
      ['bold', 'italic', 'underline', 'strike'],
      [{ list: 'ordered' }, { list: 'bullet' }],
      [{ color: [] }, { background: [] }],
      [{ align: [] }],
      ['link', 'image'],
      ['clean']
    ]
  };

  get isSendDisabled(): boolean {
    return this.isSending || (!this.hasValidManualRecipient && !this.hasSelectedSqlRecipientRole);
  }

  private get hasSelectedSqlRecipientRole(): boolean {
    return this.selectedOrganizationRole.length > 0 && this.selectedUserRole.length > 0;
  }

  private get hasValidManualRecipient(): boolean {
    return this.toRecipients
      .split(',')
      .map((email) => email.trim())
      .some((email) => this.isValidEmailAddress(email));
  }

  ngOnInit(): void {
    void this.loadOrganizationRoles();
  }

  async sendMail(): Promise<void> {
    if (this.isSending) {
      return;
    }

    this.statusMessage = '';

    const includeSqlRecipients = this.hasSelectedSqlRecipientRole;

    if (!this.hasValidManualRecipient && !includeSqlRecipients) {
      this.statusMessage = 'Enter a valid email address or select a SQL recipient role.';
      return;
    }

    if (includeSqlRecipients && !this.selectedOrganizationRole) {
      this.statusMessage = 'Select an organization role before sending to SQL recipients.';
      return;
    }

    if (includeSqlRecipients && !this.selectedUserRole) {
      this.statusMessage = 'Select a user role before sending to SQL recipients.';
      return;
    }

    this.isSending = true;

    try {
      const payload = {
        subject: this.subject,
        bodyHtml: this.bodyHtml,
        includeSqlRecipients,
        organizationRole: includeSqlRecipients ? this.selectedOrganizationRole : null,
        userRole: includeSqlRecipients ? this.selectedUserRole : null,
        toRecipients: this.toRecipients
          .split(',')
          .map((email) => email.trim())
          .filter((email) => email.length > 0)
      };

      await firstValueFrom(
        this.http.post(`${environment.apiBaseUrl}/api/mail/send`, payload)
      );
      this.statusMessage = 'Email sent successfully.';
    } catch {
      this.statusMessage = 'Failed to send email. Check backend logs for details.';
    } finally {
      this.isSending = false;
    }
  }

  async insertImage(event: Event): Promise<void> {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    if (!file) {
      return;
    }

    try {
      const dataUrl = await this.fileToDataUrl(file);
      const editor = this.editorComponent?.quillEditor;
      if (!editor) {
        this.bodyHtml += `<p><img src="${dataUrl}" alt="${file.name}" /></p>`;
        return;
      }

      const range = editor.getSelection(true);
      const index = range ? range.index : editor.getLength();
      editor.insertEmbed(index, 'image', dataUrl, Quill.sources.USER);
      editor.setSelection(index + 1, 0, Quill.sources.SILENT);
    } finally {
      input.value = '';
    }
  }

  onOrganizationRoleChange(organizationRole: string): void {
    this.selectedOrganizationRole = organizationRole;
    void this.loadUserRoles(organizationRole);
  }

  private async loadOrganizationRoles(): Promise<void> {
    this.isLoadingOrganizationRoles = true;
    this.organizationRolesLoadFailed = false;

    try {
      this.organizationRoles = await firstValueFrom(
        this.http.get<string[]>(`${environment.apiBaseUrl}/api/mail/organization-roles`)
      );
    } catch {
      this.organizationRoles = [];
      this.organizationRolesLoadFailed = true;
    } finally {
      this.isLoadingOrganizationRoles = false;
    }
  }

  private async loadUserRoles(organizationRole: string): Promise<void> {
    const requestId = ++this.userRoleRequestId;
    this.isLoadingUserRoles = true;
    this.userRolesLoadFailed = false;
    this.userRoles = [];
    this.selectedUserRole = '';

    if (!organizationRole) {
      this.isLoadingUserRoles = false;
      return;
    }

    try {
      const userRoles = await firstValueFrom(
        this.http.get<string[]>(`${environment.apiBaseUrl}/api/mail/user-roles`, {
          params: { organizationRole }
        })
      );

      if (requestId !== this.userRoleRequestId) {
        return;
      }

      this.userRoles = userRoles;
    } catch {
      if (requestId !== this.userRoleRequestId) {
        return;
      }

      this.userRoles = [];
      this.userRolesLoadFailed = true;
    } finally {
      if (requestId === this.userRoleRequestId) {
        this.isLoadingUserRoles = false;
      }
    }
  }

  private isValidEmailAddress(email: string): boolean {
    return /^[^\s@,]+@[^\s@,]+\.[^\s@,]+$/.test(email);
  }

  private fileToDataUrl(file: File): Promise<string> {
    return new Promise((resolve, reject) => {
      const reader = new FileReader();
      reader.onload = () => resolve(String(reader.result));
      reader.onerror = () => reject(reader.error);
      reader.readAsDataURL(file);
    });
  }
}
