# HTML Email Composer (Angular + .NET + Microsoft Graph)

This repository contains:

- `email-composer-frontend`: Angular app with a rich text editor (Quill) for composing HTML email and inserting local image files.
- `email-composer-backend`: ASP.NET Core Web API that acquires a token via Azure AD client credentials and calls:
  - `POST https://graph.microsoft.com/v1.0/users/{id}/sendMail`

## Frontend

### Features

- Rich text editor for HTML body
- Insert local image files into editor as inline base64 images
- Subject field
- Recipient addresses are loaded by the backend from Azure SQL active WFM Administrators
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

### Azure SQL configuration

Configure the Azure SQL connection string in `email-composer-backend/appsettings.Development.json` or with the
`ConnectionStrings__AzureSqlDatabase` environment variable:

```json
"ConnectionStrings": {
  "AzureSqlDatabase": "Server=tcp:<server>.database.windows.net,1433;Initial Catalog=<database>;Persist Security Info=False;User ID=<user>;Password=<password>;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;"
}
```

The backend uses this query to select recipient email addresses:

```sql
WITH WorkerRoles AS (
    SELECT
        Worker.[Id] AS WorkerId,
        Worker.[Role] AS RoleName
    FROM [dbo].[Worker] Worker
    UNION
    SELECT
        Worker.[Id] AS WorkerId,
        WR.[Name] AS RoleName
    FROM [dbo].[Worker] Worker
    LEFT JOIN [dbo].[WorkerRoleAssignment] WRA ON WRA.[WorkerId] = Worker.[Id]
    INNER JOIN [dbo].[WorkerRole] WR ON WR.[Id] = WRA.[RoleId]
),
WorkerRolesAgg AS (
    SELECT
        WorkerId,
        STRING_AGG(RoleName, ', ') AS Roles
    FROM WorkerRoles
    GROUP BY WorkerId
),
CompanyActiveWfmAdmins AS (
    SELECT
        Worker.[EmailAddress]
    FROM [dbo].[Worker] Worker
    INNER JOIN WorkerRoles ON WorkerRoles.WorkerId = Worker.[Id]
    WHERE WorkerRoles.RoleName = 'WFM Administrator' AND Worker.[Status] = 'A'
)
SELECT DISTINCT
    EmailAddress
FROM CompanyActiveWfmAdmins
WHERE EmailAddress IS NOT NULL AND LTRIM(RTRIM(EmailAddress)) <> ''
ORDER BY EmailAddress;
```

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
  "bodyHtml": "<p>Message body</p>"
}
```
