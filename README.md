# HTML Email Composer (Angular + .NET + Microsoft Graph)

This repository contains:

- `email-composer-frontend`: Angular app with a rich text editor (Quill) for composing HTML email and inserting local image files.
- `email-composer-backend`: ASP.NET Core Web API that acquires a token via Azure AD client credentials and calls:
  - `POST https://graph.microsoft.com/v1.0/users/{id}/sendMail`

## Frontend

### Features

- Rich text editor for HTML body
- Insert local image files into editor as inline base64 images
- To/Subject fields
- Calls backend endpoint `POST /api/mail/send`

### Run

```bash
cd email-composer-frontend
npm install
npm start
```

By default the frontend calls `http://localhost:5000` (see `src/environments/environment.ts`).

## Backend

### Graph configuration

Configure these values in `email-composer-backend/appsettings.Development.json` or environment variables:

```json
"Graph": {
  "TenantId": "<azure-tenant-id>",
  "ClientId": "<app-registration-client-id>",
  "ClientSecret": "<app-registration-client-secret>",
  "SenderUserId": "<user-id-or-upn>",
  "Scope": "https://graph.microsoft.com/.default"
}
```

The Azure app registration requires **Application** permissions such as `Mail.Send`, with admin consent.

### Run

```bash
cd email-composer-backend
dotnet restore
dotnet run
```

Backend listens on `http://localhost:5000` by default via launch settings.

## API contract

`POST /api/mail/send`

```json
{
  "subject": "Hello",
  "bodyHtml": "<p>Message body</p>",
  "toRecipients": ["person@contoso.com"]
}
```
