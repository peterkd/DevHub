# AGENTS.md

## Cursor Cloud specific instructions

This repo is a single product split into two services (see `README.md`):

- `email-composer-frontend` — Angular 21 app (Quill rich-text email composer). Dev server: `npm start` (`ng serve`) on `http://localhost:4200`. Build: `npm run build`. Tests: `npx ng test --watch=false` (Vitest).
- `email-composer-backend` — ASP.NET Core (net8.0) Web API that sends mail via Microsoft Graph. Build: `dotnet build`. Run: see caveat below. Swagger UI at `/swagger` in Development.

Non-obvious caveats:

- The .NET SDK (8.0) is installed under `~/.dotnet` and added to `PATH` via `~/.bashrc`; it is not from apt. New non-login shells may need `export PATH="$HOME/.dotnet:$PATH"`.
- Backend HTTPS/dev-cert gotcha: `dotnet run` uses `Properties/launchSettings.json`, which binds both `http://localhost:5000` and `https://localhost:7000`, and `Program.cs` calls `UseHttpsRedirection()`. The frontend targets the HTTP URL (`src/environments/environment.ts` -> `http://localhost:5000`), so browser requests get 307-redirected to the untrusted dev cert on :7000 and fail. To develop/test through the browser, run the backend HTTP-only so no redirect happens: `ASPNETCORE_ENVIRONMENT=Development ASPNETCORE_URLS="http://localhost:5000" dotnet run --no-launch-profile`.
- Actual email delivery requires Microsoft Graph / Azure AD credentials in `appsettings.Development.json` (or env vars) under `Graph:` (`TenantId`, `ClientId`, `ClientSecret`, `SenderUserId`). These ship empty, so `POST /api/mail/send` returns HTTP 400 `Graph settings are incomplete...` until real credentials are supplied. The compose UI and full frontend->backend request path work without them.
- `Services/SqlRecipientService.cs` is orphaned/dead code (not registered in DI, not referenced by any controller). It requires the `Microsoft.Data.SqlClient` package. The `.csproj` also depends on `Swashbuckle.AspNetCore` for the Swagger calls in `Program.cs`.
- Known pre-existing test failure: `src/app/app.spec.ts` "should render title" is a stale Angular scaffold test that expects "Hello, email-composer-frontend" but the app renders "HTML Email Composer". This is unrelated to environment setup.
