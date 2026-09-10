# CMS

Full-stack CMS built against the existing SQL Server schema in `database/`.

| Layer | Project | Stack | Port |
|-------|---------|-------|------|
| Backend | `src/CMS.API` | .NET 9 Web API, Dapper (no EF), Swashbuckle 7.2.0 | 5000 |
| Backend tests | `src/CMS.API.Tests` | xUnit | — |
| Frontend | `src/CMS.NG` | Angular 20 standalone, PrimeNG 20 | 4200 |

## Running

```powershell
# API — Swagger UI at http://localhost:5000/swagger
dotnet run --project src/CMS.API

# Angular — http://localhost:4200
cd src/CMS.NG
npm start
```

The API connects to `Server=.\SQLEXPRESS;Database=CMS;Trusted_Connection=True` (set in
`src/CMS.API/appsettings.json`). CORS is open to any `localhost` / loopback origin.

The frontend does **not** proxy; it reads `apiUrl` from `src/environments/environment.ts`
(`environment.development.ts` is substituted by the `development` build configuration).

## Tests

```powershell
dotnet test                                    # 737 xUnit tests
cd src/CMS.NG; npm test -- --watch=false       # 663 Karma/Jasmine specs
```

## Path aliases (`tsconfig.json`)

| Alias | Target |
|-------|--------|
| `@env` | `src/environments/environment` |
| `@env/*` | `src/environments/*` |
| `@app/*` | `src/app/*` |
| `@core/*` | `src/app/core/*` |
| `@features/*` | `src/app/features/*` |
| `@layout/*` | `src/app/layout/*` |

## Feature: 角色 AppRole

Sidebar: **系統管理 Admin → 角色 AppRole**

`AppRole` uses a string primary key (`RoleId` nvarchar(200)); `pkid` is a non-key IDENTITY
column shown in the list as 主代碼. Routes therefore use `{id}` with no `:int` constraint and
the Angular service applies `encodeURIComponent`. `AppUserRole` is the n-n junction to
`AppUser`, written delete-then-reinsert and surfaced as a 使用者數 count on the list.

| Method | Route | Notes |
|--------|-------|-------|
| GET | `/api/app-roles` | All roles |
| POST | `/api/app-roles/query` | Filter by `keyword` (RoleId/RoleName/Description) and `permissionLevel` |
| GET | `/api/app-roles/{id}` | Single role, including `userIds` |
| POST | `/api/app-roles` | Create — 409 on duplicate RoleId |
| PUT | `/api/app-roles` | Update — key in body, RoleId immutable |
| DELETE | `/api/app-roles/{id}` | Delete (clears junction rows first) |
| GET | `/api/lookups/app-users` | Options for the 使用者 multiselect |

Frontend pages: `app-role-list` (sortable `p-table`, `p-drawer` filter, session-storage
filter/sort/page state), `app-role-detail`, `app-role-form` (add + edit).
