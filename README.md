# HTML Email Composer (Angular + .NET + Microsoft Graph)

This repository contains:

- `email-composer-frontend`: Angular app with a rich text editor (Quill) for composing HTML email and inserting local image files.
- `email-composer-backend`: ASP.NET Core Web API that acquires a token via Azure AD client credentials and calls:
  - `POST https://graph.microsoft.com/v1.0/users/{id}/sendMail`

## Frontend

### Features

- Rich text editor for HTML body
- Insert local image files into editor as inline base64 images
- Manual comma-separated recipient textbox
- Optional SQL recipient inclusion filtered by selected organization role (appended to manual recipients when selected)
- Subject field
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

### SQL recipient source

The backend can optionally load recipients from Azure SQL using connection string key:

```json
"ConnectionStrings": {
  "AzureSqlDatabase": "<sql-connection-string>"
}
```

The frontend loads organization role choices from:

```sql
SELECT distinct [OrganizationRole]
FROM [dbo].[WorkerRole]
ORDER BY [OrganizationRole]
```

After an organization role is selected, the frontend loads user role choices from:

```sql
SELECT distinct [Name]
FROM [dbo].[WorkerRole]
WHERE [OrganizationRole] = @OrganizationRole
ORDER BY [Name]
```

Both role dropdowns default to a neutral `"Select Role"` option. The send button is enabled when there is at least one valid manual email address or a selected SQL user role.

If the request includes `"includeSqlRecipients": true`, SQL recipients for the selected `"organizationRole"` and `"userRole"` are appended to the manually provided recipients and deduplicated. The manual `"toRecipients"` list may be empty when SQL recipients are included.

### Run

```bash
cd email-composer-backend
dotnet restore
dotnet run
```

Backend listens on `http://localhost:5000` by default via launch settings.

## API contract

`GET /api/mail/organization-roles`

```json
["Accounting", "Operations"]
```

`GET /api/mail/user-roles?organizationRole=Operations`

```json
["Supervisor", "WFM Administrator"]
```

`POST /api/mail/send`

```json
{
  "subject": "Hello",
  "bodyHtml": "<p>Message body</p>",
  "toRecipients": ["person@contoso.com", "team@contoso.com"],
  "includeSqlRecipients": true,
  "organizationRole": "Operations",
  "userRole": "WFM Administrator"
}
```
