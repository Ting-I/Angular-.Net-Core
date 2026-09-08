# CLAUDE.md

What you always need. The detail lives in the reference files at the bottom — read the one that
matches what you are about to touch, rather than working from memory.

## What this repo is

A CMS scaffolded from an **existing** SQL Server schema. The database is the source of truth:
`database/*.sql` are the real `CREATE TABLE` scripts and are **read-only reference**. Never
generate migrations or alter the schema; write code that matches what is already there, and follow
the house patterns rather than inventing one per feature.

```
database/          # schema reference (read-only)
spec/              # conventions + feature specs + UI mockups
src/CMS.API/       # .NET 9 Web API, Dapper (NO Entity Framework), port 5000
src/CMS.API.Tests/ # xUnit
src/CMS.NG/        # Angular 20 standalone + PrimeNG 20, port 4200
```

## Commands

```powershell
dotnet run --project src\CMS.API   # API -> http://localhost:5000/swagger
dotnet test

cd src\CMS.NG
npm start                          # UI  -> http://localhost:4200
npm test -- --watch=false --browsers=ChromeHeadless
```

`npm test` without those flags enters watch mode and opens a browser — always pass them when
running it non-interactively.

## Environment gotchas

- **`global.json` pins the .NET 9 SDK.** SDK 10 is also installed on this machine and would be
  picked by default, targeting `net10.0`. Leave the pin in place.
- **`node` may be missing from an agent shell's PATH.** It *is* in the machine PATH
  (`C:\Program Files\nodejs\`); a shell that can't see it inherited a stale environment snapshot.
  Prefix with `$env:Path = "C:\Program Files\nodejs;$env:Path"` rather than editing any environment
  variable — the system one is already correct.
- Connection string lives in `src/CMS.API/appsettings.json` only (`Server=.\SQLEXPRESS;Database=CMS`).
  `appsettings.Development.json` deliberately does not repeat it.
- A running `dotnet run` holds a lock on `CMS.API.exe` and fails the next build with MSB3027. Stop
  it, or send the test build elsewhere with `-p:OutDir=`.

## Rules that hold everywhere

The ones that cost data or a rewrite when missed; the reference files carry the detail behind each.

- **Dapper only. No EF, ever.**
- **Read the `CREATE TABLE` before assuming the key shape.** `pkid int IDENTITY` is common but not
  universal — `AppRole` has a string PK and `PublishStatus` a non-IDENTITY `tinyint` the operator
  supplies. Keys are always immutable on edit.
- **Never let the database be the thing that refuses a destructive write.** Project child counts in
  `{Table}Sql.SelectBase`, read the record in the controller, return `409` when one is non-zero.
  `FK_Course_CourseGroup` is `ON DELETE CASCADE`, so an unguarded delete silently destroys courses;
  elsewhere a reference has no FK behind it at all and the delete just orphans rows.
- **Never PUT a list row straight back.** List and `query` responses carry the n-n key arrays empty,
  and the repositories rewrite their junction tables from whatever the request holds — so a write
  built from a list row silently clears the relations. Re-read with `GET /{table}/{key}` first.
- **Credentials leave the server in exactly one shape: none.** `AuthSql.SelectCredential` is the
  only query in the API that selects `PasswordHash` — keep it out of `AppUserSql.SelectBase`, out of
  every response model, and out of the JWT payload. `POST /api/auth/login` answers one identical
  `401` for an unknown UserId, an `IsActive = 0` account and a wrong password, so the endpoint
  cannot be used to enumerate accounts; the JWT signing secret is the `symmetricSecurityKey` of the
  `SysConfig` `appConfig` JSON, read per login — and again per validated request — and never
  hard-coded or cached. **A valid signature is not the whole of a valid token:** `TokenFreshness`
  refuses one whose `iat` predates the account's `PasswordUpdatedTime`, so changing a password
  signs out every session issued before it.
- **Every endpoint needs a token; `AuthController` is the only exception.** Authorization is a
  global `FallbackPolicy`, so a new controller is protected by omission — don't add
  `[AllowAnonymous]` to reach one. On the UI side the token lives in **session** storage, an
  interceptor attaches it, a guard keeps the routes shut without it, and a `401` ends the session.
  Hiding a menu by role is presentation only; the API is what actually refuses.
- **Language split:** UI labels and validation messages are Traditional Chinese, usually paired with
  the English entity name (`角色 AppRole`, `權限等級`). Code, identifiers, comments and commit
  messages are English.

## Reference

| Read | Before you |
|------|------------|
| `spec/conventions/backend.md` | touch anything under `src/CMS.API/` |
| `spec/conventions/frontend.md` | touch anything under `src/CMS.NG/` |
| `spec/conventions/testing.md` | write or change a test |
| `spec/code-gen.convention.md` | scaffold an entity — file layout, endpoint shapes, column types |
| `spec/sample1.spec.md` (Course) | need a worked example — FKs, n-n, date ranges, copy action |
| `spec/sample2.spec.md` (SkillTrain) | need a simpler one — n-n with `DisplayOrder` |
| `spec/feature-spec.template.md` | write a new feature spec |
| `spec/{sub-system}/{Table}.md` | change a built entity (e.g. `spec/admin/PublishStatus.md`) |
| `spec/custom/{Feature}/` | build a feature that ships its own spec and mockups |
| `spec/ui-sample-*.png` | build a list / view / edit / add page. **Style only** — the data in them is illustrative |

A feature under `spec/custom/` overrides the house page layout where the two disagree — that is what
the custom spec is for. `spec/promotion/FeaturedPromoItem.md` is the worked example and lists every
deviation it took; do the same in any generated spec.

The `/crud` skill (`.claude/skills/crud`) scaffolds an entity: it reads the schema, writes
`spec/{sub-system}/{Table}.md`, stops for confirmation, then builds both sides plus tests. Where it
conflicts with this file, **this file wins** — it asks for Moq and a `RowAuditWriter`, neither of
which exists here, and for a sticky `p-toolbar` where the pinned bar is a `.sticky-toolbar` wrapped
around the house `.page-header`. Record any such deviation in the generated spec.
