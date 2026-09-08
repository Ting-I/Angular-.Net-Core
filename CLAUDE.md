# CLAUDE.md

Guidance for working in this repository. This file is what you always need; the detail lives in the
reference files at the bottom — read the one that matches what you are about to touch.

## What this repo is

A CMS scaffolded from an **existing** SQL Server schema. The database is the source of truth —
`database/*.sql` are the real `CREATE TABLE` scripts and are **read-only reference**. Never
generate migrations or alter the schema; write code that matches what is already there.

Features follow the house patterns rather than being invented per-feature.

## Layout

```
database/          # schema reference (read-only)
spec/              # conventions + feature specs + UI mockups
src/CMS.API/       # .NET 9 Web API, Dapper (NO Entity Framework), port 5000
src/CMS.API.Tests/ # xUnit
src/CMS.NG/        # Angular 20 standalone + PrimeNG 20, port 4200
```

## Commands

```powershell
dotnet run --project src\CMS.API          # API  -> http://localhost:5000/swagger
dotnet test                                # 417 xUnit tests

cd src\CMS.NG
npm start                                  # UI   -> http://localhost:4200
npm test -- --watch=false --browsers=ChromeHeadless   # 401 Karma/Jasmine specs
```

`npm test` without flags enters watch mode and opens a browser — always pass the flags above when
running it non-interactively.

## Environment gotchas

- **`global.json` pins the .NET 9 SDK.** SDK 10 is also installed on this machine and would be
  picked by default, targeting `net10.0`. Leave the pin in place.
- **`node` may be missing from an agent shell's PATH.** It *is* in the machine PATH
  (`C:\Program Files\nodejs\`) — a shell that can't see it inherited a stale environment snapshot.
  Prefix with `$env:Path = "C:\Program Files\nodejs;$env:Path"` rather than editing any environment
  variable; the system variable is already correct.
- Connection string lives in `src/CMS.API/appsettings.json` only (`Server=.\SQLEXPRESS;Database=CMS`).
  `appsettings.Development.json` deliberately does not repeat it.
- A running `dotnet run` holds a lock on `CMS.API.exe` and fails the next build with MSB3027. Stop
  it, or send the test build elsewhere with `-p:OutDir=`.

## Rules that hold everywhere

- **Dapper only. No EF, ever.**
- **Never rely on the database to refuse a DELETE.** Project child counts in `{Table}Sql.SelectBase`,
  read the record in the controller, return `409` when a count is non-zero. `FK_Course_CourseGroup`
  is `ON DELETE CASCADE` and would silently destroy courses; `Seminar.Partner_pkid` has no FK behind
  it at all. Both cases are in `spec/conventions/backend.md`.
- **Read the `CREATE TABLE` before assuming the key shape.** `pkid int IDENTITY` is common but not
  universal — `AppRole` has a string PK and `PublishStatus` a non-IDENTITY `tinyint` the operator
  supplies. Keys are always immutable on edit.
- Filter logic goes in a static `{Table}Sql.BuildWhere` returning `(where, parameters)`, so query
  filters are unit-testable without a database.
- `PUT` takes the key **from the body**, not a route param.
- `nchar(n)` needs `RTRIM()` in every SELECT; `date` / `time(7)` map to `DateOnly` / `TimeOnly` via
  the handlers already registered in `Program.cs`.
- **No mocking library.** Backend tests run against hand-written fakes and never touch SQL Server.
- **QR codes go through `core/utils/qr-code.util.ts`** — `qrPngDataUrl` / `downloadDataUrl` wrap
  `qrcode-generator`, which only yields a module matrix and a GIF. The util draws the canvas, so the
  `<img>` and the saved file are the same PNG bytes. `course-detail` is the worked example.
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
the custom spec is for. `spec/promotion/FeaturedPromoItem.md` is the worked example, and it lists
every deviation it took. Do the same in any generated spec.

The `/crud` skill (`.claude/skills/crud`) automates scaffolding: it reads the schema, writes
`spec/{sub-system}/{Table}.md`, stops for confirmation, then builds both sides plus tests. Where it
conflicts with this file, **this file wins** — it asks for Moq, a `RowAuditWriter` and a sticky
`p-toolbar`, none of which exist here. Record any such deviation in the generated spec.
