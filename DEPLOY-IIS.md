# CMS — IIS Deployment Guide

How the CMS app is deployed to an on-prem IIS server. Copied into the project by
`copy-to-project.ps1`; the scripts live in `deploy\`.

## Environment

| Item | Value |
|------|-------|
| Host | `Localhost` by default — set `$remote` in both scripts to your IIS server |
| Web Server | IIS 10 |
| Angular site | `CMS` — port **80**, physical path `C:\VHome\CMS\NG` |
| API site | `CMS.API` — port **5001**, physical path `C:\VHome\CMS\API` |
| Angular app pool | `CMS.NG.Pool` (No Managed Code) |
| API app pool | `CMS.API.Pool` (No Managed Code) |
| Angular URL | http://localhost/ |
| API URL | http://localhost:5001 — no Swagger (dev-only; use `dotnet run` locally) |
| Database | `CMS` on `.\SQLEXPRESS` — **must already exist** (Windows auth, as the API app pool identity) |

## Topology — why two sites and a proxy

```
Browser ──▶ IIS site "CMS" :80    (C:\VHome\CMS\NG — the Angular build)
                │
                ├─ /api/*  ──[URL Rewrite + ARR proxy]──▶ http://localhost:5001/api/*
                │                                              │
                │                                    IIS site "CMS.API" :5001
                │                                    (AspNetCoreModuleV2, in-process)
                │                                              │
                │                                              ▼
                └─ anything else ──▶ index.html            SQL Server [CMS]
                   (Angular deep-link fallback)
```

The Angular production build hardcodes `apiUrl: '/api'` (`src/CMS.NG/src/environments/environment.ts`)
— a **relative** path, deliberately: production is meant to be **same-origin**. The ARR proxy is
what makes that true on IIS — the browser only ever talks to port 80, so there is no cross-origin
request to allow. (`Program.cs` does register a loopback CORS policy unconditionally, but in this
topology nothing needs it.) Put an absolute origin back in `environment.ts` and the deployed SPA
calls a port that IIS is not serving; the symptom is a login that fails while the API is healthy. It is the same arrangement as the
nginx `/api` proxy in the Azure demo, and it means **the CMS source needs no changes to deploy**.

## Prerequisite: the database

The `CMS` database is **assumed to exist already**, with its schema and runtime data in place.
This kit deploys the app; it does not build the database.

One row is worth checking before you blame IIS for a failed login: the API reads its **JWT signing
key from `SysConfig.appConfig`** at startup. If that row is missing, `/api/Auth/login` returns 500
no matter how well the sites are configured.

## One-Time Setup

Run from an **elevated** PowerShell. `setup-iis.ps1` is idempotent — re-running it is safe.

```powershell
cd C:\dev\cms\deploy
.\setup-iis.ps1 -GrantSqlAccess
```

It installs IIS, the **ASP.NET Core 9 Hosting Bundle**, **URL Rewrite** and **ARR**; enables the
ARR proxy at server level; creates the folders, app pools and both sites; and grants the pool
identities filesystem rights.

Because the `CMS` site takes **port 80**, the script **stops IIS's stock `Default Web Site`**,
which ships bound to that port. It is stopped, not deleted — `Start-Website -Name 'Default Web Site'`
brings it back (though the two will then compete for port 80). If any *other* site holds port 80,
the script stops with an error rather than guessing.

`-GrantSqlAccess` additionally creates a SQL login for `IIS APPPOOL\CMS.API.Pool` and makes it
`db_owner` on `CMS`. You need it whenever the connection string uses **Windows auth**, because the
site runs as the app pool identity, not as you. Omit it if the API connects with SQL auth.

> If SQL Server lives on a **different machine** from IIS, the pool authenticates as the IIS
> *machine account* (e.g. `DOMAIN\CMSWEB01$`), not `IIS APPPOOL\...`. Adjust `$poolLogin` in
> `setup-iis.ps1`.

### For a remote IIS server

Set `$remote` in both scripts, then:

```powershell
# on the IIS server, as admin:
Enable-PSRemoting -Force

# on your dev machine, as admin, once:
Set-Item WSMan:\localhost\Client\TrustedHosts -Value "CMSWEB01" -Force
```

Your Windows identity needs **administrator rights on the IIS server** — the scripts control app
pools and copy over the `C$` admin share. If it doesn't, pass `-Credential (Get-Credential)`.

## Deploying

```powershell
cd C:\dev\cms\deploy

.\deploy.ps1              # full deploy — API + Angular
.\deploy.ps1 -ApiOnly     # API only
.\deploy.ps1 -NgOnly      # Angular only
.\deploy.ps1 -SkipBuild   # re-copy the last build artifacts without rebuilding
```

## What the Script Does

### API

1. `dotnet publish -c Release -o deploy\publish\API`
2. **Stamps `web.config`** over the one the SDK generated, injecting `ASPNETCORE_ENVIRONMENT` and
   `ConnectionStrings__CMS` as `<environmentVariables>` (from `CMS.API\web.config.template`)
3. **Creates `CMS.API.Pool` if it doesn't exist** (No Managed Code)
4. Stops the pool and waits up to 30 s for it to actually stop — otherwise the DLLs are locked
5. Clears `C:\VHome\CMS\API\*`, copies the publish output in
6. Restarts the pool — in a `finally`, so a failed copy never leaves the site down

### Angular

1. `npm run build` (`angular.json` defaults to the production configuration) → `dist\CMS.NG\browser`
2. **Stamps `web.config`** into the dist with the ARR proxy rule and SPA fallback
   (from `CMS.NG\web.config.template`)
3. **Creates `CMS.NG.Pool` if it doesn't exist**
4. Clears `C:\VHome\CMS\NG\*`, copies the dist in

The Angular pool is **not** stopped — IIS serves static files with no DLL lock.

## Configuration is stamped, not committed

Neither `web.config` lives in the source tree. `deploy.ps1` fills the placeholders in the two
templates at deploy time:

| Template | Placeholder | Filled with |
|---|---|---|
| `CMS.API\web.config.template` | `{{ASPNETCORE_ENVIRONMENT}}` | `$aspnetEnv` |
| | `{{CONNECTION_STRING}}` | `$connString` |
| `CMS.NG\web.config.template` | `{{API_ORIGIN}}` | `http://localhost:$apiPort` |

So neither `web.config` is in the repo, and repointing the SPA at a different API is a config edit,
not a rebuild.

`$connString` in `deploy.ps1` **is** committed, and it must stay in step with
`src/CMS.API/appsettings.json` — the stamped environment variable overrides `appsettings.json` on
IIS, so the two disagreeing is a box that behaves differently from `dotnet run`. It is safe to
commit only because it uses **Windows auth** and therefore carries no password. Point it at a
server that needs SQL auth and the credential belongs somewhere else — not in this file.

> **`ASPNETCORE_ENVIRONMENT` is `Production`, and that is what closes Swagger.** `Program.cs`
> registers Swagger only under `IsDevelopment()`, so a deployed box serves no `/swagger` — the
> document is a complete map of the API, including the call that mints a token, and anyone who can
> reach port 5001 could read it. Use `dotnet run` locally when you want Swagger.
>
> `Production` is safe on an HTTP-only binding **because `Program.cs` calls neither
> `UseHttpsRedirection()` nor `UseHsts()`**. Add either one and a Production API will 307-redirect
> every call to `https://` and the SPA dies — so add the HTTPS binding in the same change, or use
> **`Staging`**, which is neither Development nor Production.

## Response headers the server should not send

IIS and ARR both announce themselves by default. Neither header does anything for the browser;
both hand a scanner a product and version to look up CVEs against. Three settings remove them, and
they are not interchangeable:

| Header | Removed by | Where |
|---|---|---|
| `Server: Microsoft-IIS/10.0` | `<security><requestFiltering removeServerHeader="true" />` | **both** `web.config.template`s (IIS 10+) |
| `X-Powered-By: ARR/3.0` | `system.webServer/proxy/@arrResponseHeader = False` | `setup-iis.ps1` step 3, server level |
| ↑ fallback | outbound rewrite rule | `CMS.NG\web.config.template` |

The order matters. `<customHeaders><remove name="X-Powered-By" />` does **not** work for the ARR
header — ARR stamps it on the way out, after that module has run. An outbound rewrite rule catches
it, but a rule can only rewrite a value, not delete a header, so on its own it leaves an **empty**
`X-Powered-By:` rather than none. Only `arrResponseHeader = False` stops the header being added at
all; the rewrite rule is kept as a fallback for a box where `setup-iis.ps1` has not been re-run.

ASP.NET Core adds nothing of its own here — there is no `X-AspNet-Version` or
`X-Powered-By: ASP.NET` to strip. The `Server` header on `:5001` comes from IIS, not Kestrel.

## Security headers

The SPA site sends `Content-Security-Policy`, `X-Content-Type-Options`, `X-Frame-Options`,
`Referrer-Policy` and `Permissions-Policy`. The API site sends only `Cache-Control: no-store`,
because the SPA site adds its set to proxied `/api` responses too and duplicating them there just
emits each header twice. The division: the SPA site owns what the browser sees, the API site owns
what is true about the response itself.

`Strict-Transport-Security` is deliberately absent. Browsers ignore it over plain HTTP, and a long
`max-age` set before TLS exists strands clients that later need to fall back. It belongs in the
same change as the HTTPS binding.

### The CSP is coupled to an Angular build setting

`script-src 'self'` refuses inline event handlers. With `optimization.styles.inlineCritical` on,
Angular emits exactly one:

```html
<link rel="stylesheet" href="styles-*.css" media="print" onload="this.media='all'">
```

CSP blocks the `onload`, so the stylesheet stays `media="print"` and **never applies**. The page
still renders — the critical CSS was inlined into `index.html` — which is what makes this so easy
to ship. What breaks is everything outside that subset: PrimeIcons first, so every icon-only
button renders blank. `getComputedStyle($0).fontFamily` on a `.pi` element reads `Arial` instead
of `primeicons`.

`angular.json` therefore sets `inlineCritical: false` for the production configuration. The cost is
one render-blocking stylesheet; the alternative is `'unsafe-hashes'` plus a hash that Angular is
free to change between versions.

**Verify a CSP change in a browser, not with `curl`.** The headers were byte-identical in the
working and the broken build.

## Troubleshooting

| Symptom | Fix |
|---------|-----|
| `/api/*` returns **404**, SPA loads fine | The ARR server proxy is off. `Set-WebConfigurationProperty -PSPath 'MACHINE/WEBROOT/APPHOST' -Filter system.webServer/proxy -Name enabled -Value True` — this is what `setup-iis.ps1` step 3 does. |
| `/api/*` returns **502.3** | The `CMS.API` site is down. Hit http://localhost:5001/api/auth/login directly — a **405** proves the app is running (there is no `/swagger` on a deployed box); anything 502-shaped means it never started, so read `C:\VHome\CMS\API\logs\stdout*.log`. |
| API returns **500.19** | Config error — usually URL Rewrite not installed, or the pool identity can't read `C:\VHome\CMS\API`. |
| API returns **500.30 / 502.5** | ASP.NET Core Hosting Bundle missing, or `arguments=".\CMS.API.dll"` doesn't match the published DLL name. |
| Every API call **307-redirects to https** | Someone added `UseHttpsRedirection()` / `UseHsts()` to `Program.cs` under `IsProduction()`, on an HTTP-only site. Give IIS an HTTPS binding, or set `$aspnetEnv` to `Staging` — **not** `Development`, which reopens Swagger. |
| **`/swagger` returns 404** on the deployed box | Working as intended — it is Development-only. Run `dotnet run --project src\CMS.API` locally for Swagger. |
| Responses carry an **empty `X-Powered-By:`** | `setup-iis.ps1` has not been re-run since `arrResponseHeader` was added. The outbound rule blanked the value; only the server-level switch removes the header. Re-run `.\setup-iis.ps1` elevated — it is idempotent. |
| Login returns **500** | The database has no `SysConfig.appConfig` row — the JWT signing key is read from it at runtime. |
| API **500** on any data call | The app pool identity has no SQL access. The site runs as `IIS APPPOOL\CMS.API.Pool`, not as you — `setup-iis.ps1 -GrantSqlAccess` creates that login. |
| **F5 on a deep link → 404** | The SPA fallback rewrite is missing. Confirm `web.config` reached `C:\VHome\CMS\NG\` and URL Rewrite is installed. |
| Deployed, but the browser shows the **old app** | Hard-refresh. `index.html` is served no-cache by the stamped `web.config`; a stale copy pins the old hashed bundle names. |
| `dotnet publish` fails | Run it by hand in `src\CMS.API`. |
| `npm run build` fails | Run it by hand in `src\CMS.NG`. `deploy.ps1` runs `npm ci` automatically only when `node_modules` is absent. |
| Angular build output not found | Don't use `-SkipBuild` before a successful build has run. |
| `Access is denied` on `Invoke-Command` | Run PowerShell as admin, or pass `-Credential`. |
| `The client cannot connect to the destination` | Do the remote one-time setup above (`Enable-PSRemoting` / `TrustedHosts`). |
| Site `CMS` won't start / port 80 in use | Another site or process owns port 80. `setup-iis.ps1` stops `Default Web Site` automatically, but not anything else — find it with `Get-Website`, or `netstat -ano \| findstr :80`. |
| Port 5001 already bound | Change the port in **both** `setup-iis.ps1` and `deploy.ps1`. |
| Need `Default Web Site` back | `Start-Website -Name 'Default Web Site'` (it was stopped, not deleted — but it will fight `CMS` for port 80). |
