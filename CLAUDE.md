# CLAUDE.md

What you always need. Everything else lives in the reference files at the bottom — read the one
matching what you are about to touch rather than working from memory.

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
dotnet run --project src\CMS.API   # -> http://localhost:5000/swagger
dotnet test

cd src\CMS.NG
npm start                          # -> http://localhost:4200
npm test -- --watch=false --browsers=ChromeHeadless   # bare `npm test` never returns
```

Toolchain traps — the fix here, the reasoning in `spec/conventions/environment.md`:

- Build fails **MSB3027** → a running `dotnet run` holds the exe. Stop it, or `-p:OutDir=<scratch>\`.
- `node` not found → prefix `$env:Path = "C:\Program Files\nodejs;$env:Path"`. Never edit the
  system variable; it is already correct.
- Leave the `global.json` SDK pin alone — SDK 10 is installed and would retarget to `net10.0`.
- The connection string lives in `src/CMS.API/appsettings.json` and nowhere else.

## Rules that hold everywhere

The ones that cost data or a rewrite when missed. The reference files carry the reasoning.

- **Dapper only. No EF, ever.**
- **Read the `CREATE TABLE` before assuming the key shape.** `pkid int IDENTITY` is common but not
  universal — `AppRole` has a string PK, `PublishStatus` a non-IDENTITY `tinyint`. Keys are always
  immutable on edit.
- **Never let the database be the thing that refuses a destructive write.** Project child counts in
  `{Table}Sql.SelectBase`, read the record in the controller, `409` when one is non-zero. Error 547
  is not the only outcome: one FK cascades, and one has no constraint behind it at all.
- **Never PUT a list row straight back.** List and `query` responses carry the n-n key arrays empty
  and the repositories rewrite junction tables from the request, so the write silently clears the
  relations. Re-read with `GET /{table}/{key}` first.
- **Every Insert / Update / Delete writes a `RowAudit` row, on the same transaction.** Repositories
  call `IRowAuditWriter`; an update reads the "before" first and the "after" after, or the
  changed-column list is a guess. A rolled-back change must leave no trail entry claiming it
  happened. **Who** did it is the token's `userId`; **what they are called** is read from `AppUser`
  on that transaction — the `userName` claim is only as fresh as the login that issued it, and a
  rename re-issues nothing. Reading it back is a different object: `IRowAuditRepository` behind
  `GET /api/rowaudit?tableName=&pkid=`, never the writer, and there is no endpoint that edits a
  row. **Every detail and form page renders `RowAuditBadge`** (`core/components/`), so a new page
  needs one — it takes the database table name and the record's **pkid**, which is the surrogate
  key even where the operator's key is a string.
- **Credentials leave the server in exactly one shape: none.** `AuthSql.SelectCredential` is the
  only query selecting `PasswordHash`; keep it out of every other projection, response model and
  JWT payload. The client never hashes.
- **An unhandled exception is answered once, and it says nothing.** `ExceptionHandlingMiddleware`
  is registered first in `Program.cs`: it logs the exception in full server-side and returns one
  fixed `ProblemDetails` 500 — never a stack trace, SQL text or connection details. It only ever
  catches, so 401 / 403 / 400 / 404 / 409, all of which are *returned*, pass through untouched —
  it is the net under a bug, not a substitute for the delete guard above. The Angular
  `authInterceptor` toasts the safe message out of that body; every other status stays the page's
  to handle.
- **Every endpoint needs a token; `AuthController` is the only exception.** Authorization is a
  global `FallbackPolicy` — a new controller is protected by omission, so never add
  `[AllowAnonymous]` to reach one. A valid signature is not the whole of a valid token:
  `TokenFreshness` refuses any whose `iat` predates the account's `PasswordUpdatedTime`. Hiding a
  menu by role is presentation; the API is what refuses.
- **Language split:** UI labels and validation messages are Traditional Chinese, usually paired
  with the English entity name (`角色 AppRole`). Code, identifiers, comments and commit messages
  are English.

## Reference

| Read | Before you |
|------|------------|
| `spec/conventions/backend.md` | touch anything under `src/CMS.API/` |
| `spec/conventions/frontend.md` | touch anything under `src/CMS.NG/` |
| `spec/conventions/testing.md` | write or change a test |
| `spec/conventions/environment.md` | fight the toolchain |
| `spec/code-gen.convention.md` | scaffold an entity — file layout, endpoint shapes, column types |
| `spec/sample1.spec.md` (Course) | need a worked example — FKs, n-n, date ranges, copy action |
| `spec/sample2.spec.md` (SkillTrain) | need a simpler one — n-n with `DisplayOrder` |
| `spec/feature-spec.template.md` | write a new feature spec |
| `spec/{sub-system}/{Table}.md` | change a built entity (e.g. `spec/admin/PublishStatus.md`) |
| `spec/custom/{Feature}/` | build a feature that ships its own spec and mockups |
| `spec/ui-sample-*.png` | build a list / view / edit / add page — **style only**, the data is illustrative |

A feature under `spec/custom/` overrides the house page layout where the two disagree — that is
what a custom spec is for. `spec/promotion/FeaturedPromoItem.md` is the worked example; list your
deviations the way it does.

The `/crud` skill scaffolds an entity: schema → spec → stop for confirmation → both sides plus
tests. **Where it conflicts with this file, this file wins** — it asks for Moq and a sticky
`p-toolbar`, neither of which exists here. `RowAuditWriter` does
(`src/CMS.API/Repositories/`), and every CRUD repository now calls it, so a scaffolded entity must
too — see the 異動紀錄 section of `spec/conventions/backend.md`. The `RowAuditBadgeComponent` it
asks for exists too, as `RowAuditBadge`; it goes at the start of the `.page-header`, which is where
a `#start` toolbar slot lands in a codebase with no `p-toolbar`. Record the remaining deviations in
the generated spec.
