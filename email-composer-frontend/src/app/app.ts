import { HttpClient, HttpClientModule } from '@angular/common/http';
import { Component, OnInit, ViewChild, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { QuillEditorComponent, QuillModule } from 'ngx-quill';
import Quill from 'quill';
import { firstValueFrom } from 'rxjs';
import { environment } from '../environments/environment';

@Component({
  selector: 'app-root',
  imports: [FormsModule, HttpClientModule, QuillModule],
  templateUrl: './app.html',
  styleUrl: './app.scss'
})
export class App implements OnInit {
  @ViewChild(QuillEditorComponent) editorComponent?: QuillEditorComponent;
  private readonly http = inject(HttpClient);

  toRecipients = '';
  includeSqlRecipients = false;
  subject = '';
  bodyHtml = '';
  isSending = false;
  statusMessage = '';
  organizationRole = '';
  organizationRoles: string[] = [];
  isLoadingOrganizationRoles = false;

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

  async ngOnInit(): Promise<void> {
    await this.loadOrganizationRoles();
  }

  async loadOrganizationRoles(): Promise<void> {
    this.isLoadingOrganizationRoles = true;
    try {
      const roles = await firstValueFrom(
        this.http.get<string[]>(
          `${environment.apiBaseUrl}/api/mail/organization-roles`
        )
      );
      this.organizationRoles = roles ?? [];
    } catch {
      this.organizationRoles = [];
    } finally {
      this.isLoadingOrganizationRoles = false;
    }
  }

  async sendMail(): Promise<void> {
    if (this.isSending) {
      return;
    }

    this.statusMessage = '';
    this.isSending = true;

    try {
      const payload = {
        subject: this.subject,
        bodyHtml: this.bodyHtml,
        includeSqlRecipients: this.includeSqlRecipients,
        organizationRole: this.organizationRole || null,
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

  private fileToDataUrl(file: File): Promise<string> {
    return new Promise((resolve, reject) => {
      const reader = new FileReader();
      reader.onload = () => resolve(String(reader.result));
      reader.onerror = () => reject(reader.error);
      reader.readAsDataURL(file);
    });
  }
}
